using UnityEngine;

/// <summary>
/// Last-resort generated-ground OOB safety for dynamic bodies that must never
/// remain below the authored/generated surface (the player is the first user).
///
/// This is not ordinary grounding or collision response. It only corrects a
/// body after some teleport/transfer/physics failure has already left its main
/// solid collider below the generated ground edge.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class GeneratedGroundPenetrationGuard2D : MonoBehaviour
{
    [Header("Gameplay Authority")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("Ground Safety")]
    [SerializeField] private GeneratedGroundSampler2D groundSampler;
    [SerializeField] private Collider2D primarySolidCollider;

    [Tooltip("Minimum vertical gap restored between the collider bottom and generated ground after a rescue.")]
    [SerializeField, Min(0f)] private float rescueClearance = 0.035f;

    [Tooltip("Small penetration tolerated so normal contact-solver overlap on slopes does not cause constant micro-snaps.")]
    [SerializeField, Min(0f)] private float penetrationTolerance = 0.04f;

    [Tooltip("Bell occupants are allowed to exist inside bell geometry near/below the sampled floor. Normal bell exit selection handles their safe return to world space.")]
    [SerializeField] private bool suppressWhileInsideDivingBell = true;

    [Tooltip("After a rescue, discard downward velocity so the next physics step does not immediately drive the body back through the floor.")]
    [SerializeField] private bool cancelDownwardVelocityOnRescue = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Rigidbody2D _body;
    private PlayerBellOccupantState _bellOccupantState;
    private float _nextGroundSamplerResolveTime;

    private void Awake()
    {
        ResolveRefs();
    }

    private void Reset()
    {
        ResolveRefs();
    }

    private void OnValidate()
    {
        rescueClearance = Mathf.Max(0f, rescueClearance);
        penetrationTolerance = Mathf.Max(0f, penetrationTolerance);
    }

    private void FixedUpdate()
    {
        if (!GameplayAuthority.CanRun(gameplayAuthorityMode))
            return;

        ResolveRefs();

        if (_body == null ||
            primarySolidCollider == null ||
            groundSampler == null)
        {
            return;
        }

        if (suppressWhileInsideDivingBell &&
            _bellOccupantState != null &&
            _bellOccupantState.IsInsideBell)
        {
            return;
        }

        Physics2D.SyncTransforms();

        Vector2 colliderCenter = primarySolidCollider.bounds.center;

        if (!GeneratedGroundClearanceUtility2D.TryGetRequiredUpwardCorrection(
                groundSampler,
                primarySolidCollider,
                colliderCenter,
                rescueClearance,
                out float correction,
                out float groundY))
        {
            return;
        }

        if (correction <= penetrationTolerance)
            return;

        _body.position += Vector2.up * correction;

        if (cancelDownwardVelocityOnRescue &&
            _body.linearVelocity.y < 0f)
        {
            Vector2 velocity = _body.linearVelocity;
            velocity.y = 0f;
            _body.linearVelocity = velocity;
        }

        Physics2D.SyncTransforms();

        if (verboseLogging)
        {
            Debug.LogWarning(
                $"[GeneratedGroundPenetrationGuard2D:{name}] Rescued body above generated ground. " +
                $"correction={correction:F3} groundY={groundY:F3}",
                this);
        }
    }

    private void ResolveRefs()
    {
        if (_body == null)
            _body = GetComponent<Rigidbody2D>();

        if (groundSampler == null &&
            Time.unscaledTime >= _nextGroundSamplerResolveTime)
        {
            groundSampler = FindAnyObjectByType<GeneratedGroundSampler2D>();
            _nextGroundSamplerResolveTime = Time.unscaledTime + 1f;
        }

        if (_bellOccupantState == null)
        {
            _bellOccupantState =
                GetComponent<PlayerBellOccupantState>() ??
                GetComponentInParent<PlayerBellOccupantState>() ??
                GetComponentInChildren<PlayerBellOccupantState>(true);
        }

        if (primarySolidCollider == null)
            primarySolidCollider = ResolvePrimarySolidCollider();
    }

    private Collider2D ResolvePrimarySolidCollider()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

        Collider2D best = null;
        float bestArea = -1f;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];

            if (collider == null ||
                !collider.enabled ||
                collider.isTrigger)
            {
                continue;
            }

            if (_body != null &&
                collider.attachedRigidbody != _body)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            float area = Mathf.Abs(bounds.size.x * bounds.size.y);

            if (area <= bestArea)
                continue;

            bestArea = area;
            best = collider;
        }

        return best;
    }
}
