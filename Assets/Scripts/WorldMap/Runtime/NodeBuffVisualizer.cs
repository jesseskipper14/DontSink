using System.Collections.Generic;
using UnityEngine;

// DEPRECATED

public class NodeBuffVisualizer : MonoBehaviour
{
    [Header("Layout")]
    public int maxPerRow = 4;
    public int maxShown = 12;
    public float iconSize = 0.18f;
    public float xSpacing = 0.22f;
    public float ySpacing = 0.22f;
    public Vector2 offset = new Vector2(0f, -0.45f);

    [Header("Refresh")]
    public float refreshIntervalSeconds = 1f;

    private float _t;
    private WorldMapRuntimeBinder _runtimeBinder;
    private MapNodeView _nodeView;

    private readonly List<GameObject> _icons = new();
    private Sprite _dotSprite;

    public void Initialize(WorldMapRuntimeBinder runtimeBinder, MapNodeView nodeView)
    {
        _runtimeBinder = runtimeBinder;
        _nodeView = nodeView;
        _dotSprite = MakeDotSprite();
    }

    private void Update()
    {
        if (_nodeView == null) return;
        if (_runtimeBinder == null || !_runtimeBinder.IsBuilt) return;

        _t += Time.deltaTime;
        if (_t >= refreshIntervalSeconds)
        {
            _t = 0f;
            Refresh();
        }
    }

    public void Refresh()
    {
        int id = _nodeView.NodeId;
        if (id < 0) return;
        if (_runtimeBinder == null || !_runtimeBinder.IsBuilt) return;
        if (!_runtimeBinder.Registry.TryGetByIndex(id, out var rt)) return;

        var state = rt.State;
        if (state == null) return;

        var buffs = state.ActiveBuffs;
        int count = buffs == null ? 0 : buffs.Count;
        int show = Mathf.Min(count, maxShown);

        EnsureIconCount(show);

        for (int i = 0; i < show; i++)
        {
            var inst = buffs[i];
            var go = _icons[i];
            go.SetActive(true);

            int row = i / maxPerRow;
            int col = i % maxPerRow;

            float x = (col - (maxPerRow - 1) * 0.5f) * xSpacing + offset.x;
            float y = offset.y - row * ySpacing;

            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one * iconSize;

            var view = go.GetComponent<BuffIconView>();
            string name = inst.buff != null ? inst.buff.displayName : "(null)";
            float rem = inst.RemainingHours;

            // tint: positive accel greener, negative redder
            Color tint = Color.white;
            if (inst.buff != null)
            {
                float a = inst.buff.accelPerHour;
                tint = a >= 0f ? new Color(0.3f, 1f, 0.4f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
            }

            view.SetData(name, rem, tint);
        }

        for (int i = show; i < _icons.Count; i++)
            _icons[i].SetActive(false);
    }

    private void EnsureIconCount(int needed)
    {
        while (_icons.Count < needed)
        {
            var go = new GameObject("BuffIcon");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _dotSprite;
            sr.sortingOrder = 1100;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var biv = go.AddComponent<BuffIconView>();
            biv.Initialize(_dotSprite);

            _icons.Add(go);
        }
    }

    private static Sprite _cachedDot;
    private static Sprite MakeDotSprite()
    {
        if (_cachedDot != null) return _cachedDot;

        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var c = new Color32(255, 255, 255, 255);
        var clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float dx = (x - 7.5f) / 7.5f;
                float dy = (y - 7.5f) / 7.5f;
                tex.SetPixel(x, y, (dx * dx + dy * dy) <= 1f ? c : clear);
            }

        tex.Apply();
        _cachedDot = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        return _cachedDot;
    }
}
