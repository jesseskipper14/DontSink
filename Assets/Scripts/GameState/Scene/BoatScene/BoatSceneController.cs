using UnityEngine;
using UnityEngine.SceneManagement;

// FLAGGED FOR FIELD/METHOD CLEANUP

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

    [SerializeField] private PlayerLoadoutPersistence playerLoadoutPersistence;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    public TravelPayload Payload { get; private set; }

    private bool _completed;
    private bool _initialized;

    private DockTrigger _activeDockInRange;

    private void Reset()
    {
        ctx = FindAnyObjectByType<BoatSceneContext>();
        dockingPanel = FindAnyObjectByType<DockingActionPanel>();
        playerLoadoutPersistence = FindAnyObjectByType<PlayerLoadoutPersistence>();
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

        GameState gs = GameState.I;
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

        Log(
            $"Start | payload from='{Payload.fromNodeStableId}' to='{Payload.toNodeStableId}' " +
            $"boatId='{Payload.boatInstanceId}' boatGuid='{Payload.boatPrefabGuid}'");

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
            Vector3 p = ctx.sourceDockAnchor.position;
            p.x = sourceDockX;
            ctx.sourceDockAnchor.position = p;
        }

        float dist = baseTravelDistance * distanceScale;
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
        if (_activeDockInRange != trigger) return;

        if (dockingPanel != null)
            dockingPanel.Hide();

        Payload ??= GameState.I != null ? GameState.I.activeTravel : null;
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

        SceneTransitionController transition = SceneTransitionController.I;
        if (transition == null)
        {
            Debug.LogError("[BoatSceneController] SceneTransitionController missing on abort.");
            return;
        }

        transition.AbortTravelToSource();
    }

    private void CompleteTravelToDestination()
    {
        _completed = true;
        PersistBoatAndCargo();

        SceneTransitionController transition = SceneTransitionController.I;
        if (transition == null)
        {
            Debug.LogError("[BoatSceneController] SceneTransitionController missing on completion.");
            return;
        }

        transition.CompleteTravelToDestination();
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
        SceneTransitionController transition = SceneTransitionController.I;
        if (transition != null)
        {
            transition.SaveCurrentBoatState("BoatSceneController.PersistBoatAndCargo");
            return;
        }

        // Fallback if transition singleton is missing.
        PersistBoatAndCargoFallback();
    }

    private void PersistBoatAndCargoFallback()
    {
        GameState gs = GameState.I;
        if (gs == null) return;
        if (gs.boatRegistry == null) return;
        if (gs.boat == null) return;

        if (!gs.boatRegistry.TryGetById(gs.boat.boatInstanceId, out Boat boatObj) || boatObj == null)
        {
            LogWarning($"Persist fallback failed: no boat found for id='{gs.boat.boatInstanceId}'.");
            return;
        }

        Transform boatRoot = boatObj.transform;

        BoatIdentity boatId = boatRoot.GetComponent<BoatIdentity>();
        if (boatId != null)
            gs.boat.boatPrefabGuid = boatId.BoatGuid;

        BoatBoardedVolume boarded = boatRoot.GetComponentInChildren<BoatBoardedVolume>(true);
        Collider2D volumeCol = boarded != null ? boarded.GetComponent<Collider2D>() : null;

        gs.boat.cargo = CargoManifest.Capture(boatRoot, volumeCol);

        BoatLooseItemPersistence loosePersistence = boatObj.GetComponent<BoatLooseItemPersistence>();
        if (loosePersistence != null)
        {
            BoatLooseItemManifest manifest = loosePersistence.CaptureManifest();
            gs.SetBoatLooseItems(manifest, "BoatSceneController fallback capture");
        }
        else
        {
            LogWarning($"Persist fallback: boat '{boatObj.name}' has no BoatLooseItemPersistence.");
        }
    }

    private void Log(string msg)
    {
        if (!verboseLogging) return;
        Debug.Log($"[BoatSceneController] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging) return;
        Debug.LogWarning($"[BoatSceneController] {msg}", this);
    }
}