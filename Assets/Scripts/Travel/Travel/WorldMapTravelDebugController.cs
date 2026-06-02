using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldMapTravelDebugController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapPlayerRef player;

    [Header("Travel Rules")]
    [SerializeField] private WorldMapTravelRulesConfig travelRules;
    [SerializeField] private float maxRouteLengthOverride = 999f;
    [SerializeField] private bool useOverrideMaxRouteLength = false;

    [Header("Launch Debug Overrides")]
    [Tooltip("Master switch. Lets NodeTravelController skip boarding, edge, route restriction, and outcome-roll validation.")]
    [SerializeField] private bool bypassAllTravelValidation = false;

    [Tooltip("Allows launch even if PlayerBoardingState is not boarded to the active boat. Boat registry/identity still must resolve.")]
    [SerializeField] private bool bypassBoardingRequirement = false;

    [Tooltip("Allows launch to a locked destination even without a direct graph edge. Usually only for fog/map testing.")]
    [SerializeField] private bool bypassDirectEdgeRequirement = false;

    [Tooltip("Skips max route length and route unlock restrictions.")]
    [SerializeField] private bool bypassRouteRestrictions = false;

    [Tooltip("Skips the old random SimpleTravelResolver pre-launch roll. Recommended ON; travel launch should usually be deterministic.")]
    [SerializeField] private bool bypassOutcomeRoll = true;

    [SerializeField] private bool logTravelDecisions = true;

    [Header("Instant Travel Completion")]
    [Tooltip("When true, instant completion loads NodeScene after moving player state to destination.")]
    [SerializeField] private bool loadNodeSceneAfterInstantComplete = false;

    [SerializeField] private string nodeSceneName = "NodeScene";

    [Header("Legacy Debug Override")]
    [Tooltip("Used by Debug Try Travel only. NodeTravelController uses the explicit launch override booleans above.")]
    [SerializeField] private TravelOverrideMode overrideMode = TravelOverrideMode.None;

    [Header("Debug Travel Input")]
    [SerializeField] private string toNodeId;
    [SerializeField] private float routeLength = 1f;
    [SerializeField] private int seed = 12345;

    private TravelSystem _travelSystem;
    private WorldMapSimContext _ctx;
    private Dictionary<string, MapNodeRuntime> _nodesById;

    public float MaxRouteLength => GetMaxRouteLength();

    public bool BypassAllTravelValidation => bypassAllTravelValidation;
    public bool BypassBoardingRequirement => bypassAllTravelValidation || bypassBoardingRequirement;
    public bool BypassDirectEdgeRequirement => bypassAllTravelValidation || bypassDirectEdgeRequirement;
    public bool BypassRouteRestrictions => bypassAllTravelValidation || bypassRouteRestrictions;
    public bool BypassOutcomeRoll => bypassAllTravelValidation || bypassOutcomeRoll;
    public bool LogTravelDecisions => logTravelDecisions;

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();
    }

    private void OnEnable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt += BuildContext;
    }

    private void OnDisable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt -= BuildContext;
    }

    private void Start()
    {
        RebuildTravelSystem();

        if (runtimeBinder != null && runtimeBinder.IsBuilt)
            BuildContext();
    }

    private void AutoWire()
    {
        if (runtimeBinder == null)
            runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>(FindObjectsInactive.Include);

        if (player == null)
            player = FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);
    }

    private void BuildContext()
    {
        _nodesById = new Dictionary<string, MapNodeRuntime>();

        if (runtimeBinder == null || runtimeBinder.Registry == null)
        {
            _ctx = new WorldMapSimContext(_nodesById);
            RebuildTravelSystem();
            return;
        }

        foreach (var rt in runtimeBinder.Registry.AllRuntimes)
        {
            if (rt == null || string.IsNullOrEmpty(rt.StableId))
                continue;

            _nodesById[rt.StableId] = rt;
        }

        _ctx = new WorldMapSimContext(_nodesById);
        RebuildTravelSystem();

        if (logTravelDecisions)
            Debug.Log($"[TravelDebug] Built context. Nodes={_nodesById.Count}", this);
    }

    private void RebuildTravelSystem()
    {
        float maxLen = GetMaxRouteLength();

        _travelSystem = new TravelSystem(
            new TravelOverrideResolver(overrideMode),
            new RestrictionGateResolver(
                new MaxRouteLengthRestriction(maxLen),
                new RouteUnlockRestriction()
            ),
            new SimpleTravelResolver()
        );
    }

    private float GetMaxRouteLength()
    {
        if (bypassAllTravelValidation || bypassRouteRestrictions)
            return float.PositiveInfinity;

        if (useOverrideMaxRouteLength)
            return maxRouteLengthOverride;

        if (travelRules != null)
            return travelRules.maxRouteLength;

        return maxRouteLengthOverride;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            RebuildTravelSystem();
    }

    [ContextMenu("Debug Instant Complete Travel")]
    public void DebugInstantCompleteTravel()
    {
        GameState gs = GameState.I;

        if (gs == null || gs.player == null)
        {
            Debug.LogError("[TravelDebug] Cannot instant-complete travel: GameState/player missing.", this);
            return;
        }

        string from = gs.player.currentNodeId;
        string to = null;
        string source = null;

        if (gs.activeTravel != null && !string.IsNullOrWhiteSpace(gs.activeTravel.toNodeStableId))
        {
            from = string.IsNullOrWhiteSpace(gs.activeTravel.fromNodeStableId)
                ? from
                : gs.activeTravel.fromNodeStableId;

            to = gs.activeTravel.toNodeStableId;
            source = "activeTravel";
        }
        else if (!string.IsNullOrWhiteSpace(gs.player.lockedDestinationNodeId))
        {
            to = gs.player.lockedDestinationNodeId;
            source = "lockedDestination";
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            Debug.LogWarning(
                "[TravelDebug] Cannot instant-complete travel: no active travel and no locked destination. " +
                "Select a non-current node and lock it first. Ignoring Debug To Node Id so stale manual IDs cannot corrupt currentNodeId.",
                this
            );
            return;
        }

        if (!CanResolveNodeId(to))
        {
            Debug.LogWarning(
                $"[TravelDebug] Cannot instant-complete travel: destination '{to}' from {source} does not resolve in the current graph/runtime. " +
                "Refusing to write it into player.currentNodeId.",
                this
            );
            return;
        }

        if (string.Equals(from, to, System.StringComparison.Ordinal))
        {
            // No-op. This matters when the selected/current node somehow gets locked.
            gs.player.lockedSourceNodeId = null;
            gs.player.lockedDestinationNodeId = null;

            if (gs.activeTravel != null)
                gs.ClearTravel();

            Debug.Log($"[TravelDebug] Instant travel ignored: destination is already current node '{to}'.", this);

            WorldMapKnowledgeSource sameNodeKnowledge =
                FindAnyObjectByType<WorldMapKnowledgeSource>(FindObjectsInactive.Include);

            if (sameNodeKnowledge != null)
                sameNodeKnowledge.RevealSurfaceAroundCurrentNode();

            return;
        }

        gs.player.currentNodeId = to;
        gs.player.lockedSourceNodeId = null;
        gs.player.lockedDestinationNodeId = null;

        if (gs.activeTravel != null)
            gs.ClearTravel();

        WorldMapKnowledgeSource knowledge =
            FindAnyObjectByType<WorldMapKnowledgeSource>(FindObjectsInactive.Include);

        if (knowledge != null)
        {
            knowledge.RevealSurfaceAlongRouteByNodeIds(from, to);
            knowledge.RevealSurfaceAroundCurrentNode();
            knowledge.RevealTravelDestinationNow();
        }

        Debug.Log($"[TravelDebug] Instant-completed travel: {from} -> {to} ({source})", this);

        if (loadNodeSceneAfterInstantComplete && !string.IsNullOrWhiteSpace(nodeSceneName))
            SceneManager.LoadScene(nodeSceneName);
    }

    private bool CanResolveNodeId(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return false;

        AutoWire();

        if (runtimeBinder != null &&
            runtimeBinder.IsBuilt &&
            runtimeBinder.Registry != null &&
            runtimeBinder.Registry.TryGetByStableId(stableId, out MapNodeRuntime rt) &&
            rt != null)
        {
            return true;
        }

        WorldMapGraphGenerator generator =
            FindAnyObjectByType<WorldMapGraphGenerator>(FindObjectsInactive.Include);

        MapGraph graph = generator != null ? generator.graph : null;
        if (graph == null || graph.nodes == null)
            return false;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            string nodeStableId = WorldMapStableIdUtility.BuildNodeStableId(graph.seed, graph.nodes[i]);
            if (nodeStableId == stableId)
                return true;
        }

        return false;
    }

    [ContextMenu("Debug Try Travel")]
    public void DebugTryTravel()
    {
        AutoWire();

        if (runtimeBinder != null && runtimeBinder.IsBuilt && _ctx == null)
            BuildContext();

        if (player == null || player.State == null)
        {
            Debug.LogError("[TravelDebug] Missing player/state.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(toNodeId))
        {
            Debug.LogError("[TravelDebug] toNodeId is empty.", this);
            return;
        }

        string from = player.State.currentNodeId;
        var req = new TravelRequest(from, toNodeId, routeLength, seed);

        if (bypassAllTravelValidation)
        {
            player.State.currentNodeId = toNodeId;
            Debug.Log($"[TravelDebug] FORCE travel OK: {from} -> {toNodeId}", this);
            return;
        }

        if (_travelSystem == null)
            RebuildTravelSystem();

        var result = _travelSystem.TryTravel(req, _ctx, player.State);

        if (result.success)
        {
            Debug.Log($"[TravelDebug] Travel OK: {from} -> {toNodeId} (roll={result.roll})", this);
        }
        else
        {
            Debug.LogWarning($"[TravelDebug] Travel FAILED: {from} -> {toNodeId} reason='{result.failureReason}' roll={result.roll}", this);
        }
    }

    [ContextMenu("Debug Unlock Route (From current -> To)")]
    public void DebugUnlockRoute()
    {
        if (player == null || player.State == null)
        {
            Debug.LogError("TravelDebug: missing player/state", this);
            return;
        }

        if (string.IsNullOrEmpty(toNodeId))
        {
            Debug.LogError("TravelDebug: toNodeId is empty.", this);
            return;
        }

        string from = player.State.currentNodeId;
        string key = RouteKey.Make(from, toNodeId);
        player.State.unlockedRoutes.Add(key);

        Debug.Log($"Unlocked route: {key}", this);
    }

    [ContextMenu("Debug Lock Route (From current -> To)")]
    public void DebugLockRoute()
    {
        if (player == null || player.State == null)
        {
            Debug.LogError("TravelDebug: missing player/state", this);
            return;
        }

        if (string.IsNullOrEmpty(toNodeId))
        {
            Debug.LogError("TravelDebug: toNodeId is empty.", this);
            return;
        }

        string from = player.State.currentNodeId;
        string key = RouteKey.Make(from, toNodeId);
        player.State.unlockedRoutes.Remove(key);

        Debug.Log($"Locked route: {key}", this);
    }

    [ContextMenu("Debug Enable Full Travel Bypass")]
    private void DebugEnableFullTravelBypass()
    {
        bypassAllTravelValidation = true;
        bypassOutcomeRoll = true;
        useOverrideMaxRouteLength = true;
        maxRouteLengthOverride = 999999f;

        Debug.Log("[TravelDebug] Full travel bypass enabled.", this);
    }

    [ContextMenu("Debug Disable Full Travel Bypass")]
    private void DebugDisableFullTravelBypass()
    {
        bypassAllTravelValidation = false;

        Debug.Log("[TravelDebug] Full travel bypass disabled.", this);
    }
}
