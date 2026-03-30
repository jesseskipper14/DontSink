using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class LadderZone : MonoBehaviour, IInteractable
{
    [Header("Climb")]
    [SerializeField] private float climbSpeed = 3.25f;
    [SerializeField] private float snapToCenterSpeed = 18f;
    [SerializeField] private bool requireInteractToClimb = false;
    [SerializeField] private bool allowImplicitDownClimb = true;

    [Header("Exit")]
    [SerializeField] private bool allowTopExit = true;
    [SerializeField] private bool allowBottomExit = true;
    [SerializeField] private float topExitMargin = 0.15f;
    [SerializeField] private float bottomExitMargin = 0.15f;
    [SerializeField] private Transform topExitPoint;
    [SerializeField] private Transform bottomExitPoint;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 25;
    [SerializeField] private float maxInteractDistance = 1.5f;

    [Header("Centering")]
    [SerializeField] private Transform climbCenter;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    public int InteractionPriority => interactionPriority;
    public float ClimbSpeed => climbSpeed;
    public float SnapToCenterSpeed => snapToCenterSpeed;
    public bool RequireInteractToClimb => requireInteractToClimb;
    public bool AllowImplicitDownClimb => allowImplicitDownClimb;
    public bool AllowTopExit => allowTopExit;
    public bool AllowBottomExit => allowBottomExit;
    public float TopExitMargin => Mathf.Max(0f, topExitMargin);
    public float BottomExitMargin => Mathf.Max(0f, bottomExitMargin);
    public Transform TopExitPoint => topExitPoint;
    public Transform BottomExitPoint => bottomExitPoint;
    public Transform ClimbCenter => climbCenter != null ? climbCenter : transform;

    public bool CanInteract(in InteractContext context)
    {
        if (!requireInteractToClimb)
        {
            Log("CanInteract = false because requireInteractToClimb is false.");
            return false;
        }

        if (context.InteractorGO == null)
        {
            Log("CanInteract = false because interactor GO was null.");
            return false;
        }

        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxInteractDistance)
        {
            Log($"CanInteract = false because distance {dist:0.00} > max {maxInteractDistance:0.00}.");
            return false;
        }

        var climber = context.InteractorGO.GetComponentInChildren<PlayerLadderClimber>(true);
        bool can = climber != null && climber.CanBeginClimb(this);
        Log($"CanInteract evaluated. climberFound={climber != null}, result={can}");
        return can;
    }

    public void Interact(in InteractContext context)
    {
        Log($"Interact called by '{context.InteractorGO?.name ?? "NULL"}'.");

        if (context.InteractorGO == null)
            return;

        var climber = context.InteractorGO.GetComponentInChildren<PlayerLadderClimber>(true);
        if (climber == null)
        {
            Log("Interact aborted. No PlayerLadderClimber found on interactor.");
            return;
        }

        climber.TryBeginClimb(this);
    }

    public bool TryGetWorldYBounds(out float minY, out float maxY)
    {
        minY = 0f;
        maxY = 0f;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Log("TryGetWorldYBounds failed because no Collider2D was found.");
            return false;
        }

        Bounds b = col.bounds;
        minY = b.min.y;
        maxY = b.max.y;
        return true;
    }

    private void Log(string message)
    {
        if (!debugLogs)
            return;

        Debug.Log($"[LadderZone:{name}] {message}", this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.9f);

        var c = ClimbCenter;
        Gizmos.DrawWireSphere(c.position, 0.08f);

        if (TryGetWorldYBounds(out float minY, out float maxY))
        {
            Vector3 a = new Vector3(c.position.x, minY, c.position.z);
            Vector3 b = new Vector3(c.position.x, maxY, c.position.z);
            Gizmos.DrawLine(a, b);
        }

        if (topExitPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(topExitPoint.position, 0.08f);
        }

        if (bottomExitPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(bottomExitPoint.position, 0.08f);
        }
    }
#endif
}