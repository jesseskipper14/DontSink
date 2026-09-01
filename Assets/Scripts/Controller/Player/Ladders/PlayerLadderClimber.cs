using System.Collections;
using System.Collections.Generic;
using Survival.Attributes;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerLadderClimber : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LocalCharacterIntentSource localCharacterIntentSource;
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private PlayerExertionEnergyState exertionEnergy;
    [SerializeField] private PlayerAttributeState attributes;

    [Header("Detection")]
    [SerializeField] private LayerMask ladderMask = ~0;
    [SerializeField] private float ladderSearchRadius = 0.75f;

    [Header("Movement")]
    [SerializeField] private bool disableGravityWhileClimbing = true;
    [SerializeField] private float detachDistance = 1.25f;

    [Header("Ladder-Local Movement")]
    [SerializeField] private bool allowHorizontalMovementWhileClimbing = true;

    [SerializeField, Min(0f)]
    private float horizontalClimbSpeed = 2.25f;

    [Header("Exertion / Energy")]
    [SerializeField] private bool useLadderExertion = true;

    [Tooltip("Exertion target while hanging/paused on a ladder. Mild but not totally free.")]
    [SerializeField, Range(0f, 1f)]
    private float idleOnLadderExertionCeiling = 0.16f;

    [Tooltip("Exertion target while climbing upward. Keep above PlayerExertionEnergyState.drainThreshold if climbing should drain energy.")]
    [SerializeField, Range(0f, 1f)]
    private float climbUpExertionCeiling = 0.92f;

    [Tooltip("Exertion target while climbing downward.")]
    [SerializeField, Range(0f, 1f)]
    private float climbDownExertionCeiling = 0.38f;

    [Tooltip("Exertion target while shimmying sideways on a ladder.")]
    [SerializeField, Range(0f, 1f)]
    private float horizontalClimbExertionCeiling = 0.48f;

    [SerializeField, Min(0f)]
    private float ladderExertionApproachRate = 1.4f;

    [Header("Climb Authority From Energy")]
    [SerializeField] private bool scaleUpwardClimbByEnergy = true;
    [SerializeField] private bool scaleHorizontalClimbByEnergy = true;
    [SerializeField] private bool scaleDownwardClimbByEnergy = false;

    [Tooltip("Minimum climb authority even at zero energy. Set to 0 if exhaustion should fully stop climbing.")]
    [SerializeField, Range(0f, 1f)]
    private float minClimbAuthorityWhenExhausted = 0.25f;

    [Tooltip("How far sideways from the ladder center the player may drift while still attached.")]
    [SerializeField, Min(0.01f)]
    private float maxAttachedLocalXDistance = 0.65f;

    [Tooltip("If true, softly pulls the player toward ladder center only when no horizontal input is held.")]
    [SerializeField] private bool recenterWhenNoHorizontalInput = true;

    [SerializeField, Min(0f)]
    private float idleRecenterSpeed = 6f;

    [Tooltip("If true, the player is immediately moved to the ladder centerline when climbing starts.")]
    [SerializeField] private bool snapToCenterOnBeginClimb = false;

    [Tooltip("When not snapping to center on attach, clamp initial horizontal offset to this fraction of max attached distance.")]
    [SerializeField, Range(0.1f, 1f)]
    private float initialAttachClampFraction = 0.85f;

    [Header("Side Detach")]
    [Tooltip("Velocity applied when the player drifts far enough sideways to detach.")]
    [SerializeField, Min(0f)]
    private float sideDetachVelocity = 1.25f;

    [Header("Rotation")]
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

    [Header("Climb Collision Safety")]
    [SerializeField] private bool blockClimbIntoSolid = true;

    [Tooltip("Layers that block ladder climbing movement. If empty, uses CharacterMotor2D.groundMask.")]
    [SerializeField] private LayerMask climbBlockMask;

    [SerializeField, Min(0.001f)] private float climbBlockSkin = 0.025f;
    [SerializeField, Min(1)] private int climbCastMaxHits = 8;
    [SerializeField] private bool debugClimbBlock = false;

    [Tooltip("While climbing an exterior ladder on a boat from outside, temporarily ignore collisions between the player and that boat's solid colliders.")]
    [SerializeField] private bool ignoreBoatCollisionsWhileExternallyClimbing = true;

    [Header("Hatch Ledge Collision Safety")]
    [SerializeField] private bool ignoreHatchLedgeLayerWhileClimbing = true;

    [Tooltip("GameObject layer used by HatchLedge colliders.")]
    [SerializeField] private string hatchLedgeLayerName = "HatchLedge";

    [Tooltip("Minimum time after leaving a ladder before HatchLedge collision can restore. Actual restore waits until the player is no longer penetrating a ledge.")]
    [SerializeField, Min(0f)]
    private float hatchLedgeMinimumIgnoreAfterDetachSeconds = 0.05f;

    [Tooltip("Extra bounds padding used while checking whether the player is still penetrating a hatch ledge.")]
    [SerializeField, Min(0f)]
    private float hatchLedgeOverlapCheckPadding = 0.05f;

    [Tooltip("Small negative distance tolerance for Physics2D.Distance before treating the player as penetrating the ledge.")]
    [SerializeField, Min(0f)]
    private float hatchLedgePenetrationTolerance = 0.002f;

    [Tooltip("Safety cap. If the player remains penetrating longer than this, keep ignoring HatchLedge and log a warning instead of forcing a physics explosion.")]
    [SerializeField, Min(0.25f)]
    private float maxHatchLedgeRestoreWaitSeconds = 3f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Rigidbody2D _rb;
    private float _originalGravityScale;
    private LadderZone _activeLadder;
    private RaycastHit2D[] _climbCastHits;

    private int _hatchLedgeLayer = -1;
    private int _hatchLedgeBit = 0;
    private int _excludeLayersBeforeClimb = 0;
    private bool _hasExcludeLayersBeforeClimb;
    private Coroutine _restoreHatchLedgeLayerRoutine;

    private Collider2D[] _playerColliders;
    private Collider2D[] _hatchLedgeOverlapBuffer;

    private readonly List<IgnoredBoatCollisionPair> _ignoredExternalBoatCollisions = new();
    private Coroutine _restoreExternalBoatCollisionsRoutine;
    private Boat _externalClimbBoat;
    private Rigidbody2D _externalClimbBoatBody;

    private struct IgnoredBoatCollisionPair
    {
        public Collider2D PlayerCollider;
        public Collider2D BoatCollider;
    }

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

        if (exertionEnergy == null)
            exertionEnergy = GetComponent<PlayerExertionEnergyState>();

        if (attributes == null)
        {
            attributes =
                GetComponent<PlayerAttributeState>() ??
                GetComponentInParent<PlayerAttributeState>() ??
                GetComponentInChildren<PlayerAttributeState>(true);
        }

        _climbCastHits = new RaycastHit2D[Mathf.Max(1, climbCastMaxHits)];
        _playerColliders = GetComponentsInChildren<Collider2D>(true);
        _hatchLedgeOverlapBuffer = new Collider2D[24];

        CacheHatchLedgeLayer();
    }

    private void CacheHatchLedgeLayer()
    {
        _hatchLedgeLayer = LayerMask.NameToLayer(hatchLedgeLayerName);
        _hatchLedgeBit = _hatchLedgeLayer >= 0 ? 1 << _hatchLedgeLayer : 0;

        if (ignoreHatchLedgeLayerWhileClimbing && _hatchLedgeLayer < 0)
        {
            Debug.LogError(
                $"[PlayerLadderClimber:{name}] Hatch ledge layer '{hatchLedgeLayerName}' does not exist. " +
                "Create it in Project Settings > Tags and Layers, then put HatchLedge objects on that layer.",
                this);
        }
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

        RequestLadderExertion(vertical, horizontal);

        float verticalAuthority = GetVerticalClimbAuthority(vertical);
        float horizontalAuthority = GetHorizontalClimbAuthority(horizontal);

        // Per-ladder authored speed remains the baseline. Player attributes apply
        // a global multiplier on top, so buffs/debuffs can affect climbing without
        // erasing ladder-specific tuning.
        float climbSpeedMultiplier =
            GetLadderClimbSpeedMultiplier();

        Transform ladderFrame = _activeLadder.transform;
        Vector3 proposedLocalPos = _ladderLocalClimbPosition;

        float centerLocalX = GetClimbCenterLocalX(ladderFrame);

        proposedLocalPos.y +=
            vertical *
            _activeLadder.ClimbSpeed *
            climbSpeedMultiplier *
            verticalAuthority *
            Time.fixedDeltaTime;

        ApplyHorizontalLadderMovement(
            ref proposedLocalPos,
            centerLocalX,
            horizontal,
            horizontalAuthority,
            climbSpeedMultiplier);

        float localXDistanceFromCenter = Mathf.Abs(proposedLocalPos.x - centerLocalX);
        if (allowHorizontalMovementWhileClimbing && localXDistanceFromCenter > maxAttachedLocalXDistance)
        {
            DetachSideways(horizontal, ladderFrame);
            return;
        }

        Vector2 currentWorld = _rb.position;
        Vector2 proposedWorld = ladderFrame.TransformPoint(proposedLocalPos);
        Vector2 delta = proposedWorld - currentWorld;

        if (blockClimbIntoSolid && delta.sqrMagnitude > 0.000001f && WouldClimbHitSolid(delta))
        {
            if (debugClimbBlock)
            {
                Debug.Log(
                    $"[PlayerLadderClimber:{name}] Blocked ladder movement into solid. " +
                    $"current={currentWorld}, proposed={proposedWorld}, delta={delta}",
                    this);
            }

            proposedLocalPos = _ladderLocalClimbPosition;
            proposedWorld = ladderFrame.TransformPoint(proposedLocalPos);
        }

        _ladderLocalClimbPosition = proposedLocalPos;

        _rb.MovePosition(proposedWorld);

        if (alignRotationToLadderWhileClimbing)
            AlignRotationToLadder(ladderFrame);

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

    public bool TryBeginClimb(LadderZone ladder)
    {
        if (!CanBeginClimb(ladder))
            return false;

        _activeLadder = ladder;
        BeginIgnoringHatchLedgeLayer();
        BeginIgnoringExternalBoatCollisionsIfNeeded();

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
        _ladderLocalClimbPosition = ladderFrame.InverseTransformPoint(_rb.position);

        float centerLocalX = GetClimbCenterLocalX(ladderFrame);

        if (snapToCenterOnBeginClimb)
        {
            _ladderLocalClimbPosition.x = centerLocalX;
        }
        else
        {
            float maxInitialOffset = maxAttachedLocalXDistance * initialAttachClampFraction;
            float offset = _ladderLocalClimbPosition.x - centerLocalX;
            offset = Mathf.Clamp(offset, -maxInitialOffset, maxInitialOffset);
            _ladderLocalClimbPosition.x = centerLocalX + offset;
        }

        Vector2 startWorld = ladderFrame.TransformPoint(_ladderLocalClimbPosition);
        _rb.position = startWorld;

        Log($"Begin climb on ladder={ladder.name}, localPos={_ladderLocalClimbPosition}");

        return true;
    }

    public void EndClimb(bool keepVelocity = false)
    {
        if (_activeLadder == null)
            return;

        Log($"End climb on ladder={_activeLadder.name}, keepVelocity={keepVelocity}");

        _activeLadder = null;
        ScheduleRestoreExternalBoatCollisions();

        if (disableGravityWhileClimbing)
            _rb.gravityScale = _originalGravityScale;

        if (!keepVelocity)
            _rb.linearVelocity = Vector2.zero;

        ScheduleRestoreHatchLedgeLayer();
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

    private void ApplyHorizontalLadderMovement(
    ref Vector3 localPos,
    float centerLocalX,
    float horizontal,
    float horizontalAuthority,
    float climbSpeedMultiplier)
    {
        if (!allowHorizontalMovementWhileClimbing)
        {
            localPos.x = Mathf.Lerp(
                localPos.x,
                centerLocalX,
                1f - Mathf.Exp(-_activeLadder.SnapToCenterSpeed * Time.fixedDeltaTime));

            return;
        }

        if (Mathf.Abs(horizontal) > 0.01f)
        {
            localPos.x +=
                horizontal *
                horizontalClimbSpeed *
                climbSpeedMultiplier *
                horizontalAuthority *
                Time.fixedDeltaTime;
            return;
        }

        if (recenterWhenNoHorizontalInput)
        {
            localPos.x = Mathf.Lerp(
                localPos.x,
                centerLocalX,
                1f - Mathf.Exp(-idleRecenterSpeed * Time.fixedDeltaTime));
        }
    }

    private void DetachSideways(float horizontal, Transform ladderFrame)
    {
        float dir = Mathf.Abs(horizontal) > 0.01f
            ? Mathf.Sign(horizontal)
            : Mathf.Sign(_ladderLocalClimbPosition.x - GetClimbCenterLocalX(ladderFrame));

        if (Mathf.Approximately(dir, 0f))
            dir = transform.localScale.x >= 0f ? 1f : -1f;

        Vector2 sideDir = ladderFrame.right * dir;

        EndClimb(keepVelocity: false);

        _rb.linearVelocity = sideDir * sideDetachVelocity;

        Log($"Detached sideways. dir={dir}, velocity={_rb.linearVelocity}");
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

        if (!CanAccessLadderByBoatContext(_activeLadder))
            return false;

        Vector2 closestPoint = _activeLadder.GetClosestInteractionPoint(transform.position);
        float distanceToLadder = Vector2.Distance(transform.position, closestPoint);

        return distanceToLadder <= detachDistance;
    }

    private bool CanAccessLadderByBoatContext(LadderZone ladder)
    {
        if (ladder == null)
            return false;

        PlayerBoardingState boardingState = GetComponent<PlayerBoardingState>();
        bool isBoarded = boardingState != null && boardingState.IsBoarded;

        Boat ladderBoat = ResolveBoatForLadder(ladder);

        // World/dock ladders do not belong to the boat the player is currently inside.
        // While boarded, do not let proximity through the hull make them interactable.
        if (ladderBoat == null)
            return !isBoarded;

        LadderZone.BoatAccessMode resolvedMode =
            ResolveLadderBoatAccessMode(ladder, ladderBoat);

        if (resolvedMode == LadderZone.BoatAccessMode.Any)
            return !isBoarded ||
                   boardingState.CurrentBoatRoot == ladderBoat.transform;

        if (resolvedMode == LadderZone.BoatAccessMode.InteriorOnly)
        {
            return isBoarded &&
                   boardingState.CurrentBoatRoot == ladderBoat.transform;
        }

        // Exterior ladders are service/access geometry used while physically outside.
        // This deliberately prevents a boarded player from grabbing an exterior ladder
        // through a wall just because its trigger happens to be close.
        if (resolvedMode == LadderZone.BoatAccessMode.ExteriorOnly)
            return !isBoarded;

        return false;
    }

    private static LadderZone.BoatAccessMode ResolveLadderBoatAccessMode(
        LadderZone ladder,
        Boat ladderBoat)
    {
        if (ladder == null)
            return LadderZone.BoatAccessMode.ExteriorOnly;

        if (ladder.AccessMode != LadderZone.BoatAccessMode.Auto)
            return ladder.AccessMode;

        if (ladderBoat == null)
            return LadderZone.BoatAccessMode.ExteriorOnly;

        BoatBoardedVolume boardedVolume =
            ladderBoat.GetComponentInChildren<BoatBoardedVolume>(true);

        if (boardedVolume == null)
        {
            // Preserve the old safe assumption when a boat has no authored volume.
            return LadderZone.BoatAccessMode.InteriorOnly;
        }

        bool inside =
            boardedVolume.ContainsWorldPoint(
                ladder.ClimbCenter.position);

        return inside
            ? LadderZone.BoatAccessMode.InteriorOnly
            : LadderZone.BoatAccessMode.ExteriorOnly;
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

            if (motor.IsGrounded &&
                (atTop || atBottom) &&
                Mathf.Abs(vertical) <= 0.01f &&
                Mathf.Abs(horizontal) > 0.01f)
            {
                EndClimb(keepVelocity: false);
            }
        }
    }

    private bool WouldClimbHitSolid(Vector2 delta)
    {
        if (delta.sqrMagnitude <= 0.000001f)
            return false;

        if (_climbCastHits == null || _climbCastHits.Length != Mathf.Max(1, climbCastMaxHits))
            _climbCastHits = new RaycastHit2D[Mathf.Max(1, climbCastMaxHits)];

        LayerMask mask = climbBlockMask.value != 0
            ? climbBlockMask
            : motor != null ? motor.groundMask : ~0;

        // The climb safety cast must respect the player's actual Rigidbody2D
        // collision policy. For example, while unboarded PlayerBoardingState
        // excludes Hull/HatchLedge, so those layers must not independently
        // block ladder-local movement just because climbBlockMask contains them.
        mask.value &= ~_rb.excludeLayers.value;

        if (ignoreHatchLedgeLayerWhileClimbing && _hatchLedgeBit != 0)
            mask &= ~_hatchLedgeBit;

        if (mask.value == 0)
            return false;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = mask,
            useTriggers = false
        };

        Vector2 direction = delta.normalized;
        float distance = delta.magnitude + climbBlockSkin;

        int hitCount = _rb.Cast(direction, filter, _climbCastHits, distance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _climbCastHits[i];

            if (hit.collider == null)
                continue;

            if (hit.collider.isTrigger)
                continue;

            if (hit.collider.attachedRigidbody == _rb)
                continue;

            HatchLedge hatchLedge = hit.collider.GetComponent<HatchLedge>();
            if (hatchLedge == null)
                hatchLedge = hit.collider.GetComponentInParent<HatchLedge>();

            if (hatchLedge != null)
                continue;

            LadderZone ladder = hit.collider.GetComponent<LadderZone>();
            if (ladder == null)
                ladder = hit.collider.GetComponentInParent<LadderZone>();

            if (ladder != null && ReferenceEquals(ladder, _activeLadder))
                continue;

            if (ShouldIgnoreBoatColliderForExternalClimb(hit.collider))
                continue;

            if (debugClimbBlock)
            {
                Debug.Log(
                    $"[PlayerLadderClimber:{name}] Climb cast hit blocker '{hit.collider.name}' " +
                    $"layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} distance={hit.distance:F3}",
                    hit.collider);
            }

            return true;
        }

        return false;
    }

    private void BeginIgnoringExternalBoatCollisionsIfNeeded()
    {
        if (!ignoreBoatCollisionsWhileExternallyClimbing ||
            _activeLadder == null)
        {
            return;
        }

        Boat ladderBoat =
            ResolveBoatForLadder(_activeLadder);

        if (ladderBoat == null)
            return;

        PlayerBoardingState boardingState =
            GetComponent<PlayerBoardingState>();

        if (boardingState != null && boardingState.IsBoarded)
            return;

        LadderZone.BoatAccessMode resolvedMode =
            ResolveLadderBoatAccessMode(
                _activeLadder,
                ladderBoat);

        if (resolvedMode != LadderZone.BoatAccessMode.ExteriorOnly)
            return;

        if (_restoreExternalBoatCollisionsRoutine != null)
        {
            StopCoroutine(_restoreExternalBoatCollisionsRoutine);
            _restoreExternalBoatCollisionsRoutine = null;
        }

        RestoreExternalBoatCollisionsImmediately();

        _externalClimbBoat = ladderBoat;
        _externalClimbBoatBody = ResolveBoatRigidbody(ladderBoat);

        if (_playerColliders == null || _playerColliders.Length == 0)
            _playerColliders = GetComponentsInChildren<Collider2D>(true);

        List<Collider2D> boatColliders = CollectExternalClimbBoatColliders(
            ladderBoat,
            _externalClimbBoatBody);

        for (int p = 0; p < _playerColliders.Length; p++)
        {
            Collider2D playerCollider = _playerColliders[p];
            if (playerCollider == null ||
                !playerCollider.enabled ||
                playerCollider.isTrigger)
            {
                continue;
            }

            for (int b = 0; b < boatColliders.Count; b++)
            {
                Collider2D boatCollider = boatColliders[b];
                if (boatCollider == null ||
                    !boatCollider.enabled ||
                    boatCollider.isTrigger)
                {
                    continue;
                }

                if (boatCollider.attachedRigidbody == _rb)
                    continue;

                if (Physics2D.GetIgnoreCollision(playerCollider, boatCollider))
                    continue;

                Physics2D.IgnoreCollision(
                    playerCollider,
                    boatCollider,
                    true);

                _ignoredExternalBoatCollisions.Add(
                    new IgnoredBoatCollisionPair
                    {
                        PlayerCollider = playerCollider,
                        BoatCollider = boatCollider
                    });
            }
        }

        Log(
            $"Ignoring {_ignoredExternalBoatCollisions.Count} player/boat collider pairs " +
            $"while externally climbing boat '{ladderBoat.name}'. " +
            $"boatBody='{(_externalClimbBoatBody != null ? _externalClimbBoatBody.name : "NULL")}', " +
            $"candidateBoatColliders={boatColliders.Count}.");

        if (_ignoredExternalBoatCollisions.Count == 0)
        {
            Debug.LogWarning(
                $"[PlayerLadderClimber:{name}] Exterior ladder climb resolved boat '{ladderBoat.name}', " +
                "but zero player/boat collider pairs were ignored. If climbing is still blocked, " +
                "enable Debug Climb Block and inspect the reported blocker.",
                this);
        }

        Physics2D.SyncTransforms();
    }

    private bool ShouldIgnoreBoatColliderForExternalClimb(Collider2D collider)
    {
        if (collider == null ||
            _externalClimbBoat == null ||
            _activeLadder == null)
        {
            return false;
        }

        // Primary authority: all normal boat structure should be attached to the
        // boat's single shared Rigidbody2D, regardless of child hierarchy.
        if (_externalClimbBoatBody != null &&
            collider.attachedRigidbody == _externalClimbBoatBody)
        {
            return true;
        }

        // Fallbacks protect authored/generated colliders that are under the boat
        // hierarchy but do not currently resolve an attached Rigidbody2D.
        if (collider.transform == _externalClimbBoat.transform ||
            collider.transform.IsChildOf(_externalClimbBoat.transform))
        {
            return true;
        }

        Boat hitBoat = collider.GetComponentInParent<Boat>();
        return hitBoat == _externalClimbBoat;
    }

    private static Boat ResolveBoatForLadder(LadderZone ladder)
    {
        if (ladder == null)
            return null;

        Boat boat = ladder.GetComponentInParent<Boat>();
        if (boat != null)
            return boat;

        // Fallback through the ladder collider's attached body. This catches
        // generated/atypical hierarchy layouts while still requiring a real Boat.
        Collider2D[] ladderColliders =
            ladder.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < ladderColliders.Length; i++)
        {
            Collider2D ladderCollider = ladderColliders[i];
            if (ladderCollider == null || ladderCollider.attachedRigidbody == null)
                continue;

            Rigidbody2D attachedBody = ladderCollider.attachedRigidbody;

            boat =
                attachedBody.GetComponent<Boat>() ??
                attachedBody.GetComponentInParent<Boat>();

            if (boat != null)
                return boat;
        }

        return null;
    }

    private static Rigidbody2D ResolveBoatRigidbody(Boat boat)
    {
        if (boat == null)
            return null;

        Rigidbody2D body = boat.GetComponent<Rigidbody2D>();
        if (body != null)
            return body;

        return boat.GetComponentInParent<Rigidbody2D>();
    }

    private static List<Collider2D> CollectExternalClimbBoatColliders(
        Boat boat,
        Rigidbody2D boatBody)
    {
        List<Collider2D> result = new();

        if (boatBody != null)
        {
            // This is the authoritative set for the one-Rigidbody boat architecture.
            boatBody.GetAttachedColliders(result);
        }

        if (boat != null)
        {
            Collider2D[] hierarchyColliders =
                boat.GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < hierarchyColliders.Length; i++)
            {
                Collider2D collider = hierarchyColliders[i];
                if (collider == null || result.Contains(collider))
                    continue;

                result.Add(collider);
            }
        }

        return result;
    }

    private void ScheduleRestoreExternalBoatCollisions()
    {
        if (_ignoredExternalBoatCollisions.Count == 0)
        {
            _externalClimbBoat = null;
            _externalClimbBoatBody = null;
            return;
        }

        if (_restoreExternalBoatCollisionsRoutine != null)
            StopCoroutine(_restoreExternalBoatCollisionsRoutine);

        _restoreExternalBoatCollisionsRoutine =
            StartCoroutine(RestoreExternalBoatCollisionsWhenSafe());
    }

    private IEnumerator RestoreExternalBoatCollisionsWhenSafe()
    {
        // Give an authored ladder exit point one physics step to move the player clear.
        yield return new WaitForFixedUpdate();

        while (AnyIgnoredBoatPairStillOverlapping())
            yield return new WaitForFixedUpdate();

        RestoreExternalBoatCollisionsImmediately();
        _restoreExternalBoatCollisionsRoutine = null;
    }

    private bool AnyIgnoredBoatPairStillOverlapping()
    {
        for (int i = 0; i < _ignoredExternalBoatCollisions.Count; i++)
        {
            IgnoredBoatCollisionPair pair =
                _ignoredExternalBoatCollisions[i];

            if (pair.PlayerCollider == null ||
                pair.BoatCollider == null ||
                !pair.PlayerCollider.enabled ||
                !pair.BoatCollider.enabled)
            {
                continue;
            }

            ColliderDistance2D distance =
                Physics2D.Distance(
                    pair.PlayerCollider,
                    pair.BoatCollider);

            if (distance.isOverlapped)
                return true;
        }

        return false;
    }

    private void RestoreExternalBoatCollisionsImmediately()
    {
        for (int i = 0; i < _ignoredExternalBoatCollisions.Count; i++)
        {
            IgnoredBoatCollisionPair pair =
                _ignoredExternalBoatCollisions[i];

            if (pair.PlayerCollider == null ||
                pair.BoatCollider == null)
            {
                continue;
            }

            Physics2D.IgnoreCollision(
                pair.PlayerCollider,
                pair.BoatCollider,
                false);
        }

        _ignoredExternalBoatCollisions.Clear();
        _externalClimbBoat = null;
        _externalClimbBoatBody = null;
    }

    private void AlignRotationToLadder(Transform ladderFrame)
    {
        float targetRot = ladderFrame.eulerAngles.z;

        float snappedRot = Mathf.LerpAngle(
            _rb.rotation,
            targetRot,
            1f - Mathf.Exp(-rotationSnapSpeed * Time.fixedDeltaTime));

        _rb.MoveRotation(snappedRot);
        _rb.angularVelocity = 0f;
    }

    private float GetClimbCenterLocalX(Transform ladderFrame)
    {
        if (_activeLadder == null || ladderFrame == null)
            return 0f;

        return ladderFrame.InverseTransformPoint(_activeLadder.ClimbCenter.position).x;
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

    private float GetLadderClimbSpeedMultiplier()
    {
        float multiplier =
            attributes != null
                ? attributes.GetFloat(
                    PlayerAttributeId.LadderClimbSpeedMultiplier,
                    1f)
                : 1f;

        return Mathf.Max(
            0f,
            multiplier);
    }

    private float GetVerticalIntent(CharacterIntent intent)
    {
        float up = intent.ClimbUpHeld ? 1f : 0f;
        float down = intent.ClimbDownHeld ? 1f : 0f;
        return up - down;
    }

    private void RequestLadderExertion(float vertical, float horizontal)
    {
        if (!useLadderExertion)
            return;

        if (exertionEnergy == null)
            return;

        float ceiling = idleOnLadderExertionCeiling;

        if (vertical > 0.01f)
        {
            ceiling = Mathf.Max(ceiling, climbUpExertionCeiling);
        }
        else if (vertical < -0.01f)
        {
            ceiling = Mathf.Max(ceiling, climbDownExertionCeiling);
        }

        if (Mathf.Abs(horizontal) > 0.01f)
            ceiling = Mathf.Max(ceiling, horizontalClimbExertionCeiling);

        exertionEnergy.AddExternalExertionDemand(ceiling, ladderExertionApproachRate);
    }

    private float GetVerticalClimbAuthority(float vertical)
    {
        if (exertionEnergy == null)
            return 1f;

        if (vertical > 0.01f && scaleUpwardClimbByEnergy)
            return GetEnergyAuthority();

        if (vertical < -0.01f && scaleDownwardClimbByEnergy)
            return GetEnergyAuthority();

        return 1f;
    }

    private float GetHorizontalClimbAuthority(float horizontal)
    {
        if (exertionEnergy == null)
            return 1f;

        if (Mathf.Abs(horizontal) > 0.01f && scaleHorizontalClimbByEnergy)
            return GetEnergyAuthority();

        return 1f;
    }

    private float GetEnergyAuthority()
    {
        if (exertionEnergy == null)
            return 1f;

        return Mathf.Clamp01(Mathf.Max(
            minClimbAuthorityWhenExhausted,
            exertionEnergy.MoveAuthority));
    }

    private void BeginIgnoringHatchLedgeLayer()
    {
        if (!ignoreHatchLedgeLayerWhileClimbing)
            return;

        if (_hatchLedgeBit == 0)
            return;

        if (_restoreHatchLedgeLayerRoutine != null)
        {
            StopCoroutine(_restoreHatchLedgeLayerRoutine);
            _restoreHatchLedgeLayerRoutine = null;
        }

        if (!_hasExcludeLayersBeforeClimb)
        {
            _excludeLayersBeforeClimb = _rb.excludeLayers;
            _hasExcludeLayersBeforeClimb = true;
        }

        _rb.excludeLayers |= _hatchLedgeBit;

        Log($"BeginIgnoringHatchLedgeLayer | layer='{hatchLedgeLayerName}' excludeLayers={_rb.excludeLayers}");
    }

    private void ScheduleRestoreHatchLedgeLayer()
    {
        if (!ignoreHatchLedgeLayerWhileClimbing)
            return;

        if (_restoreHatchLedgeLayerRoutine != null)
            StopCoroutine(_restoreHatchLedgeLayerRoutine);

        _restoreHatchLedgeLayerRoutine = StartCoroutine(RestoreHatchLedgeLayerWhenSafe());
    }

    private IEnumerator RestoreHatchLedgeLayerWhenSafe()
    {
        float minDelay = Mathf.Max(0f, hatchLedgeMinimumIgnoreAfterDetachSeconds);
        if (minDelay > 0f)
            yield return new WaitForSeconds(minDelay);

        float deadline = Time.time + Mathf.Max(0.25f, maxHatchLedgeRestoreWaitSeconds);

        while (IsPenetratingHatchLedge())
        {
            if (Time.time >= deadline)
            {
                Debug.LogWarning(
                    $"[PlayerLadderClimber:{name}] Still penetrating HatchLedge after {maxHatchLedgeRestoreWaitSeconds:F2}s. " +
                    "Keeping HatchLedge ignored to avoid boat launch. This likely means the ladder exit point needs adjustment.",
                    this);

                _restoreHatchLedgeLayerRoutine = null;
                yield break;
            }

            yield return null;
        }

        RestoreHatchLedgeLayer();
        _restoreHatchLedgeLayerRoutine = null;
    }

    private bool IsPenetratingHatchLedge()
    {
        if (_hatchLedgeBit == 0)
            return false;

        if (_playerColliders == null || _playerColliders.Length == 0)
            _playerColliders = GetComponentsInChildren<Collider2D>(true);

        if (_hatchLedgeOverlapBuffer == null || _hatchLedgeOverlapBuffer.Length == 0)
            _hatchLedgeOverlapBuffer = new Collider2D[24];

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _hatchLedgeBit,
            useTriggers = false
        };

        for (int i = 0; i < _playerColliders.Length; i++)
        {
            Collider2D playerCol = _playerColliders[i];
            if (playerCol == null || !playerCol.enabled || playerCol.isTrigger)
                continue;

            Bounds b = playerCol.bounds;
            Vector2 center = b.center;
            Vector2 size = new Vector2(
                b.size.x + hatchLedgeOverlapCheckPadding * 2f,
                b.size.y + hatchLedgeOverlapCheckPadding * 2f);

            int hitCount = Physics2D.OverlapBox(
                center,
                size,
                0f,
                filter,
                _hatchLedgeOverlapBuffer);

            for (int h = 0; h < hitCount; h++)
            {
                Collider2D hit = _hatchLedgeOverlapBuffer[h];
                if (hit == null || !hit.enabled || hit.isTrigger)
                    continue;

                HatchLedge ledge = hit.GetComponent<HatchLedge>();
                if (ledge == null)
                    ledge = hit.GetComponentInParent<HatchLedge>();

                if (ledge == null)
                    continue;

                ColliderDistance2D distance = Physics2D.Distance(playerCol, hit);

                if (distance.isOverlapped && distance.distance < -hatchLedgePenetrationTolerance)
                {
                    Log(
                        $"Still penetrating HatchLedge '{hit.name}' distance={distance.distance:F4}. " +
                        "Keeping HatchLedge ignored.");

                    return true;
                }
            }
        }

        return false;
    }

    private void RestoreHatchLedgeLayer()
    {
        if (!_hasExcludeLayersBeforeClimb)
            return;

        _rb.excludeLayers = _excludeLayersBeforeClimb;
        _hasExcludeLayersBeforeClimb = false;

        Log($"RestoreHatchLedgeLayer | excludeLayers={_rb.excludeLayers}");
    }

    private void OnDisable()
    {
        if (_restoreExternalBoatCollisionsRoutine != null)
        {
            StopCoroutine(_restoreExternalBoatCollisionsRoutine);
            _restoreExternalBoatCollisionsRoutine = null;
        }

        RestoreExternalBoatCollisionsImmediately();

        if (_restoreHatchLedgeLayerRoutine != null)
        {
            StopCoroutine(_restoreHatchLedgeLayerRoutine);
            _restoreHatchLedgeLayerRoutine = null;
        }

        RestoreHatchLedgeLayer();

        if (disableGravityWhileClimbing && _rb != null && IsClimbing)
            _rb.gravityScale = _originalGravityScale;

        _activeLadder = null;
    }

    private void Log(string message)
    {
        if (!debugLogs)
            return;

        Debug.Log($"[PlayerLadderClimber:{name}] {message}", this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(hatchLedgeLayerName))
            hatchLedgeLayerName = "HatchLedge";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, ladderSearchRadius);

        if (_activeLadder != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _activeLadder.GetClosestInteractionPoint(transform.position));

            Transform ladderFrame = _activeLadder.transform;
            float centerLocalX = GetClimbCenterLocalX(ladderFrame);

            Vector3 left = ladderFrame.TransformPoint(new Vector3(
                centerLocalX - maxAttachedLocalXDistance,
                _ladderLocalClimbPosition.y,
                _ladderLocalClimbPosition.z));

            Vector3 right = ladderFrame.TransformPoint(new Vector3(
                centerLocalX + maxAttachedLocalXDistance,
                _ladderLocalClimbPosition.y,
                _ladderLocalClimbPosition.z));

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(left, right);
        }
    }
#endif
}