using UnityEngine;
using MiniGames;
using WorldMap.Player.StarMap;

public sealed class StarObservationWorldMapRunner : MonoBehaviour
{
    [Header("Refs")]
    public MiniGameOverlayHost overlay;
    public WorldMapPlayerRef playerRef;
    public WorldMapRuntimeBinder runtimeBinder;

    [Header("Target")]
    [Tooltip("Graph node index (NOT stableId) for the destination node.")]
    public int targetNodeIndex = 0;

    [Header("Tuning")]
    [Range(0.25f, 3f)] public float difficulty = 1f;

    private StarMapService _svc;
    private string _activeRouteKey;
    private bool _subscribed;

    private void Reset()
    {
        overlay = FindAnyObjectByType<MiniGameOverlayHost>();
        playerRef = FindAnyObjectByType<WorldMapPlayerRef>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed || overlay == null) return;
        overlay.EffectEmitted += OnMiniGameEffect;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || overlay == null) return;
        overlay.EffectEmitted -= OnMiniGameEffect;
        _subscribed = false;
    }

    private void OnMiniGameEffect(MiniGameEffect e)
    {
        // Only listen while we have an active route context.
        if (string.IsNullOrEmpty(_activeRouteKey) || _svc == null || playerRef?.State == null) return;

        // Only handle StarMap progress effects for THIS session's route.
        if (e.kind != MiniGameEffectKind.Progress) return;
        if (e.system != "StarMap") return;
        if (e.targetId != _activeRouteKey) return;

        // Apply progress (service currently stores progress; quality can be incorporated later).
        var newState = _svc.AddProgress(_activeRouteKey, e.value01);

        // Bridge for travel compatibility: once Known, mirror into unlockedRoutes.
        if (newState == RouteKnowledgeState.Known)
            playerRef.State.unlockedRoutes.Add(_activeRouteKey);
    }

    [ContextMenu("StarObs/Open (Current -> Target)")]
    public void OpenStarObservation()
    {
        if (overlay == null)
        {
            Debug.LogError("[StarObservationWorldMapRunner] Missing MiniGameOverlayHost.");
            return;
        }
        if (playerRef == null || playerRef.State == null)
        {
            Debug.LogError("[StarObservationWorldMapRunner] Missing WorldMapPlayerRef/State.");
            return;
        }
        if (runtimeBinder == null || !runtimeBinder.IsBuilt)
        {
            Debug.LogError("[StarObservationWorldMapRunner] Runtime not built yet.");
            return;
        }
        if (!runtimeBinder.Registry.TryGetByStableId(playerRef.State.currentNodeId, out var fromRt) || fromRt == null)
        {
            Debug.LogError("[StarObservationWorldMapRunner] Could not resolve current node runtime.");
            return;
        }
        if (!runtimeBinder.Registry.TryGetByIndex(targetNodeIndex, out var toRt) || toRt == null)
        {
            Debug.LogError($"[StarObservationWorldMapRunner] Invalid targetNodeIndex {targetNodeIndex}.");
            return;
        }
        if (fromRt.StableId == toRt.StableId)
        {
            Debug.LogWarning("[StarObservationWorldMapRunner] Target is current node. Pick a different target.");
            return;
        }

        playerRef.State.starMap ??= new PlayerStarMapState();
        _svc = new StarMapService(playerRef.State.starMap);

        _activeRouteKey = RouteKey.Make(fromRt.StableId, toRt.StableId);

        // Subscribe BEFORE opening so we don't miss early effects.
        Subscribe();

        var ctx = new MiniGameContext
        {
            targetId = _activeRouteKey,
            difficulty = difficulty,
            pressure = 0f,
            seed = Time.frameCount // deterministic-ish per attempt
        };

        // New: cartridge emits MiniGameEffect via ctx.emitEffect -> host -> EffectEmitted
        var cart = new StarObservationCartridge();

        overlay.Open(cart, ctx);

        Debug.Log($"[StarObs] Opened for {fromRt.DisplayName} -> {toRt.DisplayName} key={_activeRouteKey}");
    }
}
