using UnityEngine;
using UnityEngine.SceneManagement;

// BoatSceneController owns BoatScene dock layout + docking UI.
// SceneTransitionController owns persistence and scene loading.
public sealed class BoatSceneController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string nodeSceneName = "NodeScene";

    [Header("Scene Anchors")]
    [SerializeField] private BoatSceneContext ctx;

    [Header("Dock UI")]
    [SerializeField] private DockingActionPanel dockingPanel;

    [Header("Layout")]
    [Tooltip("X position for the dock you departed from (behind you).")]
    [SerializeField] private float sourceDockX = -20f;

    [Tooltip("How far the target dock extends in +X from its anchor. This amount is subtracted so the dock ends at total travel distance.")]
    [SerializeField] private float targetDockLength = 20f;

    [Tooltip("Base travel distance in world units before scaling.")]
    [SerializeField] private float baseTravelDistance = 200f;

    [Tooltip("Inspector knob to make trips shorter/longer for debugging.")]
    [Min(0.05f)]
    [SerializeField] private float distanceScale = 1f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;
    [Header("Geographic harbor docking")]
    [SerializeField] private bool useHarborDocking = true;
    [SerializeField] private HarborPresentationSettings harborPresentation = new();
    public HarborPresentationSettings HarborPresentation => harborPresentation;
    public Boat HarborBoat => _harborBoat;
    public BoatSceneWorldPositionBridge HarborBridge => _harborBridge;
    private Boat _harborBoat;
    private BoatSceneWorldPositionBridge _harborBridge;
    private string _harborNodeId;
    private MapNode _harborNode;
    private HarborBerth _harborBerth;
    private float _nextHarborQuery;
    private bool _overlapWarning;
    public string HarborStatus { get; private set; } = "waiting for harbor context";
    public HarborBerth CurrentHarborBerth => _harborBerth;
    public string CurrentHarborNodeId => _harborNodeId;

    public TravelPayload Payload { get; private set; }

    /// <summary>
    /// Nominal local BoatScene source-to-target travel distance.
    /// WorldNavigation bridging uses this only as a scale conversion; it does not
    /// redefine or own piloting/navigation state.
    /// </summary>
    public float NominalTravelDistance =>
        Mathf.Max(0.01f, baseTravelDistance * distanceScale);

    private bool _completed;
    private bool _initialized;

    private DockTrigger _activeDockInRange;

    private void Reset()
    {
        ctx = FindAnyObjectByType<BoatSceneContext>();
        dockingPanel = FindAnyObjectByType<DockingActionPanel>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        EnsureContext();
        EnsureDockingPanel();
        HookDockEvents();
    }

    private void OnDisable()
    {
        UnhookDockEvents();
    }

    private void Start()
    {
        if (_initialized)
            return;

        _initialized = true;

        GameState gs = GameState.I;
        if (gs == null)
        {
            Debug.LogError("[BoatSceneController] GameState missing. Cannot run BoatScene.", this);
            return;
        }

        Payload = gs.activeTravel;
        if (Payload == null)
        {
            Debug.LogError("[BoatSceneController] No active travel payload. Returning to node scene.", this);
            SceneManager.LoadScene(nodeSceneName);
            return;
        }

        EnsureContext();
        if (ctx == null && !useHarborDocking)
        {
            Debug.LogError("[BoatSceneController] Missing BoatSceneContext in BoatScene.", this);
            return;
        }

        Log(
            $"Start | payload from='{Payload.fromNodeStableId}' to='{Payload.toNodeStableId}' " +
            $"boatId='{Payload.boatInstanceId}' boatGuid='{Payload.boatPrefabGuid}'");

        LayoutDocks();
        if (useHarborDocking)
        {
            if (dockingPanel != null) dockingPanel.Hide();
            var action = GetComponent<HarborDockInteraction>();
            if (action == null) action = gameObject.AddComponent<HarborDockInteraction>();
            action.Controller = this;
            var presentation = GetComponent<BoatHarborPresentation>();
            if (presentation == null) presentation = gameObject.AddComponent<BoatHarborPresentation>();
            presentation.Controller = this;
        }
    }

    private void EnsureContext()
    {
        if (ctx != null)
            return;

        ctx = FindAnyObjectByType<BoatSceneContext>();
    }

    private void EnsureDockingPanel()
    {
        if (dockingPanel != null)
            return;

        dockingPanel = FindAnyObjectByType<DockingActionPanel>(FindObjectsInactive.Include);
    }

    private void HookDockEvents()
    {
        if (useHarborDocking) return;
        if (ctx == null)
            return;

        if (ctx.sourceDockTrigger != null)
        {
            ctx.sourceDockTrigger.OnEnteredRange -= OnDockEnteredRange;
            ctx.sourceDockTrigger.OnExitedRange -= OnDockExitedRange;

            ctx.sourceDockTrigger.OnEnteredRange += OnDockEnteredRange;
            ctx.sourceDockTrigger.OnExitedRange += OnDockExitedRange;
        }

        if (ctx.targetDockTrigger != null)
        {
            ctx.targetDockTrigger.OnEnteredRange -= OnDockEnteredRange;
            ctx.targetDockTrigger.OnExitedRange -= OnDockExitedRange;

            ctx.targetDockTrigger.OnEnteredRange += OnDockEnteredRange;
            ctx.targetDockTrigger.OnExitedRange += OnDockExitedRange;
        }
    }

    private void UnhookDockEvents()
    {
        if (ctx == null)
            return;

        if (ctx.sourceDockTrigger != null)
        {
            ctx.sourceDockTrigger.OnEnteredRange -= OnDockEnteredRange;
            ctx.sourceDockTrigger.OnExitedRange -= OnDockExitedRange;
        }

        if (ctx.targetDockTrigger != null)
        {
            ctx.targetDockTrigger.OnEnteredRange -= OnDockEnteredRange;
            ctx.targetDockTrigger.OnExitedRange -= OnDockExitedRange;
        }
    }

    private void LayoutDocks()
    {
        if (useHarborDocking)
        {
            if (ctx != null && ctx.sourceDockAnchor != null) ctx.sourceDockAnchor.gameObject.SetActive(false);
            if (ctx != null && ctx.targetDockAnchor != null) ctx.targetDockAnchor.gameObject.SetActive(false);
            return;
        }
        if (ctx.sourceDockAnchor != null)
        {
            Vector3 p = ctx.sourceDockAnchor.position;
            p.x = sourceDockX;
            ctx.sourceDockAnchor.position = p;
        }

        float dist = NominalTravelDistance;
        float targetDockEndX = sourceDockX + dist;

        if (ctx.targetDockAnchor != null)
        {
            Vector3 p = ctx.targetDockAnchor.position;
            p.x = targetDockEndX - targetDockLength;
            ctx.targetDockAnchor.position = p;
        }

        Log($"LayoutDocks | sourceDockX={sourceDockX} | dist={dist} | targetDockEndX={targetDockEndX}");
    }

    private void OnDockEnteredRange(DockTrigger trigger, Collider2D other)
    {
        if (useHarborDocking) return;
        if (_completed)
            return;

        _activeDockInRange = trigger;

        EnsureDockingPanel();
        if (dockingPanel == null)
            return;

        string msg = trigger.kind == DockTrigger.DockKind.Source
            ? "Dock (return to departure node)"
            : "Dock (arrive at destination node)";

        dockingPanel.Show(msg, onDock: () => ConfirmDock(trigger));
    }

    private void OnDockExitedRange(DockTrigger trigger, Collider2D other)
    {
        if (_activeDockInRange != trigger)
            return;

        _activeDockInRange = null;

        if (dockingPanel != null)
            dockingPanel.Hide();
    }

    private void ConfirmDock(DockTrigger trigger)
    {
        if (_completed)
            return;

        if (_activeDockInRange != trigger)
            return;

        if (dockingPanel != null)
            dockingPanel.Hide();

        Payload ??= GameState.I != null ? GameState.I.activeTravel : null;
        if (Payload == null)
        {
            Debug.LogError("[BoatSceneController] Cannot dock because Payload is null.", this);
            return;
        }

        switch (trigger.kind)
        {
            case DockTrigger.DockKind.Source:
                AbortTravelToSource();
                break;

            case DockTrigger.DockKind.Destination:
                CompleteTravelToDestination();
                break;
        }
    }

    private void AbortTravelToSource()
    {
        if (_completed)
            return;

        SceneTransitionController transition = SceneTransitionController.I;
        if (transition == null)
        {
            Debug.LogError(
                "[BoatSceneController] SceneTransitionController missing on abort. " +
                "Cannot safely persist/transition. Add SceneTransitionController to bootstrap.",
                this);
            return;
        }

        if (!TryPassDepartureGate(transition, "Return to source"))
            return;

        _completed = true;
        transition.AbortTravelToSource();
    }

    private void CompleteTravelToDestination()
    {
        if (_completed)
            return;

        SceneTransitionController transition = SceneTransitionController.I;
        if (transition == null)
        {
            Debug.LogError(
                "[BoatSceneController] SceneTransitionController missing on completion. " +
                "Cannot safely persist/transition. Add SceneTransitionController to bootstrap.",
                this);
            return;
        }

        if (!TryPassDepartureGate(transition, "Travel completion"))
            return;

        _completed = true;
        transition.CompleteTravelToDestination();
    }

    private bool TryPassDepartureGate(
        SceneTransitionController transition,
        string actionLabel)
    {
        if (transition == null)
            return false;

        if (transition.CanDepartCurrentScene(out string reason))
            return true;

        LogWarning($"{actionLabel} blocked | {reason}");
        transition.ReportDepartureBlocked(actionLabel, reason);
        ShowDepartureBlocked(reason);
        return false;
    }

    private void ShowDepartureBlocked(string reason)
    {
        EnsureDockingPanel();

        DockTrigger trigger = _activeDockInRange;
        if (dockingPanel == null || trigger == null)
            return;

        dockingPanel.Show(
            $"Cannot depart: {reason}",
            onDock: () => ConfirmDock(trigger));
    }

    [ContextMenu("DEBUG: Dock to Source (Abort Travel)")]
    public void DebugDockToSource()
    {
        Payload = GameState.I != null ? GameState.I.activeTravel : Payload;
        if (GameState.I == null || Payload == null)
        {
            Debug.LogError("[BoatSceneController] Missing GameState/Payload.", this);
            return;
        }

        AbortTravelToSource();
    }

    [ContextMenu("DEBUG: Dock to Destination (Complete Travel)")]
    public void DebugDockToDestination()
    {
        Payload = GameState.I != null ? GameState.I.activeTravel : Payload;
        if (GameState.I == null || Payload == null)
        {
            Debug.LogError("[BoatSceneController] Missing GameState/Payload.", this);
            return;
        }

        CompleteTravelToDestination();
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[BoatSceneController] {msg}", this);
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatSceneController] {msg}", this);
    }

    private void Update()
    {
        if (!useHarborDocking || _completed || Time.unscaledTime < _nextHarborQuery) return;
        _nextHarborQuery = Time.unscaledTime + .25f;
        _harborNodeId = null; _harborNode = null; _harborBerth = default;
        var transition = SceneTransitionController.I;
        var gs = GameState.I;
        if (transition == null || gs == null || gs.activeTravel == null || transition.HarborSettings.terrainProfile == null)
        { HarborStatus = "assign SceneTransitionController Harbor Settings / Terrain Profile"; return; }
        if (_harborBoat == null && gs.boatRegistry != null && gs.boat != null)
            gs.boatRegistry.TryGetById(gs.boat.boatInstanceId, out _harborBoat);
        if (_harborBoat == null) { HarborStatus = "boat unavailable"; return; }
        BoatSceneWorldPositionBridge.TryGetForState(_harborBoat.GetComponent<BoatPilotingState>(), out _harborBridge);
        if (_harborBridge == null || !_harborBridge.TryRefreshProjection() || !WorldNavigationService.TryGetTrueWorldPosition(out var world))
        { HarborStatus = "navigation unavailable"; return; }
        var graph = HarborTravelService.CurrentGraph;
        if (graph == null) { HarborStatus = "node graph unavailable"; return; }
        float nearest = float.PositiveInfinity; int nearby = 0;
        var field = WorldMapRuntimeCache.I != null ? WorldMapRuntimeCache.I.Field : null;
        float searchRange = transition.HarborSettings.guidanceRange;
        if (field != null && field.IsValid)
        {
            float grid = Mathf.Min(field.WorldBounds.width / (field.Width - 1), field.WorldBounds.height / (field.Height - 1));
            searchRange += Mathf.Min(Mathf.Min(field.WorldBounds.width, field.WorldBounds.height) * .25f, Mathf.Max(16, grid * 48));
        }
        HarborStatus = "no nearby harbor";
        foreach (var node in graph.nodes)
        {
            if (node == null || WorldTopologyService.Distance(world, node.position) > searchRange) continue;
            if (!HarborTravelService.TryGeometry(node, _harborBoat, _harborBridge.WorldUnitsPerLocalUnit, transition.HarborSettings, out var berth, out string reason))
            { HarborStatus = reason; continue; }
            float distance = WorldTopologyService.Distance(world, berth.Center);
            if (distance > transition.HarborSettings.guidanceRange && !berth.Contains(world, WorldTopologyService.Current)) continue;
            nearby++;
            if (distance >= nearest) continue;
            nearest = distance; _harborNode = node; _harborBerth = berth;
            _harborNodeId = WorldMapStableIdUtility.BuildNodeStableId(graph.seed, node);
        }
        if (nearby > 1 && !_overlapWarning) Debug.LogWarning("Multiple harbor guidance ranges overlap; check node spacing/range tuning.", this);
        _overlapWarning = nearby > 1;
        if (_harborNode != null)
            HarborStatus = $"{_harborNode.displayName}: berth {nearest:0.00} map units away; " +
                (_harborBerth.Contains(world, WorldTopologyService.Current) ? "E — Dock available" : "outside berth");
    }

    public bool CanDockAtHarbor(string nodeId, GameObject requester)
    {
        if (!useHarborDocking || _completed || string.IsNullOrEmpty(nodeId) || requester == null || requester.scene != gameObject.scene || nodeId != _harborNodeId || _harborBoat == null ||
            !WorldNavigationService.TryGetTrueWorldPosition(out var world) || !_harborBerth.Contains(world, WorldTopologyService.Current)) return false;
        var boarding = requester.GetComponentInParent<PlayerBoardingState>();
        return boarding != null && boarding.IsBoarded && boarding.CurrentBoatRoot == _harborBoat.transform;
    }

    public string HarborDockVerb => _harborNode != null ? $"Dock at {_harborNode.displayName}" : "Dock";

    public bool TryGetDestinationBerth(out HarborBerth berth, out string reason)
    {
        berth = default; reason = "Destination harbor/boat context unavailable.";
        var payload = GameState.I != null ? GameState.I.activeTravel : null;
        var transition = SceneTransitionController.I;
        if (payload == null || transition == null || _harborBoat == null || _harborBridge == null || !_harborBridge.TryRefreshProjection() ||
            !HarborTravelService.TryGetNode(payload.toNodeStableId, out var node)) return false;
        return HarborTravelService.TryGeometry(node, _harborBoat, _harborBridge.WorldUnitsPerLocalUnit, transition.HarborSettings, out berth, out reason);
    }
}
