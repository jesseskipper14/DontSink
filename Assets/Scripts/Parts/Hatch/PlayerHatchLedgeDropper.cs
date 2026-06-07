using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerHatchLedgeDropper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LocalCharacterIntentSource localCharacterIntentSource;
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private PlayerBoardingState boardingState;

    [Header("Detection")]
    [Tooltip("Layers containing ledges that can be dropped through. Include HatchLedge and WorldLedge.")]
    [SerializeField] private LayerMask ledgeMask;

    [SerializeField, Min(0.01f)]
    private float ledgeDetectRadiusPadding = 0.08f;

    [Tooltip("If true, generic PlatformEffector2D ledges can be dropped through even without a HatchLedge component.")]
    [SerializeField] private bool allowPlatformEffectorLedges = true;

    [Tooltip("If true, generic colliders on ledgeMask can be dropped through even without HatchLedge or PlatformEffector2D.")]
    [SerializeField] private bool allowAnyColliderOnLedgeMask = true;

    [Header("Boarding Rules")]
    [Tooltip("If true, while boarded only boat hatch ledges are valid. WorldLedge docks are ignored.")]
    [SerializeField] private bool boardedRequiresHatchLedgeComponent = true;

    [Header("Drop Through")]
    [SerializeField, Min(0.05f)]
    private float ignoreCollisionSeconds = 0.35f;

    [SerializeField, Min(0f)]
    private float downwardNudgeVelocity = 0.75f;

    [SerializeField] private bool requireGroundedOnLedge = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Rigidbody2D _rb;
    private Collider2D[] _playerColliders;
    private bool _dropInProgress;

    private readonly List<Collider2D> _ignoredThisDrop = new();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        if (localCharacterIntentSource == null)
            localCharacterIntentSource = GetComponent<LocalCharacterIntentSource>();

        if (motor == null)
            motor = GetComponent<CharacterMotor2D>();

        if (boardingState == null)
        {
            boardingState =
                GetComponent<PlayerBoardingState>() ??
                GetComponentInParent<PlayerBoardingState>() ??
                GetComponentInChildren<PlayerBoardingState>(true);
        }

        _playerColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void FixedUpdate()
    {
        if (_dropInProgress)
            return;

        if (localCharacterIntentSource == null)
            return;

        CharacterIntent intent = localCharacterIntentSource.Current;

        if (!intent.ClimbDownHeld)
            return;

        TryDropThroughLedge();
    }

    private void TryDropThroughLedge()
    {
        if (motor != null)
            motor.UpdateGrounded();

        if (requireGroundedOnLedge && motor != null && !motor.IsGrounded)
            return;

        Vector2 checkPos = motor != null
            ? (Vector2)transform.position + motor.groundCheckLocalOffset
            : _rb.position;

        float radius = motor != null
            ? motor.groundCheckRadius + ledgeDetectRadiusPadding
            : 0.25f;

        LayerMask mask = ledgeMask.value != 0
            ? ledgeMask
            : motor != null
                ? motor.groundMask
                : ~0;

        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPos, radius, mask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !hit.enabled || hit.isTrigger)
                continue;

            if (!TryResolveDropCollider(hit, out Collider2D ledgeCollider, out string ledgeKind))
                continue;

            if (ledgeCollider == null || !ledgeCollider.enabled)
                continue;

            StartCoroutine(DropThroughRoutine(ledgeCollider, ledgeKind));
            return;
        }
    }

    private bool TryResolveDropCollider(
        Collider2D hit,
        out Collider2D ledgeCollider,
        out string ledgeKind)
    {
        ledgeCollider = null;
        ledgeKind = string.Empty;

        HatchLedge hatchLedge = hit.GetComponent<HatchLedge>();
        if (hatchLedge == null)
            hatchLedge = hit.GetComponentInParent<HatchLedge>();

        if (hatchLedge != null)
        {
            ledgeCollider = hatchLedge.Collider != null ? hatchLedge.Collider : hit;
            ledgeKind = "HatchLedge";
            return true;
        }

        // While boarded, avoid treating world dock ledges as valid drop-through targets.
        // Boarded player should be ignoring WorldLedge entirely via PlayerBoardingState anyway,
        // but this prevents the dropper from trying to helpfully sabotage us.
        if (boardedRequiresHatchLedgeComponent &&
            boardingState != null &&
            boardingState.IsBoarded)
        {
            return false;
        }

        if (allowPlatformEffectorLedges)
        {
            PlatformEffector2D effector =
                hit.GetComponent<PlatformEffector2D>() ??
                hit.GetComponentInParent<PlatformEffector2D>();

            if (effector != null)
            {
                ledgeCollider = hit;
                ledgeKind = "PlatformEffector2D";
                return true;
            }
        }

        if (allowAnyColliderOnLedgeMask && IsInLedgeMask(hit.gameObject.layer))
        {
            ledgeCollider = hit;
            ledgeKind = "LedgeMaskCollider";
            return true;
        }

        return false;
    }

    private IEnumerator DropThroughRoutine(Collider2D ledgeCollider, string ledgeKind)
    {
        _dropInProgress = true;
        _ignoredThisDrop.Clear();

        if (_playerColliders == null || _playerColliders.Length == 0)
            _playerColliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < _playerColliders.Length; i++)
        {
            Collider2D playerCol = _playerColliders[i];
            if (playerCol == null || ledgeCollider == null)
                continue;

            Physics2D.IgnoreCollision(playerCol, ledgeCollider, true);
            _ignoredThisDrop.Add(playerCol);
        }

        Vector2 v = _rb.linearVelocity;
        if (v.y > -downwardNudgeVelocity)
            v.y = -downwardNudgeVelocity;

        _rb.linearVelocity = v;

        if (debugLogs)
        {
            Debug.Log(
                $"[PlayerHatchLedgeDropper:{name}] Dropping through {ledgeKind} '{ledgeCollider.name}'.",
                this);
        }

        yield return new WaitForSeconds(ignoreCollisionSeconds);

        if (ledgeCollider != null)
        {
            for (int i = 0; i < _ignoredThisDrop.Count; i++)
            {
                Collider2D playerCol = _ignoredThisDrop[i];
                if (playerCol == null)
                    continue;

                Physics2D.IgnoreCollision(playerCol, ledgeCollider, false);
            }
        }

        _ignoredThisDrop.Clear();
        _dropInProgress = false;
    }

    private bool IsInLedgeMask(int layer)
    {
        if (layer < 0)
            return false;

        return (ledgeMask.value & (1 << layer)) != 0;
    }
}