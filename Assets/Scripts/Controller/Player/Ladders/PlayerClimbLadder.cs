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

    [Header("Facing")]
    [SerializeField] private bool faceUsingHorizontalInput = true;

    [Header("Implicit Entry")]
    [SerializeField] private bool allowImplicitEntry = true;
    [SerializeField] private float implicitInputDeadzone = 0.01f;

    [Header("Exit")]
    [SerializeField] private bool jumpExitsLadder = true;
    [SerializeField] private bool groundedExitsAtTopOrBottom = true;

    private Rigidbody2D _rb;
    private float _originalGravityScale;
    private LadderZone _activeLadder;

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
        if (!IsClimbing || localCharacterIntentSource == null)
            return;

        CharacterIntent intent = localCharacterIntentSource.Current;

        float vertical = GetVerticalIntent(intent);
        float horizontal = intent.MoveX;

        Vector2 currentPos = _rb.position;
        float targetX = _activeLadder.ClimbCenter.position.x;

        float snappedX = Mathf.Lerp(
            currentPos.x,
            targetX,
            1f - Mathf.Exp(-_activeLadder.SnapToCenterSpeed * Time.fixedDeltaTime));

        float newY = currentPos.y + (vertical * _activeLadder.ClimbSpeed * Time.fixedDeltaTime);

        _rb.MovePosition(new Vector2(snappedX, newY));

        HandleAutoExit(vertical, horizontal);
    }

    public bool CanBeginClimb(LadderZone ladder)
    {
        if (ladder == null)
            return false;

        if (IsClimbing)
            return false;

        LadderZone bestNearby = FindBestNearbyLadder();
        return ReferenceEquals(bestNearby, ladder);
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

        Vector2 startPos = _rb.position;
        startPos.x = ladder.ClimbCenter.position.x;
        _rb.position = startPos;

        return true;
    }

    public void EndClimb(bool keepVelocity = false)
    {
        if (_activeLadder == null)
            return;

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

            float dx = Mathf.Abs(transform.position.x - ladder.ClimbCenter.position.x);
            float dy = Mathf.Abs(transform.position.y - ladder.transform.position.y);
            float score = dx + (dy * 0.15f);

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

        float distToCenterX = Mathf.Abs(transform.position.x - _activeLadder.ClimbCenter.position.x);
        if (distToCenterX > detachDistance)
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, ladderSearchRadius);

        if (_activeLadder != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _activeLadder.ClimbCenter.position);
        }
    }
#endif
}