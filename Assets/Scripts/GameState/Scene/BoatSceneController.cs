using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BoatSceneController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string nodeSceneName = "NodeScene";

    [Header("Scene Anchors")]
    [SerializeField] private BoatSceneContext ctx;

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

    private void Reset()
    {
        ctx = FindAnyObjectByType<BoatSceneContext>();
    }

    private void OnEnable()
    {
        EnsureContext();
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

    private void HookDockEvents()
    {
        if (ctx == null) return;

        // Idempotent: avoid double subscriptions.
        if (ctx.sourceDockTrigger != null)
            ctx.sourceDockTrigger.OnDocked -= OnDocked;
        if (ctx.targetDockTrigger != null)
            ctx.targetDockTrigger.OnDocked -= OnDocked;

        if (ctx.sourceDockTrigger != null)
            ctx.sourceDockTrigger.OnDocked += OnDocked;
        if (ctx.targetDockTrigger != null)
            ctx.targetDockTrigger.OnDocked += OnDocked;
    }

    private void UnhookDockEvents()
    {
        if (ctx == null) return;

        if (ctx.sourceDockTrigger != null)
            ctx.sourceDockTrigger.OnDocked -= OnDocked;
        if (ctx.targetDockTrigger != null)
            ctx.targetDockTrigger.OnDocked -= OnDocked;
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

    private void OnDocked(DockTrigger trigger, Collider2D other)
    {
        if (_completed) return;

        // Payload might not be set yet if a trigger fires extremely early.
        // Resolve lazily to be safe.
        if (Payload == null)
            Payload = GameState.I != null ? GameState.I.activeTravel : null;

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
        Payload = GameState.I != null ? GameState.I.activeTravel : Payload;
        if (GameState.I == null || Payload == null)
        {
            Debug.LogError("[BoatSceneController] Missing GameState/Payload.");
            return;
        }

        CompleteTravelToDestination();
    }
}