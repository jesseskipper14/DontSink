using UnityEngine;
using WorldMap.Player.StarMap;

public sealed class StarMapDebugController : MonoBehaviour
{
    [Header("Refs")]
    public WorldMapPlayerRef playerRef; // optional; auto-find if null
    public WorldMapRuntimeBinder runtimeBinder; // optional; auto-find if null

    [Header("Target")]
    [Tooltip("Graph node index (NOT stableId). Used to pick a destination node.")]
    public int targetNodeIndex = 0;

    [Header("Actions")]
    public int rumorAmount = 1;
    [Range(0f, 1f)] public float progressDelta01 = 0.10f;
    public RouteKnowledgeState forceState = RouteKnowledgeState.Rumored;

    private StarMapService _svc;
    private bool _bound;

    private void Awake()
    {
        // Don't bind here. WorldMapPlayerRefRef.State may not exist yet.
        if (playerRef == null) playerRef = FindFirstObjectByType<WorldMapPlayerRef>();
        if (runtimeBinder == null) runtimeBinder = FindFirstObjectByType<WorldMapRuntimeBinder>();
    }

    private void Start()
    {
        // First attempt after Awake order.
        EnsureBound(logIfNotReady: false);
    }

    private void Update()
    {
        // Retry until ready, then stop doing work.
        if (!_bound)
            EnsureBound(logIfNotReady: false);
    }

    private bool EnsureBound(bool logIfNotReady)
    {
        if (_bound)
            return true;

        if (playerRef == null)
        {
            if (logIfNotReady) Debug.LogError("[StarMapDebugController] No WorldMapPlayerRef found/assigned.");
            return false;
        }

        if (runtimeBinder == null || runtimeBinder.Registry == null)
        {
            if (logIfNotReady) Debug.LogError("[StarMapDebugController] Missing runtimeBinder/registry.");
            return false;
        }

        // State may be initialized later by your WorldMapPlayerRef flow.
        if (playerRef.State == null)
        {
            if (logIfNotReady) Debug.LogWarning("[StarMapDebugController] WorldMapPlayerRefRef.State not ready yet.");
            return false;
        }

        if (playerRef.State.starMap == null)
            playerRef.State.starMap = new PlayerStarMapState();

        _svc = new StarMapService(playerRef.State.starMap);
        _bound = true;

        Debug.Log("[StarMapDebugController] Bound to player starMap state.");
        return true;
    }

    [ContextMenu("StarMap/Print Route State (Current -> Target)")]
    public void PrintRouteState()
    {
        if (!EnsureBound(logIfNotReady: true))
            return;

        if (!TryGetRouteKey(out var key, out var fromId, out var toId))
            return;

        var state = _svc.GetKnowledgeState(key);
        Debug.Log($"[StarMap] {fromId} -> {toId} key={key} state={state}");
    }

    [ContextMenu("StarMap/Add Rumor (Current -> Target)")]
    public void AddRumor()
    {
        if (!EnsureBound(logIfNotReady: true))
            return;

        if (!TryGetRouteKey(out var key, out var fromId, out var toId))
            return;

        var state = _svc.AddRumor(key, rumorAmount);
        Debug.Log($"[StarMap] AddRumor {fromId}->{toId} (+{rumorAmount}) => {state}");
    }

    [ContextMenu("StarMap/Add Progress (Current -> Target)")]
    public void AddProgress()
    {
        if (!EnsureBound(logIfNotReady: true))
            return;

        if (!TryGetRouteKey(out var key, out var fromId, out var toId))
            return;

        var state = _svc.AddProgress(key, progressDelta01);
        Debug.Log($"[StarMap] AddProgress {fromId}->{toId} (+{progressDelta01:0.00}) => {state}");
    }

    [ContextMenu("StarMap/Force State (Current -> Target)")]
    public void ForceRouteState()
    {
        if (!EnsureBound(logIfNotReady: true))
            return;

        if (!TryGetRouteKey(out var key, out var fromId, out var toId))
            return;

        _svc.ForceState(key, forceState);
        Debug.Log($"[StarMap] ForceState {fromId}->{toId} => {forceState}");
    }

    [ContextMenu("StarMap/Mark Known + Unlock Travel (No Cost)")]
    public void MarkKnownAndUnlockTravel_NoCost()
    {
        if (!EnsureBound(logIfNotReady: true))
            return;

        if (!TryGetRouteKey(out var key, out var fromId, out var toId))
            return;

        // 1) Star map truth
        _svc.ForceState(key, RouteKnowledgeState.Known);

        // 2) Compatibility layer for current travel gate (unlockedRoutes)
        // Safe even for intra-cluster (harmless).
        playerRef.State.unlockedRoutes.Add(key);

        Debug.Log($"[StarMap] Known+UnlockTravel {fromId}->{toId} key={key}");
    }

    private bool TryGetRouteKey(out string routeKey, out string fromStableId, out string toStableId)
    {
        routeKey = null;
        fromStableId = null;
        toStableId = null;

        // currentNodeId is the player's runtime stableId string
        fromStableId = playerRef.State.currentNodeId;
        if (string.IsNullOrWhiteSpace(fromStableId))
        {
            Debug.LogError("[StarMapDebugController] Player currentNodeId is null/empty.");
            return false;
        }

        if (!runtimeBinder.Registry.TryGetByIndex(targetNodeIndex, out var toRt) || toRt == null)
        {
            Debug.LogError($"[StarMapDebugController] No node runtime at index {targetNodeIndex}.");
            return false;
        }

        toStableId = toRt.StableId;

        if (fromStableId == toStableId)
        {
            Debug.LogWarning("[StarMapDebugController] Target is current node. Pick a different node index.");
            return false;
        }

        routeKey = RouteKey.Make(fromStableId, toStableId);
        return true;
    }
}
