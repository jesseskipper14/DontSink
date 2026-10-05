using System.Collections.Generic;
using UnityEngine;

// FLAGGED FOR FIELD/METHOD CLEANUP

public sealed class NodeTravelController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapTravelDebugController travelDebug; // optional
    [SerializeField] private WorldMapTravelRulesConfig travelRules;     // optional
    [SerializeField] private PlayerLoadoutPersistence playerLoadoutPersistence;

    [Header("Travel Validation")]
    [Tooltip("If true, skips route restrictions and chance validation. Kept for quick local testing.")]
    [SerializeField] private bool allowLaunchWithoutValidation = false;

    [Tooltip("If false, travel launch only validates restrictions. The actual danger/outcome should happen during travel, not block scene transition.")]
    [SerializeField] private bool useOutcomeRollBeforeLaunch = false;

    [Tooltip("If true, non-adjacent selected destinations may launch. Usually false; useful only for debug map/fog testing.")]
    [SerializeField] private bool allowNonEdgeTravel = false;

    [Tooltip("If true, player boarding validation is skipped. Boat registry/identity still must resolve.")]
    [SerializeField] private bool allowLaunchWhenNotBoarded = false;

    [Header("Debug")]
    [SerializeField] private int seedOverride = 0; // 0 = use graph seed
    [SerializeField] private bool logTravelDiagnostics = true;

    private readonly Dictionary<string, MapNodeRuntime> _nodesById = new();

    private void Awake()
    {
        AutoWire();

        if (playerLoadoutPersistence == null)
            playerLoadoutPersistence = FindAnyObjectByType<PlayerLoadoutPersistence>();
    }

    private void Reset()
    {
        AutoWire();
        playerLoadoutPersistence = FindAnyObjectByType<PlayerLoadoutPersistence>();
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
        AutoWire();

        if (runtimeBinder != null && runtimeBinder.IsBuilt)
            BuildContext();
    }

    private void AutoWire()
    {
        if (runtimeBinder == null)
            runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>(FindObjectsInactive.Include);

        if (generator == null)
            generator = FindAnyObjectByType<WorldMapGraphGenerator>(FindObjectsInactive.Include);

        if (travelDebug == null)
            travelDebug = FindAnyObjectByType<WorldMapTravelDebugController>(FindObjectsInactive.Include);
    }

    private void BuildContext()
    {
        _nodesById.Clear();

        if (runtimeBinder == null || runtimeBinder.Registry == null)
            return;

        foreach (var rt in runtimeBinder.Registry.AllRuntimes)
        {
            if (rt == null || string.IsNullOrEmpty(rt.StableId))
                continue;

            _nodesById[rt.StableId] = rt;
        }

        RefreshDockedWorldPositionTruth();

        if (logTravelDiagnostics)
            Debug.Log($"[NodeTravelController] Built travel context. Nodes={_nodesById.Count}", this);
    }

    private void RefreshDockedWorldPositionTruth()
    {
        if (!GameplayAuthority.IsAuthoritative)
            return;

        GameState gs = GameState.I;
        if (gs == null ||
            gs.activeTravel != null ||
            gs.player == null ||
            string.IsNullOrWhiteSpace(gs.player.currentNodeId))
        {
            return;
        }

        if (generator == null ||
            generator.graph == null ||
            !_nodesById.TryGetValue(
                gs.player.currentNodeId,
                out MapNodeRuntime currentRt) ||
            currentRt == null)
        {
            return;
        }

        int nodeIndex = currentRt.NodeIndex;
        if (nodeIndex < 0 ||
            nodeIndex >= generator.graph.nodes.Count)
        {
            return;
        }

        Vector2 nodeWorldPosition =
            generator.graph.nodes[nodeIndex].position;

        WorldNavigationService.TrySetAuthoritativeTrueWorldPosition(
            nodeWorldPosition,
            WorldNavigationPositionSource.NodeArrival,
            currentRt.StableId);
    }

    public void TryStartTravel()
    {
        AutoWire();

        GameState gs = GameState.I;

        bool bypassBoarding = ShouldBypassBoardingValidation();

        if (!bypassBoarding && !IsPlayerBoardedToPlayerBoat())
        {
            Debug.LogWarning("NodeTravelLauncher: Travel blocked: player is not boarded to the active boat.");
            return;
        }

        if (gs == null)
        {
            Debug.LogError("NodeTravelLauncher: GameState missing.");
            return;
        }

        if (gs.activeTravel != null)
        {
            Debug.LogWarning("Embark: already underway; dock at a harbor before embarking again.", this);
            return;
        }

        WorldMapPlayerState player = gs.player;
        if (player == null)
        {
            Debug.LogError("NodeTravelLauncher: GameState.player missing.");
            return;
        }

        if (runtimeBinder == null || !runtimeBinder.IsBuilt)
        {
            Debug.LogError("NodeTravelLauncher: runtime not built.");
            return;
        }

        if (_nodesById.Count == 0)
            BuildContext();

        if (generator == null || generator.graph == null)
        {
            Debug.LogError("NodeTravelLauncher: generator/graph missing.");
            return;
        }

        string fromId = player.currentNodeId;
        if (string.IsNullOrWhiteSpace(fromId) ||
            !_nodesById.TryGetValue(fromId, out MapNodeRuntime fromRt) || fromRt == null)
        {
            Debug.LogWarning("Embark: current harbor is unavailable.", this);
            return;
        }
        Vector2 fromWorldPosition = generator.graph.nodes[fromRt.NodeIndex].position;
        int seed = seedOverride != 0 ? seedOverride : MakeTravelSeed(fromId, "open-sea", generator.graph.seed);
        // An expedition has no destination, edge restriction or pre-launch outcome roll.
        // Departure/boarding and safe-water placement are still validated.
        StartTravelSceneTransition(gs, fromId, null, seed, 0f,
            fromWorldPosition, fromWorldPosition);
    }

    private bool ValidateRouteRestrictions(
        TravelRequest req,
        WorldMapSimContext ctx,
        WorldMapPlayerState player,
        out string reason)
    {
        reason = null;

        float maxLen = ResolveMaxRouteLength();

        var maxLength = new MaxRouteLengthRestriction(maxLen);
        if (!maxLength.CanTravel(req, ctx, player, out reason))
            return false;

        var routeUnlock = new RouteUnlockRestriction();
        if (!routeUnlock.CanTravel(req, ctx, player, out reason))
            return false;

        reason = null;
        return true;
    }

    private void StartTravelSceneTransition(
        GameState gs,
        string fromId,
        string toId,
        int seed,
        float routeLength,
        Vector2 fromWorldPosition,
        Vector2 toWorldPosition)
    {
        if (gs.boatRegistry == null)
        {
            Debug.LogError("NodeTravelLauncher: Boat registry missing.");
            return;
        }

        if (gs.boat == null)
        {
            Debug.LogError("NodeTravelLauncher: GameState.boat missing.");
            return;
        }

        if (!gs.boatRegistry.TryGetById(gs.boat.boatInstanceId, out var boatObj) || boatObj == null)
        {
            Debug.LogError("NodeTravelLauncher: Could not resolve player boat from registry.");
            return;
        }

        Transform boatRoot = boatObj.transform;

        BoatIdentity boatId = boatRoot.GetComponent<BoatIdentity>();
        if (boatId == null)
        {
            Debug.LogError("NodeTravelLauncher: Boat missing BoatIdentity component (add it to boat root prefab).");
            return;
        }

        // Persist current selections too (so NodeScene can spawn correctly even without travel)
        gs.boat.boatPrefabGuid = boatId.BoatGuid;

        SceneTransitionController transition = SceneTransitionController.I;
        if (transition == null)
        {
            Debug.LogError("NodeTravelController: SceneTransitionController missing.");
            return;
        }

        Debug.Log($"NodeTravelController: Starting travel | {fromId} -> {toId} | boatGuid={boatId.BoatGuid}", this);

        transition.StartTravelToBoatScene(
            fromId,
            toId,
            seed,
            routeLength,
            gs.boat.boatInstanceId,
            boatId.BoatGuid,
            fromWorldPosition,
            toWorldPosition
        );
    }

    private bool IsPlayerBoardedToPlayerBoat()
    {
        GameState gs = GameState.I;
        if (gs == null)
            return false;

        BoatRegistry reg = gs.boatRegistry;
        if (reg == null)
            return false;

        string boatId = gs.boat?.boatInstanceId;
        if (string.IsNullOrEmpty(boatId))
            return false;

        if (!reg.TryGetById(boatId, out var boat) || boat == null)
            return false;

        PlayerBoardingState boarding = FindAnyObjectByType<PlayerBoardingState>();
        if (boarding == null)
            return false;

        return boarding.IsBoarded && boarding.CurrentBoatRoot == boat.transform;
    }

    private float ResolveMaxRouteLength()
    {
        if (travelDebug != null)
            return travelDebug.MaxRouteLength;

        if (travelRules != null)
            return travelRules.maxRouteLength;

        return float.PositiveInfinity;
    }

    private bool ShouldBypassAllValidation()
    {
        return allowLaunchWithoutValidation ||
               (travelDebug != null && travelDebug.BypassAllTravelValidation);
    }

    private bool ShouldBypassBoardingValidation()
    {
        return allowLaunchWhenNotBoarded ||
               ShouldBypassAllValidation() ||
               (travelDebug != null && travelDebug.BypassBoardingRequirement);
    }

    private bool ShouldBypassDirectEdgeValidation()
    {
        return allowNonEdgeTravel ||
               ShouldBypassAllValidation() ||
               (travelDebug != null && travelDebug.BypassDirectEdgeRequirement);
    }

    private bool ShouldBypassRouteRestrictions()
    {
        return ShouldBypassAllValidation() ||
               (travelDebug != null && travelDebug.BypassRouteRestrictions);
    }

    private bool ShouldUseOutcomeRollBeforeLaunch()
    {
        if (travelDebug != null && travelDebug.BypassOutcomeRoll)
            return false;

        return useOutcomeRollBeforeLaunch;
    }

    private bool ShouldLogDebugTravelDecisions()
    {
        return travelDebug != null && travelDebug.LogTravelDecisions;
    }

    private static int MakeTravelSeed(string fromId, string toId, int graphSeed)
    {
        unchecked
        {
            int hash = graphSeed == 0 ? 17 : graphSeed;
            hash = hash * 31 + StableHash(fromId);
            hash = hash * 31 + StableHash(toId);
            return hash;
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];

            return hash;
        }
    }
}
