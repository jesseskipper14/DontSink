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

    [Header("Boat Spawn")]
    [SerializeField] private GameObject boatPrefab; // TEMP: swap to your real boat spawn system later

    public TravelPayload Payload { get; private set; }

    private bool _completed;

    private void Reset()
    {
        if (ctx == null) ctx = FindAnyObjectByType<BoatSceneContext>();
    }

    private void Start()
    {
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

        if (ctx == null)
        {
            Debug.LogError("[BoatSceneController] Missing BoatSceneContext in BoatScene.");
            return;
        }

        LayoutDocks();
        SpawnBoatIfNeeded();

        // Subscribe to docking events
        if (ctx.targetDockTrigger != null)
            ctx.targetDockTrigger.OnDocked += OnDocked;
        else
            Debug.LogWarning("[BoatSceneController] targetDockTrigger not set; cannot complete travel.");
    }

    private void OnDestroy()
    {
        if (ctx != null && ctx.targetDockTrigger != null)
            ctx.targetDockTrigger.OnDocked -= OnDocked;
    }

    private void LayoutDocks()
    {
        // Source dock near start (behind)
        if (ctx.sourceDockAnchor != null)
        {
            var p = ctx.sourceDockAnchor.position;
            p.x = sourceDockX;
            ctx.sourceDockAnchor.position = p;
        }

        // Target dock at scaled distance
        float dist = baseTravelDistance * distanceScale;

        if (ctx.targetDockAnchor != null)
        {
            var p = ctx.targetDockAnchor.position;
            p.x = dist;
            ctx.targetDockAnchor.position = p;
        }
    }

    private void SpawnBoatIfNeeded()
    {
        // TEMP: keep it simple. Later your BoatRegistry + boat instance id drives this.
        if (boatPrefab == null) return;
        if (ctx.playerSpawn == null) return;

        // If you already spawn boat elsewhere, delete this and wire in your real spawner.
        var boat = Instantiate(boatPrefab, ctx.playerSpawn.position, Quaternion.identity);
        boat.tag = "Boat"; // ensure matches DockTrigger.requiredTag (or set in prefab)
    }

    private void OnDocked(DockTrigger trigger, Collider2D other)
    {
        if (_completed) return;
        if (trigger.kind != DockTrigger.DockKind.Destination) return;

        CompleteTravelToDestination();
    }

    private void CompleteTravelToDestination()
    {
        _completed = true;

        var gs = GameState.I;
        if (gs == null || Payload == null)
        {
            Debug.LogError("[BoatSceneController] Missing GameState/Payload on completion.");
            return;
        }

        gs.player.currentNodeId = Payload.toNodeStableId;
        gs.ClearTravel();

        SceneManager.LoadScene(nodeSceneName);
    }

    [ContextMenu("DEBUG: Dock to Source (Abort Travel)")]
    public void DebugDockToSource()
    {
        var gs = GameState.I;
        if (gs == null || Payload == null)
        {
            Debug.LogError("[BoatSceneController] Missing GameState/Payload.");
            return;
        }

        gs.player.currentNodeId = Payload.fromNodeStableId;
        gs.ClearTravel();
        SceneManager.LoadScene(nodeSceneName);
    }

    [ContextMenu("DEBUG: Dock to Destination (Complete Travel)")]
    public void DebugDockToDestination()
    {
        var gs = GameState.I;
        if (gs == null || Payload == null)
        {
            Debug.LogError("[BoatSceneController] Missing GameState/Payload.");
            return;
        }

        gs.player.currentNodeId = Payload.toNodeStableId;
        gs.ClearTravel();
        SceneManager.LoadScene(nodeSceneName);
    }
}


