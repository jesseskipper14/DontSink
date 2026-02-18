using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BoatBoardingInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private int priority = 60;
    [SerializeField] private float maxUseDistance = 1.8f;

    [Header("Boarding")]
    [SerializeField] private Transform boatRoot;
    [SerializeField] private Transform boardPoint;
    [SerializeField] private Transform unboardPoint;
    [SerializeField] private bool parentPlayerToBoat = true;

    [SerializeField] private Vector2 postSnapNudge = Vector2.zero;

    public int InteractionPriority => priority;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        if (boatRoot == null) boatRoot = transform.root;
        if (boardPoint == null) boardPoint = transform;
        if (unboardPoint == null) unboardPoint = transform;
    }

    public bool CanInteract(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxUseDistance) return false;

        // If boarded, only allow toggling off if they are boarded to THIS boat.
        var boarding = context.InteractorGO.GetComponent<PlayerBoardingState>();
        if (boarding != null && boarding.IsBoarded)
            return boarding.CurrentBoatRoot == boatRoot;

        return true;
    }

    public void Interact(in InteractContext context)
    {
        var go = context.InteractorGO;
        var t = context.InteractorTransform;
        if (go == null || t == null) return;

        var boarding = go.GetComponent<PlayerBoardingState>();
        if (boarding == null)
        {
            Debug.LogWarning($"'{go.name}' missing PlayerBoardingState.");
            return;
        }

        if (!boarding.IsBoarded)
        {
            // BOARD
            SnapTo(t, boardPoint);
            if (parentPlayerToBoat && boatRoot != null)
                t.SetParent(boatRoot, worldPositionStays: true);

            ZeroVelocity(go);
            boarding.Board(boatRoot);
        }
        else
        {
            // UNBOARD
            // (only allowed for same boat via CanInteract)
            t.SetParent(null, worldPositionStays: true);
            SnapTo(t, unboardPoint);

            ZeroVelocity(go);
            boarding.Unboard();
        }
    }

    private void SnapTo(Transform player, Transform point)
    {
        Vector3 p = point != null ? point.position : transform.position;
        player.position = p + (Vector3)postSnapNudge;
    }

    private static void ZeroVelocity(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }
}
