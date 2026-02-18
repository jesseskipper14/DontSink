using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldMapNodeMarkerOverlay : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapEventManager eventManager;

    [SerializeField] private RectTransform nodeContainer;
    [SerializeField] private RectTransform markerContainer;

    [Header("Prefabs")]
    [SerializeField] private Image buffIconPrefab;
    [SerializeField] private Image eventIconPrefab;

    [Header("Buff Layout (bottom)")]
    [Min(1)] public int buffMaxPerRow = 4;
    [Min(0)] public int buffMaxShown = 12;
    [Min(1f)] public float buffIconSizePx = 10f;
    [Min(0f)] public float buffXSpacingPx = 12f;
    [Min(0f)] public float buffYSpacingPx = 12f;
    [Min(0f)] public float buffPadFromNodePx = 6f;

    [Header("Event Layout (top)")]
    [Min(1)] public int eventMaxPerRow = 4;
    [Min(0)] public int eventMaxShown = 8;
    [Min(1f)] public float eventIconSizePx = 10f;
    [Min(0f)] public float eventXSpacingPx = 12f;
    [Min(0f)] public float eventYSpacingPx = 12f;
    [Min(0f)] public float eventPadFromNodePx = 6f;

    [Header("Refresh")]
    public bool autoRefresh = true;
    [Min(0.1f)] public float refreshIntervalSeconds = 0.25f;

    private float _t;

    // pools: nodeIndex -> list of icons
    private readonly Dictionary<int, List<Image>> _buffPool = new();
    private readonly Dictionary<int, List<Image>> _eventPool = new();

    private readonly List<WorldMapEventInstance> _tmpEvents = new(16);

    private void OnEnable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt -= Refresh;
    }

    private void Update()
    {
        if (!autoRefresh) return;

        _t += Time.unscaledDeltaTime;
        if (_t >= refreshIntervalSeconds)
        {
            _t = 0f;
            Refresh();
        }
    }

    [ContextMenu("Refresh Markers")]
    public void Refresh()
    {
        if (generator?.graph == null) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;
        if (nodeContainer == null || markerContainer == null) return;

        int nodeCount = generator.graph.nodes.Count;

        // We only care about nodes that actually have buttons spawned.
        // So iterate nodeContainer children.
        for (int i = 0; i < nodeContainer.childCount; i++)
        {
            var child = nodeContainer.GetChild(i);
            var ht = child.GetComponent<MapNodeHoverTarget>();
            if (ht == null) continue;

            int nodeIndex = ht.NodeId;
            if (nodeIndex < 0 || nodeIndex >= nodeCount) continue;

            var nodeRt = (RectTransform)child;
            Vector2 nodePos = nodeRt.anchoredPosition;
            float halfH = nodeRt.sizeDelta.y * 0.5f;

            // ---- BUFFS (bottom) ----
            int buffShow = 0;
            IReadOnlyList<TimedBuffInstance> buffs = null;

            if (runtimeBinder.Registry.TryGetByIndex(nodeIndex, out var rt) && rt != null && rt.State != null)
            {
                // IMPORTANT: if ActiveBuffs doesn't reflect injected buffs, swap to ActiveBuffsMutable.
                buffs = rt.State.ActiveBuffs;
                buffShow = Mathf.Min(buffs?.Count ?? 0, buffMaxShown);
            }

            Vector2 buffAnchor = nodePos + new Vector2(0f, -halfH - buffPadFromNodePx);
            DrawGridIcons(_buffPool, nodeIndex, buffIconPrefab, buffShow, buffMaxPerRow,
                buffIconSizePx, buffXSpacingPx, buffYSpacingPx, buffAnchor,
                tintFn: (k) => BuffTint(buffs, k));

            // ---- EVENTS (top) ----
            int eventShow = 0;
            if (eventManager != null)
            {
                _tmpEvents.Clear();
                eventManager.GetEventsAtNode(nodeIndex, _tmpEvents);
                eventShow = Mathf.Min(_tmpEvents.Count, eventMaxShown);
            }

            Vector2 eventAnchor = nodePos + new Vector2(0f, +halfH + eventPadFromNodePx);
            DrawGridIcons(_eventPool, nodeIndex, eventIconPrefab, eventShow, eventMaxPerRow,
                eventIconSizePx, eventXSpacingPx, eventYSpacingPx, eventAnchor,
                tintFn: null);
        }
    }

    private Color BuffTint(IReadOnlyList<TimedBuffInstance> buffs, int i)
    {
        if (buffs == null || i < 0 || i >= buffs.Count) return Color.white;

        var inst = buffs[i];
        if (inst.buff == null) return Color.white;

        float a = inst.buff.accelPerHour;
        return a >= 0f ? new Color(0.3f, 1f, 0.4f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
    }

    private void DrawGridIcons(
        Dictionary<int, List<Image>> pool,
        int nodeIndex,
        Image prefab,
        int show,
        int maxPerRow,
        float iconSize,
        float xSpacing,
        float ySpacing,
        Vector2 anchor,
        System.Func<int, Color> tintFn)
    {
        if (prefab == null) return;

        if (!pool.TryGetValue(nodeIndex, out var list) || list == null)
        {
            list = new List<Image>(maxPerRow * 3);
            pool[nodeIndex] = list;
        }

        // Ensure enough
        while (list.Count < show)
        {
            var img = Instantiate(prefab, markerContainer);
            img.raycastTarget = false;
            list.Add(img);
        }

        // Position active
        for (int i = 0; i < show; i++)
        {
            var img = list[i];
            img.gameObject.SetActive(true);

            int row = i / maxPerRow;
            int col = i % maxPerRow;

            float x = (col - (maxPerRow - 1) * 0.5f) * xSpacing;
            float y = -row * ySpacing;

            var rt = img.rectTransform;
            rt.anchoredPosition = anchor + new Vector2(x, y);
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            if (tintFn != null)
                img.color = tintFn(i);
        }

        // Hide extras
        for (int i = show; i < list.Count; i++)
        {
            if (list[i] != null)
                list[i].gameObject.SetActive(false);
        }
    }
}
