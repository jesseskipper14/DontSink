using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Tooltip("Base travel distance in world units before scaling.")]
    [SerializeField] private float baseTravelDistance = 200f;

    [Tooltip("Inspector knob to make trips shorter/longer for debugging.")]
    [Min(0.05f)]
    [SerializeField] private float distanceScale = 1f;

    public TravelPayload Payload { get; private set; }

    private bool _completed;
    private bool _initialized;

    // NEW: which dock we are currently “in range” of (if any)
    private DockTrigger _activeDockInRange;

    private void Reset()
    {
        ctx = FindAnyObjectByType<BoatSceneContext>();
        dockingPanel = FindAnyObjectByType<DockingActionPanel>();
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
        if (_initialized) return;
        _initialized = true;

        var gs = GameState.I;
        if (gs == null)
        {
            Debug.LogError("[BoatSceneController] GameState missing. Cannot run BoatScene.");
            return;
        }

        Payload = gs.activeTravel;
        if (Payload == null)
        {
            Debug.LogError("[BoatSceneController] No active travel payload. Returning to node scene.");
            SceneManager.LoadScene(nodeSceneName);
            return;
        }

        EnsureContext();
        if (ctx == null)
        {
            Debug.LogError("[BoatSceneController] Missing BoatSceneContext in BoatScene.");
            return;
        }

        LayoutDocks();
    }

    private void EnsureContext()
    {
        if (ctx != null) return;
        ctx = FindAnyObjectByType<BoatSceneContext>();
    }

    private void EnsureDockingPanel()
    {
        if (dockingPanel != null) return;
        dockingPanel = FindAnyObjectByType<DockingActionPanel>(FindObjectsInactive.Include);
    }

    private void HookDockEvents()
    {
        if (ctx == null) return;

        // Idempotent: avoid double subscriptions.
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
        if (ctx == null) return;

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
        if (ctx.sourceDockAnchor != null)
        {
            var p = ctx.sourceDockAnchor.position;
            p.x = sourceDockX;
            ctx.sourceDockAnchor.position = p;
        }

        float dist = baseTravelDistance * distanceScale;

        if (ctx.targetDockAnchor != null)
        {
            var p = ctx.targetDockAnchor.position;
            p.x = dist;
            ctx.targetDockAnchor.position = p;
        }
    }

    private void OnDockEnteredRange(DockTrigger trigger, Collider2D other)
    {
        if (_completed) return;

        _activeDockInRange = trigger;

        EnsureDockingPanel();
        if (dockingPanel == null) return;

        string msg = trigger.kind == DockTrigger.DockKind.Source
            ? "Dock (return to departure node)"
            : "Dock (arrive at destination node)";

        dockingPanel.Show(msg, onDock: () => ConfirmDock(trigger));
    }

    private void OnDockExitedRange(DockTrigger trigger, Collider2D other)
    {
        if (_activeDockInRange != trigger) return;

        _activeDockInRange = null;

        if (dockingPanel != null)
            dockingPanel.Hide();
    }

    private void ConfirmDock(DockTrigger trigger)
    {
        if (_completed) return;

        // Must still be the active dock in range
        if (_activeDockInRange != trigger) return;

        if (dockingPanel != null)
            dockingPanel.Hide();

        // FUTURE-PROOF HOOK:
        // In the future, instead of instantly transitioning, you can:
        // - open a Docking mini-game via MiniGameOverlayHost
        // - on success/partial/fail, decide whether to proceed
        //
        // For now: immediate scene transition.
        Payload ??= (GameState.I != null ? GameState.I.activeTravel : null);
        if (Payload == null) return;

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
        _completed = true;
        PersistBoatAndCargo();

        var gs = GameState.I;
        if (gs == null)
        {
            Debug.LogError("[BoatSceneController] GameState missing on abort.");
            return;
        }

        gs.player.currentNodeId = Payload.fromNodeStableId;
        gs.ClearTravel();
        SceneManager.LoadScene(nodeSceneName);
    }

    private void CompleteTravelToDestination()
    {
        _completed = true;
        PersistBoatAndCargo();

        var gs = GameState.I;
        if (gs == null)
        {
            Debug.LogError("[BoatSceneController] GameState missing on completion.");
            return;
        }

        gs.player.currentNodeId = Payload.toNodeStableId;
        gs.ClearTravel();
        SceneManager.LoadScene(nodeSceneName);
    }

    [ContextMenu("DEBUG: Dock to Source (Abort Travel)")]
    public void DebugDockToSource()
    {
        PersistBoatAndCargo();
        Payload = GameState.I != null ? GameState.I.activeTravel : Payload;
        if (GameState.I == null || Payload == null)
        {
            Debug.LogError("[BoatSceneController] Missing GameState/Payload.");
            return;
        }

        AbortTravelToSource();
    }

    [ContextMenu("DEBUG: Dock to Destination (Complete Travel)")]
    public void DebugDockToDestination()
    {
        PersistBoatAndCargo();
        Payload = GameState.I != null ? GameState.I.activeTravel : Payload;
        if (GameState.I == null || Payload == null)
        {
            Debug.LogError("[BoatSceneController] Missing GameState/Payload.");
            return;
        }

        CompleteTravelToDestination();
    }

    private void PersistBoatAndCargo()
    {
        var gs = GameState.I;
        if (gs == null) return;
        if (gs.boatRegistry == null) return;

        if (!gs.boatRegistry.TryGetById(gs.boat.boatInstanceId, out var boatObj) || boatObj == null)
            return;

        var boatRoot = boatObj.transform;

        var boatId = boatRoot.GetComponent<BoatIdentity>();
        if (boatId != null)
            gs.boat.boatPrefabGuid = boatId.BoatGuid;

        var boarded = boatRoot.GetComponentInChildren<BoatBoardedVolume>(true);
        var volumeCol = boarded != null ? boarded.GetComponent<Collider2D>() : null;

        gs.boat.cargo = CargoManifest.Capture(boatRoot, volumeCol);
    }
}