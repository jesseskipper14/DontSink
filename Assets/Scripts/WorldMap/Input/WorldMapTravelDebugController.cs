using System.Collections.Generic;
using UnityEngine;

public sealed class WorldMapTravelDebugController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapPlayer player;

    [Header("Travel Rules")]
    [SerializeField] private WorldMapTravelRulesConfig travelRules;
    [SerializeField] private float maxRouteLengthOverride = 999f; // debug huge by default
    [SerializeField] private bool useOverrideMaxRouteLength = false;
    [SerializeField] private bool allowIntraClusterFreeTravel = true; // already in RouteUnlockRestriction

    [Header("Debug Override")]
    [SerializeField] private TravelOverrideMode overrideMode = TravelOverrideMode.None;

    [Header("Debug Travel Input")]
    [SerializeField] private string toNodeId;
    [SerializeField] private float routeLength = 1f;
    [SerializeField] private int seed = 12345;

    private TravelSystem _travelSystem;
    private WorldMapSimContext _ctx;
    private Dictionary<string, MapNodeRuntime> _nodesById;
    public float MaxRouteLength => GetMaxRouteLength();

    private void Reset()
    {
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        player = FindAnyObjectByType<WorldMapPlayer>();
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

    private void BuildContext()
    {
        _nodesById = new Dictionary<string, MapNodeRuntime>();
        foreach (var rt in runtimeBinder.Registry.AllRuntimes)
        {
            if (rt == null || string.IsNullOrEmpty(rt.StableId))
                continue;

            _nodesById[rt.StableId] = rt;
        }

        _ctx = new WorldMapSimContext(_nodesById);
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
        if (useOverrideMaxRouteLength)
            return maxRouteLengthOverride;

        if (travelRules != null)
            return travelRules.maxRouteLength;

        // Reasonable fallback while prototyping.
        return maxRouteLengthOverride;
    }

    private void OnValidate()
    {
        // keep system in sync when you tweak override/limits in inspector
        if (Application.isPlaying)
            RebuildTravelSystem();
    }

    [ContextMenu("Debug Try Travel")]
    public void DebugTryTravel()
    {
        if (_ctx == null || player == null || player.State == null)
        {
            Debug.LogError("TravelDebug: missing ctx/player/state (is runtime built?)");
            return;
        }

        if (string.IsNullOrEmpty(toNodeId))
        {
            Debug.LogError("TravelDebug: toNodeId is empty.");
            return;
        }

        var from = player.State.currentNodeId;
        var req = new TravelRequest(from, toNodeId, routeLength, seed);

        var result = _travelSystem.TryTravel(req, _ctx, player.State);

        if (!result.success)
        {
            Debug.LogWarning($"Travel FAIL: {result.failureReason} (roll={result.roll})");
            return;
        }

        // snap player transform to destination runtime node position
        var dest = _ctx.GetNode(toNodeId);
        player.transform.position = dest.transform.position;

        Debug.Log($"Travel OK: {from} -> {toNodeId} (roll={result.roll})");
    }

    [ContextMenu("Debug Unlock Route (From current -> To)")]
    public void DebugUnlockRoute()
    {
        if (player == null || player.State == null)
        {
            Debug.LogError("TravelDebug: missing player/state");
            return;
        }

        if (string.IsNullOrEmpty(toNodeId))
        {
            Debug.LogError("TravelDebug: toNodeId is empty.");
            return;
        }

        var from = player.State.currentNodeId;
        var key = RouteKey.Make(from, toNodeId);
        player.State.unlockedRoutes.Add(key);

        Debug.Log($"Unlocked route: {key}");
    }

    [ContextMenu("Debug Lock Route (From current -> To)")]
    public void DebugLockRoute()
    {
        if (player == null || player.State == null)
        {
            Debug.LogError("TravelDebug: missing player/state");
            return;
        }

        if (string.IsNullOrEmpty(toNodeId))
        {
            Debug.LogError("TravelDebug: toNodeId is empty.");
            return;
        }

        var from = player.State.currentNodeId;
        var key = RouteKey.Make(from, toNodeId);
        player.State.unlockedRoutes.Remove(key);

        Debug.Log($"Locked route: {key}");
    }
}
