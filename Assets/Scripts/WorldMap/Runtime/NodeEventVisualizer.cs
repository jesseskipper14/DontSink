using System.Collections.Generic;
using UnityEngine;

public class NodeEventVisualizer : MonoBehaviour
{
    [Header("Layout")]
    public int maxPerRow = 4;
    public int maxShown = 8;               // cap, otherwise dots go brrrr
    public float iconSize = 0.18f;
    public float xSpacing = 0.22f;
    public float ySpacing = 0.22f;
    public Vector2 offset = new Vector2(0f, +0.45f);

    [Header("Refresh")]
    public float refreshIntervalSeconds = 0.5f;

    private float _t;
    private WorldMapEventManager _eventManager;
    private WorldMapGraphGenerator _generator;
    private MapNodeView _nodeView;

    private readonly List<GameObject> _icons = new();
    private Sprite _dotSprite;

    public void Initialize(WorldMapEventManager eventManager, WorldMapGraphGenerator generator, MapNodeView nodeView)
    {
        _eventManager = eventManager;
        _generator = generator;
        _nodeView = nodeView;

        _dotSprite = MakeDotSprite();
    }

    private void Update()
    {
        if (_eventManager == null || _generator?.graph == null || _nodeView == null) return;

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
        if (id < 0 || _generator.graph == null || id >= _generator.graph.nodes.Count) return;

        int count = _eventManager.CountEventsAtNode(id);
        int show = Mathf.Min(count, maxShown);

        EnsureIconCount(show);

        for (int i = 0; i < show; i++)
        {
            var go = _icons[i];
            go.SetActive(true);

            int row = i / maxPerRow;
            int col = i % maxPerRow;

            float x = (col - (maxPerRow - 1) * 0.5f) * xSpacing + offset.x;
            float y = offset.y - row * ySpacing;

            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one * iconSize;

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.35f, 0.55f, 1f, 1f); // blue-ish
        }

        for (int i = show; i < _icons.Count; i++)
            _icons[i].SetActive(false);
    }

    private void EnsureIconCount(int needed)
    {
        while (_icons.Count < needed)
        {
            var go = new GameObject("EventDot");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _dotSprite;
            sr.sortingOrder = 1090; // above node, below buff dots if you want

            // No collider needed: hover tooltip will list events from node hover
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
