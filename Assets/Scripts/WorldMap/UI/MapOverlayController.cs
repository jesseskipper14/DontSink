using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class MapOverlayController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapGraphGenerator generator;

    [Header("UI Root")]
    [SerializeField] private CanvasGroup overlayRoot;     // MapOverlayRoot (CanvasGroup)
    [SerializeField] private RectTransform mapPanel;      // MapPanel (RectTransform)
    [SerializeField] private RectTransform nodeContainer; // NodeContainer (RectTransform)
    [SerializeField] private UIEdgeGraphic edgeGraphic;   // UIEdgeGraphic on a child of MapPanel
    [SerializeField] private WorldMapHeatmapController heatmap;   // UIEdgeGraphic on a child of MapPanel

    [Header("Node Prefab")]
    [SerializeField] private Button nodeButtonPrefab;     // Simple UI Button (Image + Button)
    [Min(4f)] public float nodeSize = 14f;

    [Header("Optional HUD")]
    [SerializeField] private TMPro.TMP_Text selectedLabel;
    [SerializeField] private Button lockButton;

    [Header("Debug / State")]
    [SerializeField] private int currentNodeId = 0;
    [SerializeField] private int selectedNodeId = -1;

    [Range(0.005f, 0.1f)]
    public float nodeSizePercent = 0.025f; // 2.5%

    private readonly List<Button> _spawned = new List<Button>(128);
    private readonly List<Vector2> _edgeA = new List<Vector2>(256);
    private readonly List<Vector2> _edgeB = new List<Vector2>(256);

    private bool _visible;
    [SerializeField] private bool drawAllEdges = false;

    public bool IsVisible => _visible;

    private void Awake()
    {
        SetVisible(false);

        if (lockButton != null)
            lockButton.onClick.AddListener(OnLockClicked);
    }

    private void OnEnable()
    {
        if (generator != null)
            generator.OnGraphGenerated += Rebuild;
    }

    private void OnDisable()
    {
        if (generator != null)
            generator.OnGraphGenerated -= Rebuild;
    }

    private void Start()
    {
        if (generator != null && generator.graph != null)
            Rebuild();
    }

    private void Update()
    {
        // M toggles map overlay
        //if (Input.GetKeyDown(KeyCode.M)) - controlled by MapTableInteractable now
        //    Toggle();
    }

    public void Toggle() => SetVisible(!_visible);

    public void SetVisible(bool visible)
    {
        _visible = visible;

        if (overlayRoot == null) return;

        overlayRoot.alpha = visible ? 1f : 0f;
        overlayRoot.blocksRaycasts = visible;
        overlayRoot.interactable = visible;
    }

    [ContextMenu("Rebuild Overlay")]
    public void Rebuild()
    {
        // Validate first. Do NOT clear if we can’t rebuild.
        if (generator == null) return;
        var g = generator.graph;
        if (g == null || g.nodes == null || g.nodes.Count == 0) return;

        if (mapPanel == null || nodeContainer == null || nodeButtonPrefab == null)
            return;

        // Now it's safe to clear and rebuild.
        ClearNodes();

        // Compute bounds in graph space
        Vector2 min = g.nodes[0].position;
        Vector2 max = g.nodes[0].position;

        for (int i = 1; i < g.nodes.Count; i++)
        {
            var p = g.nodes[i].position;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        Vector2 size = (max - min);
        if (size.x < 0.001f) size.x = 1f;
        if (size.y < 0.001f) size.y = 1f;

        Vector2 pad = size * 0.08f;
        min -= pad;
        max += pad;

        // Build edges
        // Build edges
        if (drawAllEdges)
            BuildEdges(min, max);
        else
            ClearEdges(); // new helper, see below

        // Spawn node buttons
        for (int i = 0; i < g.nodes.Count; i++)
        {
            int nodeIndex = i;
            int nodeId = g.nodes[i].id;
            Vector2 panelPos = GraphToPanel(g.nodes[i].position, min, max);

            var btn = Instantiate(nodeButtonPrefab, nodeContainer);
            btn.name = $"NodeBtn_{nodeId}";

            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                bool isPrimary = g.nodes[nodeIndex].isPrimary; // <-- adjust field name to yours
                img.color = isPrimary ? Color.yellow : Color.white;
            }

            var hover = btn.GetComponent<MapNodeHoverTarget>();
            if (hover == null) hover = btn.gameObject.AddComponent<MapNodeHoverTarget>();
            hover.NodeId = nodeIndex;

            var rt = (RectTransform)btn.transform;
            rt.anchoredPosition = panelPos;

            float minDim = Mathf.Min(mapPanel.rect.width, mapPanel.rect.height);
            float scaledSize = minDim * nodeSizePercent;

            rt.sizeDelta = new Vector2(scaledSize, scaledSize);

            btn.onClick.AddListener(() => OnNodeClicked(nodeIndex));
            _spawned.Add(btn);
        }

        // Default selection
        if (selectedNodeId < 0 && g.nodes.Count > 0)
            Select(g.nodes[0].id);
        else
            RefreshHUD();
    }

    private void BuildEdges(Vector2 min, Vector2 max)
    {
        _edgeA.Clear();
        _edgeB.Clear();

        var g = generator.graph;
        if (g.edges == null || g.edges.Count == 0)
        {
            if (edgeGraphic != null) edgeGraphic.SetSegments(_edgeA, _edgeB);
            return;
        }

        for (int i = 0; i < g.edges.Count; i++)
        {
            var e = g.edges[i];
            if (e.a < 0 || e.a >= g.nodes.Count) continue;
            if (e.b < 0 || e.b >= g.nodes.Count) continue;

            Vector2 a = GraphToPanel(g.nodes[e.a].position, min, max);
            Vector2 b = GraphToPanel(g.nodes[e.b].position, min, max);

            _edgeA.Add(a);
            _edgeB.Add(b);
        }

        if (edgeGraphic != null)
            edgeGraphic.SetSegments(_edgeA, _edgeB);
    }

    private Vector2 GraphToPanel(Vector2 p, Vector2 min, Vector2 max)
    {
        // Normalize into [0..1]
        float u = Mathf.InverseLerp(min.x, max.x, p.x);
        float v = Mathf.InverseLerp(min.y, max.y, p.y);

        // Map to panel rect
        var r = mapPanel.rect;
        float x = Mathf.Lerp(r.xMin, r.xMax, u);
        float y = Mathf.Lerp(r.yMin, r.yMax, v);
        return new Vector2(x, y);
    }

    private void OnNodeClicked(int nodeId) => Select(nodeId);

    private void Select(int nodeId)
    {
        selectedNodeId = nodeId;
        RefreshHUD();
    }

    private void RefreshHUD()
    {
        if (generator == null || generator.graph == null) return;

        var g = generator.graph;
        string selName = (selectedNodeId >= 0 && selectedNodeId < g.nodes.Count)
            ? g.nodes[selectedNodeId].displayName
            : "(none)";

        if (selectedLabel != null)
            selectedLabel.text = $"Selected: {selName} (#{selectedNodeId})";

        if (lockButton != null)
            lockButton.interactable = IsValidTravel(currentNodeId, selectedNodeId);
    }

    private bool IsValidTravel(int fromNodeId, int toNodeId)
    {
        if (generator == null || generator.graph == null) return false;
        if (fromNodeId < 0 || toNodeId < 0) return false;
        if (fromNodeId == toNodeId) return false;

        // NOTE: this uses graph adjacency. Swap later for your authoritative validator.
        return generator.graph.HasEdge(fromNodeId, toNodeId);
    }

    private void OnLockClicked()
    {
        // Placeholder: this is where you'll set TravelIntent in GameSession later.
        // For now, just treat it as "locked" if valid.
        if (!IsValidTravel(currentNodeId, selectedNodeId))
            return;

        // You can wire this to your UI: "Destination locked: X"
        // Or set a field for later.
    }

    private void ClearNodes()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null)
                Destroy(_spawned[i].gameObject);

        _spawned.Clear();

        _edgeA.Clear();
        _edgeB.Clear();
        if (edgeGraphic != null) edgeGraphic.SetSegments(_edgeA, _edgeB);
    }

    private void ClearEdges()
    {
        // Whatever you use to draw edges, clear it here.
        // If BuildEdges uses a UIEdgeGraphic, clear its segments.
        if (edgeGraphic != null)
            edgeGraphic.SetSegments(_empty, _empty);
    }

    private static readonly List<Vector2> _empty = new List<Vector2>(0);

}
