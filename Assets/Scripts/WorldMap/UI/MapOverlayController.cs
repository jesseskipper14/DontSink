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
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapPlayerRef player;
    [SerializeField] private WorldMapTravelRulesConfig travelRules;
    [SerializeField] private WorldMapTravelDebugController travelDebug; // if you want its max len logic

    public string lockedDestinationNodeId; // stableId
    public string lockedSourceNodeId;      // stableId (optional but useful)

    [Header("Node Prefab")]
    [SerializeField] private Button nodeButtonPrefab;     // Simple UI Button (Image + Button)
    [Min(4f)] public float nodeSize = 14f;

    [Header("Optional HUD")]
    [SerializeField] private TMPro.TMP_Text selectedLabel;
    [SerializeField] private Button lockButton;
    [SerializeField] private Button travelButton;
    [SerializeField] private NodeTravelLauncher travelLauncher; // optional convenience

    [Header("Debug / State")]
    [SerializeField] private int selectedNodeIndex = -1;

    [SerializeField] private int lockedNodeIndex = -1;
    [SerializeField] private string lockedStableId;


    [Range(0.005f, 0.1f)]
    public float nodeSizePercent = 0.025f; // 2.5%

    private readonly List<Button> _spawned = new List<Button>(128);
    private readonly List<Vector2> _edgeA = new List<Vector2>(256);
    private readonly List<Vector2> _edgeB = new List<Vector2>(256);

    private bool _visible;
    [SerializeField] private bool drawAllEdges = false;

    public bool IsVisible => _visible;

    private static readonly List<Vector2> _empty = new List<Vector2>(0);
    public bool HasLockedDestination => !string.IsNullOrEmpty(lockedStableId);
    public string LockedDestinationStableId => lockedStableId;
    public int LockedDestinationIndex => lockedNodeIndex;
    public int SelectedNodeIndex => selectedNodeIndex;


    private void Awake()
    {
        SetVisible(false);

        if (lockButton != null)
            lockButton.onClick.AddListener(OnLockClicked);

        // ✅ Auto-heal refs in play mode
        if (player == null)
            player = FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);

        if (travelButton != null)
            travelButton.onClick.AddListener(OnTravelClicked);
    }

    private void OnEnable()
    {
        if (generator != null)
            generator.OnGraphGenerated += Rebuild;
        if (player == null)
            player = FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);
        AutoWireFromSceneContext();
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
        if (player == null)
            player = FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);
        AutoWireFromSceneContext();
    }

    private bool _lastCanTravel;

    private void Update()
    {
        if (!_visible) return;

        bool now = CanTravel();
        if (now != _lastCanTravel)
        {
            _lastCanTravel = now;
            if (travelButton != null)
                travelButton.interactable = now;
        }
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
        if (!drawAllEdges)
        {
            if (HasLockedDestination)
                BuildLockedRouteEdge(min, max);
            else
                ClearEdges();
        }

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
        if (selectedNodeIndex < 0 && g.nodes.Count > 0)
            SelectIndex(0);
        else
        {
            SyncLockFromPlayerState();
            RefreshHUD();
        }
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

    private void OnNodeClicked(int nodeIndex)
    {
        Debug.Log($"Node clicked: {nodeIndex}");
        SelectIndex(nodeIndex);
    }

    private void SelectIndex(int nodeIndex)
    {
        selectedNodeIndex = nodeIndex;
        RefreshHUD();
    }

    //private void Select(int nodeId)
    //{
    //    selectedNodeId = nodeId;
    //    RefreshHUD();
    //}

    private void RefreshHUD()
    {
        if (generator == null || generator.graph == null) return;
        var g = generator.graph;

        string selName = (selectedNodeIndex >= 0 && selectedNodeIndex < g.nodes.Count)
            ? g.nodes[selectedNodeIndex].displayName
            : "(none)";

        if (selectedLabel != null)
            selectedLabel.text = $"Selected: {selName} (#{selectedNodeIndex})";

        if (lockButton != null)
            lockButton.interactable = CanLockSelected();
        if (travelButton != null)
            travelButton.interactable = CanTravel();
    }

    private bool CanLockSelected()
    {
        if (runtimeBinder == null) { Debug.Log("CanLock: runtimeBinder null"); return false; }
        if (!runtimeBinder.IsBuilt) { Debug.Log("CanLock: runtime not built"); return false; }
        if (player == null || player.State == null) { Debug.Log("CanLock: player/state null"); return false; }
        if (generator == null || generator.graph == null) { Debug.Log("CanLock: generator/graph null"); return false; }
        if (selectedNodeIndex < 0) { Debug.Log("CanLock: selectedNodeIndex < 0"); return false; }

        if (string.IsNullOrEmpty(player.State.currentNodeId))
        {
            Debug.Log("CanLock: player.State.currentNodeId is empty");
            return false;
        }

        if (!runtimeBinder.Registry.TryGetByStableId(player.State.currentNodeId, out var currentRt) || currentRt == null)
        {
            Debug.Log($"CanLock: currentNodeId not resolved: '{player.State.currentNodeId}'");
            return false;
        }

        int fromIndex = currentRt.NodeIndex;
        int toIndex = selectedNodeIndex;

        if (fromIndex == toIndex) { Debug.Log("CanLock: fromIndex == toIndex"); return false; }

        if (!generator.graph.HasEdge(fromIndex, toIndex))
        {
            Debug.Log($"CanLock: no edge between {fromIndex} and {toIndex}");
            return false;
        }

        // If you added route policy gating earlier, log that too:
        // (comment this block out temporarily if you want adjacency-only)
        /*
        if (!runtimeBinder.Registry.TryGetByIndex(toIndex, out var toRt) || toRt == null)
        {
            Debug.Log($"CanLock: toIndex runtime not found: {toIndex}");
            return false;
        }

        float routeLen = Vector2.Distance(generator.graph.nodes[fromIndex].position, generator.graph.nodes[toIndex].position);
        float maxLen = travelDebug != null ? travelDebug.MaxRouteLength :
                       travelRules != null ? travelRules.maxRouteLength : float.NaN;

        if (RouteAccessPolicy.TryGetBlockReason(
                player.State,
                currentRt.StableId, toRt.StableId,
                currentRt.ClusterId, toRt.ClusterId,
                routeLen, maxLen,
                out var reason))
        {
            Debug.Log($"CanLock: blocked by policy: {reason}");
            return false;
        }
        */

        return true;
    }

    private bool CanTravel()
    {
        // Must have a locked destination
        if (string.IsNullOrEmpty(lockedStableId))
            return false;

        // Must have GameState + player boat + boarding state
        var gs = GameState.I;
        if (gs == null) return false;

        // Resolve the player boat by id
        var reg = gs.boatRegistry;
        if (reg == null) return false;

        var boatId = gs.boat?.boatInstanceId;
        if (string.IsNullOrEmpty(boatId)) return false;

        if (!reg.TryGetById(boatId, out var boat) || boat == null)
            return false;

        // Find boarding state (wherever you store it)
        // Option A: PlayerBoardingState lives on the player object
        var boarding = FindAnyObjectByType<PlayerBoardingState>();
        if (boarding == null) return false;

        // Must be boarded, and boarded to THIS boat
        if (!boarding.IsBoarded) return false;
        if (boarding.CurrentBoatRoot != boat.transform) return false;

        return true;
    }

    //private bool IsValidTravel(int fromNodeId, int toNodeId)
    //{
    //    if (generator == null || generator.graph == null) return false;
    //    if (fromNodeId < 0 || toNodeId < 0) return false;
    //    if (fromNodeId == toNodeId) return false;

    //    // NOTE: this uses graph adjacency. Swap later for your authoritative validator.
    //    return generator.graph.HasEdge(fromNodeId, toNodeId);
    //}

    private void OnLockClicked()
    {
        if (!CanLockSelected())
            return;

        lockedNodeIndex = selectedNodeIndex;

        if (runtimeBinder != null && runtimeBinder.IsBuilt &&
            runtimeBinder.Registry.TryGetByIndex(lockedNodeIndex, out var rt) && rt != null)
        {
            lockedStableId = rt.StableId;

            // ✅ Authoritative commit
            player.State.lockedSourceNodeId = player.State.currentNodeId;
            player.State.lockedDestinationNodeId = lockedStableId;
        }
        else
        {
            lockedStableId = null;

            // ✅ Clear authoritative lock if we failed
            player.State.lockedDestinationNodeId = null;
            player.State.lockedSourceNodeId = null;
        }

        RefreshHUD();
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

    private void SyncLockFromPlayerState()
    {
        if (player == null || player.State == null)
            return;

        lockedStableId = player.State.lockedDestinationNodeId;

        if (string.IsNullOrEmpty(lockedStableId))
        {
            lockedNodeIndex = -1;
            return;
        }

        if (runtimeBinder != null && runtimeBinder.IsBuilt &&
            runtimeBinder.Registry.TryGetByStableId(lockedStableId, out var rt) && rt != null)
        {
            lockedNodeIndex = rt.NodeIndex;
        }
        else
        {
            lockedNodeIndex = -1;
        }
    }

    //private float GetRouteLengthGraphSpace(int fromIndex, int toIndex)
    //{
    //    var g = generator.graph;
    //    if (g == null) return float.PositiveInfinity;
    //    if (fromIndex < 0 || toIndex < 0) return float.PositiveInfinity;
    //    if (fromIndex >= g.nodes.Count || toIndex >= g.nodes.Count) return float.PositiveInfinity;

    //    var a = g.nodes[fromIndex].position;
    //    var b = g.nodes[toIndex].position;
    //    return Vector2.Distance(a, b);
    //}

    private void BuildLockedRouteEdge(Vector2 min, Vector2 max)
    {
        _edgeA.Clear();
        _edgeB.Clear();

        if (generator == null)
        {
            Debug.LogWarning("MapOverlay: generator is null in BuildLockedRouteEdge()");
            ClearEdges();
            return;
        }

        if (generator.graph == null)
        {
            Debug.LogWarning("MapOverlay: generator.graph is null in BuildLockedRouteEdge()");
            ClearEdges();
            return;
        }

        if (runtimeBinder == null)
        {
            Debug.LogWarning("MapOverlay: runtimeBinder is null in BuildLockedRouteEdge()");
            ClearEdges();
            return;
        }

        if (!runtimeBinder.IsBuilt)
        {
            Debug.Log("MapOverlay: runtimeBinder not built yet (skipping locked edge draw)");
            ClearEdges();
            return;
        }

        if (player == null)
        {
            Debug.LogWarning("MapOverlay: player reference is null in BuildLockedRouteEdge()");
            ClearEdges();
            return;
        }

        if (player.State == null)
        {
            Debug.LogWarning("MapOverlay: player.State is null in BuildLockedRouteEdge()");
            ClearEdges();
            return;
        }

        var lockedId = player.State.lockedDestinationNodeId;

        if (string.IsNullOrEmpty(lockedId))
        {
            Debug.Log("MapOverlay: no lockedDestinationNodeId set (nothing to draw)");
            ClearEdges();
            return;
        }

        if (string.IsNullOrEmpty(player.State.currentNodeId))
        {
            Debug.LogWarning("MapOverlay: currentNodeId is null or empty");
            ClearEdges();
            return;
        }

        if (!runtimeBinder.Registry.TryGetByStableId(player.State.currentNodeId, out var fromRt) || fromRt == null)
        {
            Debug.LogWarning($"MapOverlay: could not resolve currentNodeId '{player.State.currentNodeId}' to runtime");
            ClearEdges();
            return;
        }

        if (!runtimeBinder.Registry.TryGetByStableId(lockedId, out var toRt) || toRt == null)
        {
            Debug.LogWarning($"MapOverlay: could not resolve lockedDestinationNodeId '{lockedId}' to runtime");
            ClearEdges();
            return;
        }

        int aIdx = fromRt.NodeIndex;
        int bIdx = toRt.NodeIndex;

        if (!generator.graph.HasEdge(aIdx, bIdx))
        {
            Debug.Log($"MapOverlay: no edge between {aIdx} and {bIdx}");
            ClearEdges();
            return;
        }

        var g = generator.graph;

        if (aIdx < 0 || aIdx >= g.nodes.Count || bIdx < 0 || bIdx >= g.nodes.Count)
        {
            Debug.LogWarning($"MapOverlay: invalid node indices a={aIdx}, b={bIdx}");
            ClearEdges();
            return;
        }

        Vector2 a = GraphToPanel(g.nodes[aIdx].position, min, max);
        Vector2 b = GraphToPanel(g.nodes[bIdx].position, min, max);

        _edgeA.Add(a);
        _edgeB.Add(b);

        if (edgeGraphic == null)
        {
            Debug.LogWarning("MapOverlay: edgeGraphic is null");
            return;
        }

        edgeGraphic.SetSegments(_edgeA, _edgeB);

        Debug.Log($"MapOverlay: drawing locked route {fromRt.StableId} -> {toRt.StableId}");
    }

    //private bool TryGetCurrentAndSelected(out MapNodeRuntime fromRt, out MapNodeRuntime toRt)
    //{
    //    fromRt = null;
    //    toRt = null;

    //    if (runtimeBinder == null || !runtimeBinder.IsBuilt) return false;
    //    if (player == null || player.State == null) return false;
    //    if (generator == null || generator.graph == null) return false;

    //    if (selectedNodeIndex < 0) return false;
    //    if (string.IsNullOrEmpty(player.State.currentNodeId)) return false;

    //    if (!runtimeBinder.Registry.TryGetByStableId(player.State.currentNodeId, out fromRt) || fromRt == null)
    //        return false;

    //    if (!runtimeBinder.Registry.TryGetByIndex(selectedNodeIndex, out toRt) || toRt == null)
    //        return false;

    //    return true;
    //}

    //private void DrawSingleRoute(Vector2 min, Vector2 max, int aIdx, int bIdx)
    //{
    //    _edgeA.Clear();
    //    _edgeB.Clear();

    //    var g = generator.graph;
    //    if (g == null) { ClearEdges(); return; }

    //    if (!g.HasEdge(aIdx, bIdx))
    //    {
    //        ClearEdges();
    //        return;
    //    }

    //    Vector2 a = GraphToPanel(g.nodes[aIdx].position, min, max);
    //    Vector2 b = GraphToPanel(g.nodes[bIdx].position, min, max);

    //    _edgeA.Add(a);
    //    _edgeB.Add(b);

    //    if (edgeGraphic != null)
    //        edgeGraphic.SetSegments(_edgeA, _edgeB);
    //}

    private void OnTravelClicked()
    {
        if (travelLauncher == null)
            travelLauncher = FindAnyObjectByType<NodeTravelLauncher>();

        if (travelLauncher != null)
            travelLauncher.TryStartTravel();
    }

    private void AutoWireFromSceneContext()
    {
        var ctx = SceneContext.Current;
        if (ctx == null) return;

        if (generator == null) generator = ctx.mapGenerator;
        if (heatmap == null) heatmap = ctx.heatmap;
        if (runtimeBinder == null) runtimeBinder = ctx.runtimeBinder;
        if (travelDebug == null) travelDebug = ctx.travelDebug;
        // travelLauncher too
    }
}
