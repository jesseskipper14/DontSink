using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerLadderClimber : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LocalCharacterIntentSource localCharacterIntentSource;
    [SerializeField] private CharacterMotor2D motor;

    [Header("Detection")]
    [SerializeField] private LayerMask ladderMask = ~0;
    [SerializeField] private float ladderSearchRadius = 0.75f;

    [Header("Movement")]
    [SerializeField] private bool disableGravityWhileClimbing = true;
    [SerializeField] private float detachDistance = 1.25f;
    [SerializeField] private bool alignRotationToLadderWhileClimbing = true;
    [SerializeField] private float rotationSnapSpeed = 18f;

    [Header("Facing")]
    [SerializeField] private bool faceUsingHorizontalInput = true;

    [Header("Implicit Entry")]
    [Tooltip("Keep false unless deliberately testing old behavior. Intentional interaction should start ladder climbing.")]
    [SerializeField] private bool allowImplicitEntry = false;
    [SerializeField] private float implicitInputDeadzone = 0.01f;

    [Header("Exit")]
    [SerializeField] private bool jumpExitsLadder = true;
    [SerializeField] private bool groundedExitsAtTopOrBottom = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Rigidbody2D _rb;
    private float _originalGravityScale;
    private LadderZone _activeLadder;

    // Position tracked in ladder-local space while climbing.
    private Vector3 _ladderLocalClimbPosition;

    public bool IsClimbing => _activeLadder != null;
    public LadderZone ActiveLadder => _activeLadder;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        if (localCharacterIntentSource == null)
            localCharacterIntentSource = GetComponent<LocalCharacterIntentSource>();

        if (motor == null)
            motor = GetComponent<CharacterMotor2D>();
    }

    private void Update()
    {
        if (IsClimbing && !IsActiveLadderStillNearby())
        {
            EndClimb(keepVelocity: false);
            return;
        }

        if (!IsClimbing)
        {
            if (allowImplicitEntry)
                TryImplicitEnter();

            return;
        }

        HandleFacingWhileClimbing();

        if (jumpExitsLadder && localCharacterIntentSource != null && localCharacterIntentSource.Current.JumpPressed)
        {
            localCharacterIntentSource.ConsumeJumpPressed();
            EndClimb(keepVelocity: true);
        }
    }

    private void FixedUpdate()
    {
        if (!IsClimbing || localCharacterIntentSource == null || _activeLadder == null)
            return;

        CharacterIntent intent = localCharacterIntentSource.Current;

        float vertical = GetVerticalIntent(intent);
        float horizontal = intent.MoveX;

        Transform ladderFrame = _activeLadder.transform;

        // Move along the ladder's local vertical axis, not world Y.
        _ladderLocalClimbPosition.y += vertical * _activeLadder.ClimbSpeed * Time.fixedDeltaTime;

        // Smoothly snap local X toward the ladder climb-center's local X.
        float targetLocalX = ladderFrame.InverseTransformPoint(_activeLadder.ClimbCenter.position).x;

        _ladderLocalClimbPosition.x = Mathf.Lerp(
            _ladderLocalClimbPosition.x,
            targetLocalX,
            1f - Mathf.Exp(-_activeLadder.SnapToCenterSpeed * Time.fixedDeltaTime));

        Vector2 targetWorld = ladderFrame.TransformPoint(_ladderLocalClimbPosition);

        _rb.MovePosition(targetWorld);

        if (alignRotationToLadderWhileClimbing)
        {
            float targetRot = ladderFrame.eulerAngles.z;

            float snappedRot = Mathf.LerpAngle(
                _rb.rotation,
                targetRot,
                1f - Mathf.Exp(-rotationSnapSpeed * Time.fixedDeltaTime));

            _rb.MoveRotation(snappedRot);
            _rb.angularVelocity = 0f;
        }

        _rb.linearVelocity = Vector2.zero;

        HandleAutoExit(vertical, horizontal);
    }

    public bool CanBeginClimb(LadderZone ladder)
    {
        if (ladder == null)
            return false;

        if (IsClimbing)
            return false;

        if (!CanAccessLadderByBoatContext(ladder))
            return false;

        LadderZone bestNearby = FindBestNearbyLadder();
        return ReferenceEquals(bestNearby, ladder);
    }

    private bool CanAccessLadderByBoatContext(LadderZone ladder)
    {
        if (ladder == null)
            return false;

        Boat ladderBoat = ladder.GetComponentInParent<Boat>();
        if (ladderBoat == null)
            return true;

        PlayerBoardingState boardingState = GetComponent<PlayerBoardingState>();
        if (boardingState == null)
            return false;

        return boardingState.IsBoarded &&
               boardingState.CurrentBoatRoot == ladderBoat.transform;
    }

    public bool TryBeginClimb(LadderZone ladder)
    {
        if (!CanBeginClimb(ladder))
            return false;

        _activeLadder = ladder;
        _originalGravityScale = _rb.gravityScale;

        if (disableGravityWhileClimbing)
            _rb.gravityScale = 0f;

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        if (alignRotationToLadderWhileClimbing)
        {
            float ladderRot = ladder.transform.eulerAngles.z;
            _rb.rotation = ladderRot;
            _rb.angularVelocity = 0f;
        }

        Transform ladderFrame = ladder.transform;

        // Start from the player's current position in ladder-local space.
        _ladderLocalClimbPosition = ladderFrame.InverseTransformPoint(_rb.position);

        // Snap local X to climb center immediately on attach.
        float targetLocalX = ladderFrame.InverseTransformPoint(ladder.ClimbCenter.position).x;
        _ladderLocalClimbPosition.x = targetLocalX;

        Vector2 snappedWorld = ladderFrame.TransformPoint(_ladderLocalClimbPosition);
        _rb.position = snappedWorld;

        Log($"Begin climb on ladder={ladder.name}, localPos={_ladderLocalClimbPosition}");

        return true;
    }

    public void EndClimb(bool keepVelocity = false)
    {
        if (_activeLadder == null)
            return;

        Log($"End climb on ladder={_activeLadder.name}, keepVelocity={keepVelocity}");

        _activeLadder = null;

        if (disableGravityWhileClimbing)
            _rb.gravityScale = _originalGravityScale;

        if (!keepVelocity)
            _rb.linearVelocity = Vector2.zero;
    }

    private void TryImplicitEnter()
    {
        if (!allowImplicitEntry || localCharacterIntentSource == null)
            return;

        LadderZone best = FindBestNearbyLadder();
        if (best == null)
            return;

        CharacterIntent intent = localCharacterIntentSource.Current;
        float vertical = GetVerticalIntent(intent);

        if (Mathf.Abs(vertical) <= implicitInputDeadzone)
            return;

        if (best.RequireInteractToClimb)
            return;

        if (vertical < 0f && !best.AllowImplicitDownClimb)
            return;

        TryBeginClimb(best);
    }

    private LadderZone FindBestNearbyLadder()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ladderSearchRadius, ladderMask);

        LadderZone best = null;
        float bestScore = float.PositiveInfinity;

        Vector2 playerPos = transform.position;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            LadderZone ladder = hit.GetComponent<LadderZone>();
            if (ladder == null)
                ladder = hit.GetComponentInParent<LadderZone>();

            if (ladder == null)
                continue;

            if (!CanAccessLadderByBoatContext(ladder))
                continue;

            Vector2 closestPoint = ladder.GetClosestInteractionPoint(playerPos);

            float distance = Vector2.Distance(playerPos, closestPoint);

            // Slight bias toward horizontal alignment, but no center-origin nonsense.
            float climbCenterDx = Mathf.Abs(playerPos.x - ladder.ClimbCenter.position.x);
            float score = distance + climbCenterDx * 0.05f;

            if (score < bestScore)
            {
                bestScore = score;
                best = ladder;
            }
        }

        return best;
    }

    private bool IsActiveLadderStillNearby()
    {
        if (_activeLadder == null)
            return false;

        Vector2 closestPoint = _activeLadder.GetClosestInteractionPoint(transform.position);
        float distanceToLadder = Vector2.Distance(transform.position, closestPoint);

        if (distanceToLadder > detachDistance)
            return false;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ladderSearchRadius, ladderMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            LadderZone ladder = hit.GetComponent<LadderZone>();
            if (ladder == null)
                ladder = hit.GetComponentInParent<LadderZone>();

            if (ReferenceEquals(ladder, _activeLadder))
                return true;
        }

        return false;
    }

    private void HandleAutoExit(float vertical, float horizontal)
    {
        if (_activeLadder == null)
            return;

        if (!_activeLadder.TryGetWorldYBounds(out float minY, out float maxY))
            return;

        Vector2 pos = _rb.position;

        bool atTop = pos.y >= (maxY - _activeLadder.TopExitMargin);
        bool atBottom = pos.y <= (minY + _activeLadder.BottomExitMargin);

        if (_activeLadder.AllowTopExit && atTop && vertical > 0.01f)
        {
            if (_activeLadder.TopExitPoint != null)
                _rb.position = _activeLadder.TopExitPoint.position;

            EndClimb(keepVelocity: false);
            return;
        }

        if (_activeLadder.AllowBottomExit && atBottom && vertical < -0.01f)
        {
            if (_activeLadder.BottomExitPoint != null)
                _rb.position = _activeLadder.BottomExitPoint.position;

            EndClimb(keepVelocity: false);
            return;
        }

        if (groundedExitsAtTopOrBottom && motor != null)
        {
            motor.UpdateGrounded();

            if (motor.IsGrounded && (atTop || atBottom) && Mathf.Abs(vertical) <= 0.01f && Mathf.Abs(horizontal) > 0.01f)
            {
                EndClimb(keepVelocity: false);
            }
        }
    }

    private void HandleFacingWhileClimbing()
    {
        if (!faceUsingHorizontalInput || localCharacterIntentSource == null)
            return;

        float x = localCharacterIntentSource.Current.MoveX;
        if (x > 0.01f)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x);
            transform.localScale = s;
        }
        else if (x < -0.01f)
        {
            Vector3 s = transform.localScale;
            s.x = -Mathf.Abs(s.x);
            transform.localScale = s;
        }
    }

    private float GetVerticalIntent(CharacterIntent intent)
    {
        float up = intent.ClimbUpHeld ? 1f : 0f;
        float down = intent.ClimbDownHeld ? 1f : 0f;
        return up - down;
    }

    private void Log(string message)
    {
        if (!debugLogs)
            return;

        Debug.Log($"[PlayerLadderClimber:{name}] {message}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, ladderSearchRadius);

        if (_activeLadder != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _activeLadder.GetClosestInteractionPoint(transform.position));
        }
    }
#endif
}