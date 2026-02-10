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

    [Header("Line Rendering")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float baseWidth = 0.06f;
    [SerializeField] private float selectedWidth = 0.10f;
    [SerializeField] private float zOffset = -0.5f;

    [Header("Behavior")]
    [SerializeField] private bool showLockedRoutes = true;
    [SerializeField] private bool rebuildOnGraphGenerated = true;

    private readonly List<LineRenderer> _lines = new();

    private int _hoverNodeIndex = -1;
    private readonly HashSet<int> _pinnedAnchors = new();

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
    }

    private void OnDisable()
    {
        if (rebuildOnGraphGenerated && generator != null)
            generator.OnGraphGenerated -= Rebuild;

        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt -= Rebuild;
    }

    private void Start()
    {
        Rebuild();
    }

    [ContextMenu("Rebuild Route Overlay")]
    public void Rebuild()
    {
        ClearLines();

        if (generator?.graph == null) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;
        if (player == null || player.State == null) return;

        // Current node runtime (authoritative)
        if (!runtimeBinder.Registry.TryGetByStableId(player.State.currentNodeId, out var currentRt) || currentRt == null)
            return;

        int currentIndex = currentRt.NodeIndex;

        // Build the set of anchors we should render routes from.
        var anchors = new HashSet<int>();

        // Pinned anchors first
        foreach (var a in _pinnedAnchors)
            anchors.Add(a);

        // Current hover anchor also contributes
        if (_hoverNodeIndex >= 0)
            anchors.Add(_hoverNodeIndex);

        // If nothing pinned/hovered, default to player's current node only
        if (anchors.Count == 0)
            anchors.Add(currentIndex);

        var edges = generator.graph.edges;
        var drawn = new HashSet<ulong>();

        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i];

            ulong k = EdgeKey(e.a, e.b);
            if (!drawn.Add(k))
                continue;

            // Only routes connected to the anchor node
            bool touchesAnchor = anchors.Contains(e.a) || anchors.Contains(e.b);
            if (!touchesAnchor)
                continue;

            if (!runtimeBinder.Registry.TryGetByIndex(e.a, out var aRt)) continue;
            if (!runtimeBinder.Registry.TryGetByIndex(e.b, out var bRt)) continue;

            // Read star-map knowledge state for visualization (does NOT affect travel yet).
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

            // Color rules (debug visualization):
            // Known   = white
            // Partial = yellow-ish
            // Rumored = cyan-ish
            // Unknown = red-ish
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

    private Vector3 WithZ(Vector3 p)
    {
        p.z = zOffset;
        return p;
    }

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

    public void SetHoverNode(int nodeIndex)
    {
        if (_hoverNodeIndex == nodeIndex) return;
        _hoverNodeIndex = nodeIndex;
        Rebuild();
    }

    public void ClearHover()
    {
        if (_hoverNodeIndex == -1) return;
        _hoverNodeIndex = -1;
        Rebuild();
    }

    public void PinAnchor(int nodeIndex)
    {
        if (nodeIndex < 0) return;
        if (_pinnedAnchors.Add(nodeIndex))
            Rebuild();
    }

    public void ClearPinnedAnchors()
    {
        if (_pinnedAnchors.Count == 0) return;
        _pinnedAnchors.Clear();
        Rebuild();
    }

    private static ulong EdgeKey(int a, int b)
    {
        uint x = (uint)Mathf.Min(a, b);
        uint y = (uint)Mathf.Max(a, b);
        return ((ulong)x << 32) | y;
    }
}
