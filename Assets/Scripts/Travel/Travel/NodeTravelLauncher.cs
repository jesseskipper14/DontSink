using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NodeTravelLauncher : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string boatSceneName = "BoatScene";

    [Header("Refs")]
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapTravelDebugController travelDebug; // optional (for MaxRouteLength)
    [SerializeField] private WorldMapTravelRulesConfig travelRules;     // optional (for MaxRouteLength)

    [Header("Debug")]
    [SerializeField] private int seedOverride = 0; // 0 = use graph seed
    [SerializeField] private bool allowLaunchWithoutValidation = false;

    private readonly Dictionary<string, MapNodeRuntime> _nodesById = new();

    private void Reset()
    {
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        travelDebug = FindAnyObjectByType<WorldMapTravelDebugController>();
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
        if (runtimeBinder != null && runtimeBinder.IsBuilt)
            BuildContext();
    }

    private void BuildContext()
    {
        _nodesById.Clear();

        foreach (var rt in runtimeBinder.Registry.AllRuntimes)
        {
            if (rt == null || string.IsNullOrEmpty(rt.StableId))
                continue;

            _nodesById[rt.StableId] = rt;
        }
    }

    public void TryStartTravel()
    {
        var gs = GameState.I;

        if (!IsPlayerBoardedToPlayerBoat())
        {
            Debug.LogWarning("Travel blocked: player is not boarded to the boat.");
            return;
        }

        if (gs == null)
        {
            Debug.LogError("NodeTravelLauncher: GameState missing.");
            return;
        }

        var player = gs.player;
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

        if (generator == null || generator.graph == null)
        {
            Debug.LogError("NodeTravelLauncher: generator/graph missing.");
            return;
        }

        var fromId = player.currentNodeId;
        var toId = player.lockedDestinationNodeId;

        if (string.IsNullOrEmpty(fromId))
        {
            Debug.LogError("NodeTravelLauncher: player.currentNodeId is empty.");
            return;
        }

        if (string.IsNullOrEmpty(toId))
        {
            Debug.LogError("NodeTravelLauncher: player.lockedDestinationNodeId is empty (lock a destination first).");
            return;
        }

        if (!_nodesById.TryGetValue(fromId, out var fromRt) || fromRt == null)
        {
            Debug.LogError($"NodeTravelLauncher: fromId not found in runtime registry: '{fromId}'");
            return;
        }

        if (!_nodesById.TryGetValue(toId, out var toRt) || toRt == null)
        {
            Debug.LogError($"NodeTravelLauncher: toId not found in runtime registry: '{toId}'");
            return;
        }

        int fromIndex = fromRt.NodeIndex;
        int toIndex = toRt.NodeIndex;

        if (!generator.graph.HasEdge(fromIndex, toIndex))
        {
            Debug.LogWarning($"NodeTravelLauncher: no direct edge {fromIndex} <-> {toIndex}. (Phase-1 travel is edge-only.)");
            return;
        }

        float routeLength = Vector2.Distance(
            generator.graph.nodes[fromIndex].position,
            generator.graph.nodes[toIndex].position);

        int seed = seedOverride != 0 ? seedOverride : generator.graph.seed;

        // Optional: validate using your travel system rules
        if (!allowLaunchWithoutValidation)
        {
            var ctx = new WorldMapSimContext(_nodesById);

            float maxLen =
                travelRules != null ? travelRules.maxRouteLength :
                (travelDebug != null ? travelDebug.MaxRouteLength : float.PositiveInfinity);

            var travelSystem = new TravelSystem(
                new RestrictionGateResolver(
                    new MaxRouteLengthRestriction(maxLen),
                    new RouteUnlockRestriction()
                ),
                new SimpleTravelResolver()
            );

            var req = new TravelRequest(fromId, toId, routeLength, seed);
            var result = travelSystem.TryTravel(req, ctx, player);

            if (!result.success)
            {
                Debug.LogWarning($"NodeTravelLauncher: Travel blocked: {result.failureReason} (roll={result.roll})");
                return;
            }
        }

        // Create payload and switch scene
        // Resolve current boat root via registry so we capture the boat the player is actually using.
        if (!gs.boatRegistry.TryGetById(gs.boat.boatInstanceId, out var boatObj) || boatObj == null)
        {
            Debug.LogError("NodeTravelLauncher: Could not resolve player boat from registry.");
            return;
        }

        var boatRoot = boatObj.transform;

        // Boat GUID (which prefab to spawn next scene)
        var boatId = boatRoot.GetComponent<BoatIdentity>();
        if (boatId == null)
        {
            Debug.LogError("NodeTravelLauncher: Boat missing BoatIdentity component (add it to boat root prefab).");
            return;
        }

        // Cargo manifest capture
        var boarded = boatRoot.GetComponentInChildren<BoatBoardedVolume>(true);
        var volumeCol = boarded != null ? boarded.GetComponent<Collider2D>() : null;

        var cargoManifest = CargoManifest.Capture(boatRoot, volumeCol);

        // Persist current selections too (so NodeScene can spawn correctly even without travel)
        gs.boat.boatPrefabGuid = boatId.BoatGuid;
        gs.boat.cargo = cargoManifest;

        // Create payload and switch scene
        gs.activeTravel = new TravelPayload(fromId, toId, seed, routeLength, gs.boat.boatInstanceId, boatId.BoatGuid, cargoManifest);
        Debug.Log($"NodeTravelLauncher: Loading boat scene '{boatSceneName}' | {fromId} -> {toId} | boatGuid={boatId.BoatGuid} | cargo={cargoManifest.Count}");

        SceneManager.LoadScene(boatSceneName);
    }

    private bool IsPlayerBoardedToPlayerBoat()
    {
        var gs = GameState.I;
        if (gs == null) return false;

        var reg = gs.boatRegistry;
        if (reg == null) return false;

        var boatId = gs.boat?.boatInstanceId;
        if (string.IsNullOrEmpty(boatId)) return false;

        if (!reg.TryGetById(boatId, out var boat) || boat == null)
            return false;

        var boarding = FindAnyObjectByType<PlayerBoardingState>();
        if (boarding == null) return false;

        return boarding.IsBoarded && boarding.CurrentBoatRoot == boat.transform;
    }
}
