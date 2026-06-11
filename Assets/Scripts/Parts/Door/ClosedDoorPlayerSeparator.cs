using UnityEngine;

[DisallowMultipleComponent]
public sealed class ClosedDoorPlayerSeparator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DoorRuntime doorRuntime;
    [SerializeField] private Collider2D separatorTrigger;
    [SerializeField] private Collider2D blockingCollider;

    [Header("Player Filtering")]
    [SerializeField] private LayerMask playerMask;

    [Tooltip("If true, only affects players boarded on the same boat as this door.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    [Header("Separation")]
    [Tooltip("Local direction treated as the door normal. For normal vertical doors, local +X is usually correct.")]
    [SerializeField] private Vector2 localDoorNormal = Vector2.right;

    [Tooltip("If true, flips the local door normal.")]
    [SerializeField] private bool invertNormal = false;

    [Tooltip("Velocity into the closed door is removed so the player cannot keep driving force into the blocker.")]
    [SerializeField] private bool zeroVelocityIntoDoor = true;

    [Tooltip("Small positional nudge away from the door when the player is pushing into it.")]
    [Min(0f)]
    [SerializeField] private float pushAwayDistance = 0.025f;

    [Tooltip("Extra nudge if the player somehow overlaps the physical blocking collider.")]
    [Min(0f)]
    [SerializeField] private float overlapEscapeDistance = 0.06f;

    [Tooltip("Velocity dot threshold before we consider the player to be pushing into the door.")]
    [Min(0f)]
    [SerializeField] private float minIntoDoorSpeed = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool logCorrections = false;

    private Boat _owningBoat;

    private void Reset()
    {
        ResolveRefs();

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            playerMask = 1 << playerLayer;

        if (separatorTrigger != null)
            separatorTrigger.isTrigger = true;
    }

    private void Awake()
    {
        ResolveRefs();

        if (playerMask.value == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                playerMask = 1 << playerLayer;
        }

        if (separatorTrigger != null)
            separatorTrigger.isTrigger = true;
    }

    private void ResolveRefs()
    {
        if (doorRuntime == null)
            doorRuntime = GetComponentInParent<DoorRuntime>();

        if (separatorTrigger == null)
            separatorTrigger = GetComponent<Collider2D>();

        if (doorRuntime != null && blockingCollider == null && doorRuntime.Authoring != null)
            blockingCollider = doorRuntime.Authoring.BlockingCollider;

        if (_owningBoat == null)
            _owningBoat = GetComponentInParent<Boat>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TrySeparate(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TrySeparate(other);
    }

    private void TrySeparate(Collider2D other)
    {
        if (doorRuntime == null || doorRuntime.IsOpen)
            return;

        if (separatorTrigger == null || !separatorTrigger.enabled)
            return;

        if (other == null)
            return;

        if ((playerMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        Rigidbody2D rb = other.attachedRigidbody;
        if (rb == null)
            return;

        if (!CanAffectPlayer(other))
            return;

        Vector2 awayNormal = GetAwayNormal(other);
        Vector2 velocity = GetVelocity(rb);

        float intoDoorSpeed = Vector2.Dot(velocity, awayNormal);
        bool movingIntoDoor = intoDoorSpeed < -minIntoDoorSpeed;
        bool overlappingBlocker = IsOverlappingBlockingCollider(other);

        if (!movingIntoDoor && !overlappingBlocker)
            return;

        if (zeroVelocityIntoDoor && intoDoorSpeed < 0f)
        {
            velocity -= awayNormal * intoDoorSpeed;
            SetVelocity(rb, velocity);
        }

        float correctionDistance = movingIntoDoor ? pushAwayDistance : 0f;

        if (overlappingBlocker)
            correctionDistance = Mathf.Max(correctionDistance, overlapEscapeDistance);

        if (correctionDistance > 0f)
            rb.position += awayNormal * correctionDistance;

        if (logCorrections)
        {
            Debug.Log(
                $"[ClosedDoorPlayerSeparator:{name}] Corrected '{other.name}' " +
                $"movingIntoDoor={movingIntoDoor} overlappingBlocker={overlappingBlocker} " +
                $"away={awayNormal} correction={correctionDistance:F3}",
                this);
        }
    }

    private bool CanAffectPlayer(Collider2D playerCollider)
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        if (_owningBoat == null)
            return true;

        PlayerBoardingState boarding =
            playerCollider.GetComponentInParent<PlayerBoardingState>();

        if (boarding == null)
            return false;

        if (!boarding.IsBoarded)
            return false;

        return boarding.CurrentBoatRoot == _owningBoat.transform;
    }

    private Vector2 GetAwayNormal(Collider2D playerCollider)
    {
        Vector2 normal = transform.TransformDirection(localDoorNormal.normalized);

        if (invertNormal)
            normal = -normal;

        Collider2D referenceCollider = blockingCollider != null ? blockingCollider : separatorTrigger;

        Vector2 doorCenter = referenceCollider != null
            ? (Vector2)referenceCollider.bounds.center
            : (Vector2)transform.position;

        Vector2 playerCenter = playerCollider.bounds.center;
        float side = Vector2.Dot(playerCenter - doorCenter, normal);

        if (Mathf.Abs(side) < 0.0001f)
            side = 1f;

        return side >= 0f ? normal : -normal;
    }

    private bool IsOverlappingBlockingCollider(Collider2D playerCollider)
    {
        if (blockingCollider == null || !blockingCollider.enabled)
            return false;

        ColliderDistance2D distance = playerCollider.Distance(blockingCollider);
        return distance.isOverlapped;
    }

    private static Vector2 GetVelocity(Rigidbody2D rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private static void SetVelocity(Rigidbody2D rb, Vector2 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (separatorTrigger == null)
            separatorTrigger = GetComponent<Collider2D>();

        if (separatorTrigger != null)
            separatorTrigger.isTrigger = true;

        if (localDoorNormal.sqrMagnitude < 0.0001f)
            localDoorNormal = Vector2.right;

        pushAwayDistance = Mathf.Max(0f, pushAwayDistance);
        overlapEscapeDistance = Mathf.Max(0f, overlapEscapeDistance);
        minIntoDoorSpeed = Mathf.Max(0f, minIntoDoorSpeed);
    }
#endif
}