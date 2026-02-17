using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.StarMap;

public sealed class WorldMapRouteOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapPlayer player;
    [SerializeField] private WorldMapNodeSelection selection;

    [Header("Behavior")]
    [SerializeField] private bool showLockedRoutes = true;
    [SerializeField] private bool rebuildOnGraphGenerated = true;

    [Header("UI Rendering (preferred for overlay)")]
    [Tooltip("If assigned, routes are drawn in UI space using UIEdgeGraphic. If null, world-space LineRenderer mode is used.")]
    [SerializeField] private UIEdgeGraphic uiEdges;

    [Tooltip("Map panel rect used for scaling node positions.")]
    [SerializeField] private RectTransform uiMapPanel;

    [Tooltip("Same container that node buttons are spawned under (determines anchoredPosition space).")]
    [SerializeField] private RectTransform uiNodeContainer;

    [Min(1f)]
    [SerializeField] private float uiThicknessPx = 3f;

    [Header("World Rendering (fallback)")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float baseWidth = 0.06f;
    [SerializeField] private float zOffset = -0.5f;

    private readonly List<LineRenderer> _lines = new();

    // In WorldMapRouteOverlay
    [SerializeField] private WorldMapHoverController hoverController; // assign in inspector
    private WorldMapHoverState HoverState => hoverController != null ? hoverController.State : null;


    private void Reset()
    {
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        player = FindAnyObjectByType<WorldMapPlayer>();
        selection = FindAnyObjectByType<WorldMapNodeSelection>();
    }

    private void OnEnable()
    {
        if (rebuildOnGraphGenerated && generator != null)
            generator.OnGraphGenerated += Rebuild;

        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt += Rebuild;

        if (hoverController != null)
            hoverController.HoverChanged += Rebuild;

    }

    private void OnDisable()
    {
        if (rebuildOnGraphGenerated && generator != null)
            generator.OnGraphGenerated -= Rebuild;

        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt -= Rebuild;

        if (hoverController != null)
            hoverController.HoverChanged -= Rebuild;
    }

    private void Start()
    {
        Rebuild();
    }

    [ContextMenu("Rebuild Route Overlay")]
    public void Rebuild()
    {
        // Always clear world lines; UI mode doesn't use them.
        ClearLines();

        if (generator?.graph == null) { ClearUI(); return; }
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) { ClearUI(); return; }
        if (player == null || player.State == null) { ClearUI(); return; }

        // Current node runtime (authoritative)
        if (!runtimeBinder.Registry.TryGetByStableId(player.State.currentNodeId, out var currentRt) || currentRt == null)
        {
            ClearUI();
            return;
        }

        // Build the set of anchors we should render routes from.
        var state = HoverState;
        if (state == null) { ClearUI(); return; }

        var anchors = new HashSet<int>();
        foreach (var a in state.Pinned) anchors.Add(a);
        if (state.HoveredNodeIndex >= 0) anchors.Add(state.HoveredNodeIndex);

        if (anchors.Count == 0) { ClearUI(); return; }


        // UI mode
        if (uiEdges != null)
        {
            RebuildUI(anchors);
            return;
        }

        // Fallback: world-space mode (debug)
        RebuildWorld(anchors);
    }

    // =========================
    // UI MODE
    // =========================

    private void RebuildUI(HashSet<int> anchors)
    {
        if (uiEdges == null || uiMapPanel == null || uiNodeContainer == null)
        {
            ClearUI();
            return;
        }

        var g = generator.graph;

        // Compute bounds in graph-space (same approach as your overlay rebuild)
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

        // Build segments in the SAME anchoredPosition space as node buttons
        var A = new List<Vector2>(256);
        var B = new List<Vector2>(256);
        var C = new List<Color32>(256);

        var edges = g.edges;
        var drawn = new HashSet<ulong>();

        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i];

            ulong k = EdgeKey(e.a, e.b);
            if (!drawn.Add(k)) continue;

            bool touchesAnchor = anchors.Contains(e.a) || anchors.Contains(e.b);
            if (!touchesAnchor) continue;

            if (!runtimeBinder.Registry.TryGetByIndex(e.a, out var aRt)) continue;
            if (!runtimeBinder.Registry.TryGetByIndex(e.b, out var bRt)) continue;

            var kState = StarMapVisualQuery.GetVisualState(
                player.State,
                aRt.StableId, aRt.ClusterId,
                bRt.StableId, bRt.ClusterId);

            Color32 c = kState switch
            {
                RouteKnowledgeState.Known => (Color32)Color.white,
                RouteKnowledgeState.Partial => (Color32)new Color(1f, 0.9f, 0.25f, 0.95f),
                RouteKnowledgeState.Rumored => (Color32)new Color(0.35f, 0.95f, 1f, 0.75f),
                _ => (Color32)new Color(1f, 0.25f, 0.25f, 0.85f)
            };

            if (!showLockedRoutes && kState != RouteKnowledgeState.Known)
                continue;

            // Convert graph positions -> panel positions (anchoredPosition)
            Vector2 pa = GraphToPanel(g.nodes[e.a].position, min, max, uiMapPanel);
            Vector2 pb = GraphToPanel(g.nodes[e.b].position, min, max, uiMapPanel);

            A.Add(pa);
            B.Add(pb);
            C.Add(c);
        }

        uiEdges.lineThickness = uiThicknessPx;
        uiEdges.SetSegments(A, B, C);
    }

    private static Vector2 GraphToPanel(Vector2 graphPos, Vector2 min, Vector2 max, RectTransform mapPanel)
    {
        Vector2 size = max - min;
        float nx = (graphPos.x - min.x) / (size.x <= 0.0001f ? 1f : size.x);
        float ny = (graphPos.y - min.y) / (size.y <= 0.0001f ? 1f : size.y);

        float x = (nx - 0.5f) * mapPanel.rect.width;
        float y = (ny - 0.5f) * mapPanel.rect.height;
        return new Vector2(x, y);
    }

    private void ClearUI()
    {
        if (uiEdges != null)
            uiEdges.SetSegments(_empty, _empty);
    }

    private static readonly List<Vector2> _empty = new List<Vector2>(0);

    // =========================
    // WORLD MODE (fallback)
    // =========================

    private void RebuildWorld(HashSet<int> anchors)
    {
        var g = generator.graph;
        var edges = g.edges;
        var drawn = new HashSet<ulong>();

        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i];

            ulong k = EdgeKey(e.a, e.b);
            if (!drawn.Add(k))
                continue;

            bool touchesAnchor = anchors.Contains(e.a) || anchors.Contains(e.b);
            if (!touchesAnchor)
                continue;

            if (!runtimeBinder.Registry.TryGetByIndex(e.a, out var aRt)) continue;
            if (!runtimeBinder.Registry.TryGetByIndex(e.b, out var bRt)) continue;

            var kState = StarMapVisualQuery.GetVisualState(
                player.State,
                aRt.StableId, aRt.ClusterId,
                bRt.StableId, bRt.ClusterId);

            if (!showLockedRoutes && kState != RouteKnowledgeState.Known)
                continue;

            var lr = CreateLine($"Route_{e.a}_{e.b}");

            lr.positionCount = 2;
            lr.SetPosition(0, WithZ(aRt.transform.position));
            lr.SetPosition(1, WithZ(bRt.transform.position));

            lr.widthMultiplier = baseWidth;

            Color c = kState switch
            {
                RouteKnowledgeState.Known => Color.white,
                RouteKnowledgeState.Partial => new Color(1f, 0.9f, 0.25f, 0.95f),
                RouteKnowledgeState.Rumored => new Color(0.35f, 0.95f, 1f, 0.75f),
                _ => new Color(1f, 0.25f, 0.25f, 0.85f)
            };

            lr.startColor = c;
            lr.endColor = c;

            _lines.Add(lr);
        }
    }

    private Vector3 WithZ(Vector3 p) { p.z = zOffset; return p; }

    private LineRenderer CreateLine(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, worldPositionStays: true);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;

        return lr;
    }

    private void ClearLines()
    {
        for (int i = 0; i < _lines.Count; i++)
            if (_lines[i] != null)
                Destroy(_lines[i].gameObject);

        _lines.Clear();
    }

    private static ulong EdgeKey(int a, int b)
    {
        uint x = (uint)Mathf.Min(a, b);
        uint y = (uint)Mathf.Max(a, b);
        return ((ulong)x << 32) | y;
    }
}
