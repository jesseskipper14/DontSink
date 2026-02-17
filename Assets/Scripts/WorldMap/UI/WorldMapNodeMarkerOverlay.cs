using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldMapNodeMarkerOverlay : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapEventManager eventManager;

    [Tooltip("Container holding node buttons (anchoredPosition space).")]
    [SerializeField] private RectTransform nodeContainer;

    [Tooltip("Container to spawn marker icons under (same anchored space as nodeContainer).")]
    [SerializeField] private RectTransform markerContainer;

    [Header("Marker Prefabs")]
    [SerializeField] private Image buffIconPrefab;
    [SerializeField] private Image eventIconPrefab;

    [Header("Layout")]
    [Min(4f)][SerializeField] private float iconSizePx = 12f;
    [SerializeField] private Vector2 buffOffset = new Vector2(10f, 8f);
    [SerializeField] private Vector2 eventOffset = new Vector2(-10f, 8f);

    [Header("Refresh")]
    [SerializeField] private bool autoRefresh = true;
    [Min(0.1f)][SerializeField] private float refreshInterval = 0.25f;

    // Pools keyed by node index
    private readonly Dictionary<int, Image> _buffIcons = new();
    private readonly Dictionary<int, Image> _eventIcons = new();

    private readonly List<WorldMapEventInstance> _tmpEvents = new(8);

    private float _t;

    private void Reset()
    {
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        eventManager = FindAnyObjectByType<WorldMapEventManager>();
    }

    private void OnEnable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt += RebuildAll;

        RebuildAll();
    }

    private void OnDisable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt -= RebuildAll;
    }

    private void Update()
    {
        if (!autoRefresh) return;

        _t += Time.unscaledDeltaTime;
        if (_t >= refreshInterval)
        {
            _t = 0f;
            Refresh();
        }
    }

    [ContextMenu("Rebuild All Markers")]
    public void RebuildAll()
    {
        ClearAll();
        Refresh();
    }

    private void Refresh()
    {
        if (generator?.graph == null) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;
        if (nodeContainer == null || markerContainer == null) return;

        int n = generator.graph.nodes.Count;

        // Track which nodes are still “active” this refresh so we can hide extras
        var aliveBuff = new HashSet<int>();
        var aliveEvent = new HashSet<int>();

        for (int nodeIndex = 0; nodeIndex < n; nodeIndex++)
        {
            // Find the node button’s anchored position (most robust)
            // We assume MapOverlayController named them NodeBtn_{nodeId}; but safer: find by component.
            // For now: search child with MapNodeHoverTarget.NodeId == nodeIndex.
            if (!TryGetNodeAnchoredPosition(nodeIndex, out var nodePos))
                continue;

            // ===== BUFFS =====
            bool hasBuff = false;
            if (runtimeBinder.Registry.TryGetByIndex(nodeIndex, out var rt) && rt != null && rt.State != null)
            {
                var buffs = rt.State.ActiveBuffs;
                hasBuff = buffs != null && buffs.Count > 0;
            }

            if (hasBuff)
            {
                var icon = GetOrCreate(_buffIcons, nodeIndex, buffIconPrefab);
                PositionIcon(icon.rectTransform, nodePos + buffOffset);
                aliveBuff.Add(nodeIndex);
            }

            // ===== EVENTS =====
            bool hasEvent = false;
            if (eventManager != null)
            {
                _tmpEvents.Clear();
                eventManager.GetEventsAtNode(nodeIndex, _tmpEvents);
                hasEvent = _tmpEvents.Count > 0;
            }

            if (hasEvent)
            {
                var icon = GetOrCreate(_eventIcons, nodeIndex, eventIconPrefab);
                PositionIcon(icon.rectTransform, nodePos + eventOffset);
                aliveEvent.Add(nodeIndex);
            }
        }

        HideDead(_buffIcons, aliveBuff);
        HideDead(_eventIcons, aliveEvent);
    }

    private bool TryGetNodeAnchoredPosition(int nodeIndex, out Vector2 anchoredPos)
    {
        anchoredPos = default;
        // Find node button by MapNodeHoverTarget
        for (int i = 0; i < nodeContainer.childCount; i++)
        {
            var child = nodeContainer.GetChild(i);
            var ht = child.GetComponent<MapNodeHoverTarget>();
            if (ht != null && ht.NodeId == nodeIndex)
            {
                anchoredPos = ((RectTransform)child).anchoredPosition;
                return true;
            }
        }
        return false;
    }

    private Image GetOrCreate(Dictionary<int, Image> dict, int nodeIndex, Image prefab)
    {
        if (dict.TryGetValue(nodeIndex, out var img) && img != null)
        {
            img.gameObject.SetActive(true);
            return img;
        }

        if (prefab == null)
        {
            // Fallback: create a basic Image so we don't crash.
            var go = new GameObject($"Marker_{nodeIndex}");
            go.transform.SetParent(markerContainer, false);
            img = go.AddComponent<Image>();
        }
        else
        {
            img = Instantiate(prefab, markerContainer);
        }

        img.name = $"{img.name}_Node{nodeIndex}";
        dict[nodeIndex] = img;

        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(iconSizePx, iconSizePx);

        return img;
    }

    private void PositionIcon(RectTransform rt, Vector2 anchoredPos)
    {
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(iconSizePx, iconSizePx);
    }

    private static void HideDead(Dictionary<int, Image> dict, HashSet<int> alive)
    {
        // Avoid modifying dict while iterating keys by copying keys to temp list
        var keys = ListPool<int>.Get();
        keys.AddRange(dict.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            int k = keys[i];
            if (!alive.Contains(k) && dict[k] != null)
                dict[k].gameObject.SetActive(false);
        }

        ListPool<int>.Release(keys);
    }

    private void ClearAll()
    {
        foreach (var kv in _buffIcons) if (kv.Value != null) Destroy(kv.Value.gameObject);
        foreach (var kv in _eventIcons) if (kv.Value != null) Destroy(kv.Value.gameObject);

        _buffIcons.Clear();
        _eventIcons.Clear();
    }

    // Tiny pooled list helper (so we don't allocate every refresh)
    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> _pool = new();

        public static List<T> Get() => _pool.Count > 0 ? _pool.Pop() : new List<T>(64);

        public static void Release(List<T> list)
        {
            list.Clear();
            _pool.Push(list);
        }
    }
}
