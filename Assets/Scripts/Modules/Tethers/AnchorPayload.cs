using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnchorPayload : TetherPayload,
    ITetherBottomContactProvider,
    ITetherRetrievalAwarePayload,
    ITetherHoldingStateProvider
{
    [Header("Anchor Holding")]
    [Tooltip("Flat-ground holding capacity as a multiple of this anchor's own weight.")]
    [SerializeField, Min(0f)] private float holdingWeightMultiplier = 8f;

    [SerializeField] private LayerMask validSeafloorLayers = ~0;

    [Tooltip(
        "Minimum upward component of a contact normal required for anchoring. " +
        "Keep above 0 so sheer vertical cliffs cannot anchor.")]
    [SerializeField, Range(0.01f, 1f)] private float minimumGroundNormalY = 0.25f;

    [Header("Slope Holding")]
    [SerializeField, Min(1f)] private float uphillHoldingMultiplier = 1.75f;
    [SerializeField, Range(0.01f, 1f)] private float downhillHoldingMultiplier = 0.5f;

    [Header("Re-Set After Dragging")]
    [Tooltip("Delay after an overload break before the anchor may set again.")]
    [SerializeField, Min(0f)] private float reacquireDelaySeconds = 0.2f;

    [Tooltip("Maximum seabed-tangent speed allowed when re-setting after an overload break.")]
    [SerializeField, Min(0f)] private float staticHoldReacquireSpeed = 0.03f;

    [Header("Retrieval Release")]
    [Tooltip(
        "When Raise releases a set anchor, move it this far away from the seabed " +
        "along the latched ground normal before retrieval continues.")]
    [SerializeField, Min(0f)] private float retrievalUnseatDistanceMeters = 0.08f;

    [Header("Dragging Resistance")]
    [Tooltip("Speeds below this are treated as stationary by fallback drag resistance.")]
    [SerializeField, Min(0f)] private float minimumSlidingSpeed = 0.001f;

    [Header("Anchor Balance")]
    [Tooltip("Optional child transform defining Rigidbody2D center of mass.")]
    [SerializeField] private Transform centerOfMassPoint;

    [Header("Runtime Debug")]
    [SerializeField] private Vector2 currentGroundNormal = Vector2.up;
    [SerializeField] private Vector2 currentGroundTangent = Vector2.right;
    [SerializeField] private float currentSlopeDegrees;
    [SerializeField] private float currentTangentialSpeed;
    [SerializeField] private float currentAnchorMass;
    [SerializeField] private float currentFlatHoldingForceNewtons;
    [SerializeField] private float currentSlopeHoldingMultiplier = 1f;
    [SerializeField] private float currentEffectiveHoldingForceNewtons;
    [SerializeField, Range(0f, 1f)] private float currentTangentialTetherFactor;
    [SerializeField] private float currentStaticJointBreakForceNewtons;
    [SerializeField] private float currentDragResistanceForceNewtons;
    [SerializeField] private bool isDragging;
    [SerializeField] private float lastBreakHoldingCapacityNewtons;

    private readonly Dictionary<Collider2D, Vector2> _validGroundContacts =
        new Dictionary<Collider2D, Vector2>();

    private HingeJoint2D _staticHoldJoint;
    private DistanceJoint2D _tetherDistanceJoint;

    private float _lastStaticHoldBreakTime = float.NegativeInfinity;
    private bool _hasBrokenFromOverload;
    private bool _retrievalReleaseRequested;

    private Vector2 _latchedGroundNormal = Vector2.up;
    private Vector2 _latchedGroundTangent = Vector2.right;

    public float HoldingWeightMultiplier => Mathf.Max(0f, holdingWeightMultiplier);
    public float HoldingForceNewtons => CalculateFlatHoldingForceNewtons(Rigidbody);

    public bool IsOnBottom =>
        !_retrievalReleaseRequested &&
        (HasActiveHold || TryGetGroundFrame(out _, out _));

    public bool HasActiveHold =>
        _staticHoldJoint != null &&
        _staticHoldJoint.enabled;

    public bool IsReleasedForRetrieval => _retrievalReleaseRequested;

    protected override void Awake()
    {
        base.Awake();
        ApplyCenterOfMass();
    }

    private void OnDisable()
    {
        ReleaseStaticHold();
    }

    protected override void OnDestroy()
    {
        ReleaseStaticHold();
        base.OnDestroy();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        holdingWeightMultiplier = Mathf.Max(0f, holdingWeightMultiplier);
        minimumGroundNormalY = Mathf.Clamp(minimumGroundNormalY, 0.01f, 1f);
        uphillHoldingMultiplier = Mathf.Max(1f, uphillHoldingMultiplier);
        downhillHoldingMultiplier = Mathf.Clamp(downhillHoldingMultiplier, 0.01f, 1f);
        reacquireDelaySeconds = Mathf.Max(0f, reacquireDelaySeconds);
        staticHoldReacquireSpeed = Mathf.Max(0f, staticHoldReacquireSpeed);
        retrievalUnseatDistanceMeters = Mathf.Max(0f, retrievalUnseatDistanceMeters);
        minimumSlidingSpeed = Mathf.Max(0f, minimumSlidingSpeed);

        ApplyCenterOfMass();
    }
#endif

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (!HasGameplayAuthority)
        {
            ReleaseStaticHold();
            ClearRuntimeDebug();
            return;
        }

        Rigidbody2D rb = Rigidbody;

        if (rb == null ||
            !rb.simulated ||
            rb.bodyType != RigidbodyType2D.Dynamic)
        {
            ReleaseStaticHold();
            ClearRuntimeDebug();
            return;
        }

        currentAnchorMass = Mathf.Max(0f, rb.mass);
        currentFlatHoldingForceNewtons = CalculateFlatHoldingForceNewtons(rb);

        if (HasActiveHold)
        {
            UpdateActiveHold(rb);
            return;
        }

        currentStaticJointBreakForceNewtons = 0f;

        if (_retrievalReleaseRequested)
        {
            UpdateRetrievalDebug(rb);
            return;
        }

        if (!TryGetGroundFrame(out Vector2 groundNormal, out Vector2 groundTangent))
        {
            ClearRuntimeDebug();
            currentAnchorMass = Mathf.Max(0f, rb.mass);
            currentFlatHoldingForceNewtons = CalculateFlatHoldingForceNewtons(rb);
            return;
        }

        currentGroundNormal = groundNormal;
        currentGroundTangent = groundTangent;
        currentSlopeDegrees = Vector2.Angle(groundNormal, Vector2.up);

        float tangentialVelocity = Vector2.Dot(rb.linearVelocity, groundTangent);
        currentTangentialSpeed = tangentialVelocity;

        float loadDirection =
            ResolveAttemptedLoadDirection(rb, groundTangent, tangentialVelocity);

        currentSlopeHoldingMultiplier =
            CalculateSlopeHoldingMultiplier(groundTangent, loadDirection);

        currentEffectiveHoldingForceNewtons =
            currentFlatHoldingForceNewtons * currentSlopeHoldingMultiplier;

        currentTangentialTetherFactor =
            CalculateTangentialTetherFactor(groundTangent);

        bool delayExpired =
            Time.time >= _lastStaticHoldBreakTime + reacquireDelaySeconds;

        bool maySet =
            currentEffectiveHoldingForceNewtons > 0.0001f &&
            (!_hasBrokenFromOverload ||
             (delayExpired &&
              Mathf.Abs(tangentialVelocity) <= staticHoldReacquireSpeed));

        if (maySet)
        {
            EstablishStaticHold(rb, groundNormal, groundTangent);

            if (HasActiveHold)
            {
                _hasBrokenFromOverload = false;
                currentDragResistanceForceNewtons = 0f;
                isDragging = false;
                return;
            }
        }

        ApplyDraggingResistance(rb, groundTangent, tangentialVelocity);
    }

    private void UpdateActiveHold(Rigidbody2D rb)
    {
        currentGroundNormal = _latchedGroundNormal;
        currentGroundTangent = _latchedGroundTangent;
        currentSlopeDegrees = Vector2.Angle(_latchedGroundNormal, Vector2.up);
        currentTangentialSpeed = Vector2.Dot(rb.linearVelocity, _latchedGroundTangent);

        float loadDirection =
            ResolveAttemptedLoadDirection(
                rb,
                _latchedGroundTangent,
                currentTangentialSpeed);

        currentSlopeHoldingMultiplier =
            CalculateSlopeHoldingMultiplier(_latchedGroundTangent, loadDirection);

        currentEffectiveHoldingForceNewtons =
            currentFlatHoldingForceNewtons * currentSlopeHoldingMultiplier;

        if (currentEffectiveHoldingForceNewtons <= 0.0001f)
        {
            MarkOverloadBreak(0f);
            ReleaseStaticHold();
            return;
        }

        currentTangentialTetherFactor =
            CalculateTangentialTetherFactor(_latchedGroundTangent);

        currentStaticJointBreakForceNewtons =
            CalculateJointBreakForce(
                currentEffectiveHoldingForceNewtons,
                currentTangentialTetherFactor);

        _staticHoldJoint.breakForce = currentStaticJointBreakForceNewtons;
        _staticHoldJoint.breakTorque = Mathf.Infinity;

        currentDragResistanceForceNewtons = 0f;
        isDragging = false;
    }

    private void UpdateRetrievalDebug(Rigidbody2D rb)
    {
        currentDragResistanceForceNewtons = 0f;
        currentTangentialTetherFactor = 0f;
        isDragging = false;

        if (!TryGetGroundFrame(out Vector2 normal, out Vector2 tangent))
        {
            currentGroundNormal = Vector2.up;
            currentGroundTangent = Vector2.right;
            currentSlopeDegrees = 0f;
            currentTangentialSpeed = 0f;
            currentSlopeHoldingMultiplier = 1f;
            currentEffectiveHoldingForceNewtons = 0f;
            return;
        }

        currentGroundNormal = normal;
        currentGroundTangent = tangent;
        currentSlopeDegrees = Vector2.Angle(normal, Vector2.up);
        currentTangentialSpeed = Vector2.Dot(rb.linearVelocity, tangent);
        currentSlopeHoldingMultiplier = 1f;
        currentEffectiveHoldingForceNewtons = 0f;
    }

    private void EstablishStaticHold(
        Rigidbody2D rb,
        Vector2 groundNormal,
        Vector2 groundTangent)
    {
        if (rb == null ||
            HasActiveHold ||
            _retrievalReleaseRequested ||
            currentEffectiveHoldingForceNewtons <= 0.0001f)
        {
            return;
        }

        HingeJoint2D joint = _staticHoldJoint;

        if (joint == null)
            joint = GetComponent<HingeJoint2D>();

        if (joint == null)
            joint = gameObject.AddComponent<HingeJoint2D>();

        // Reuse one disabled joint rather than repeatedly creating/destroying it.
        joint.enabled = false;
        joint.connectedBody = null;
        joint.anchor = rb.centerOfMass;
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedAnchor = rb.worldCenterOfMass;
        joint.useConnectedAnchor = true;
        joint.enableCollision = false;
        joint.useMotor = false;
        joint.useLimits = false;

        _latchedGroundNormal =
            groundNormal.sqrMagnitude > 0.0001f
                ? groundNormal.normalized
                : Vector2.up;

        _latchedGroundTangent =
            groundTangent.sqrMagnitude > 0.0001f
                ? groundTangent.normalized
                : Vector2.right;

        currentTangentialTetherFactor =
            CalculateTangentialTetherFactor(_latchedGroundTangent);

        currentStaticJointBreakForceNewtons =
            CalculateJointBreakForce(
                currentEffectiveHoldingForceNewtons,
                currentTangentialTetherFactor);

        joint.breakForce = currentStaticJointBreakForceNewtons;
        joint.breakTorque = Mathf.Infinity;

        // Disable rather than destroy on break. This avoids the Scene-view
        // MissingReferenceException Unity produced when drawing a destroyed joint.
        joint.breakAction = JointBreakAction2D.Disable;

        _staticHoldJoint = joint;
        joint.enabled = true;

        rb.linearVelocity = Vector2.zero;
        rb.WakeUp();

        isDragging = false;
    }

    private void ReleaseStaticHold()
    {
        currentStaticJointBreakForceNewtons = 0f;

        if (_staticHoldJoint != null)
            _staticHoldJoint.enabled = false;
    }

    private void OnJointBreak2D(Joint2D brokenJoint)
    {
        if (!HasGameplayAuthority)
            return;

        if (brokenJoint == null ||
            brokenJoint != _staticHoldJoint)
        {
            return;
        }

        MarkOverloadBreak(currentEffectiveHoldingForceNewtons);

        // JointBreakAction2D.Disable will also do this after the callback.
        if (_staticHoldJoint != null)
            _staticHoldJoint.enabled = false;
    }

    private void MarkOverloadBreak(float holdingCapacityNewtons)
    {
        lastBreakHoldingCapacityNewtons = Mathf.Max(0f, holdingCapacityNewtons);
        _lastStaticHoldBreakTime = Time.time;
        _hasBrokenFromOverload = true;
        currentStaticJointBreakForceNewtons = 0f;
        isDragging = true;
    }

    public void BeginTetherRetrieval()
    {
        Rigidbody2D rb = Rigidbody;
        bool wasHolding = HasActiveHold;

        Vector2 releaseNormal =
            _latchedGroundNormal.sqrMagnitude > 0.0001f
                ? _latchedGroundNormal.normalized
                : Vector2.up;

        _retrievalReleaseRequested = true;
        ReleaseStaticHold();

        if (!wasHolding ||
            rb == null ||
            !rb.simulated ||
            rb.bodyType != RigidbodyType2D.Dynamic)
        {
            return;
        }

        float unseatDistance = Mathf.Max(0f, retrievalUnseatDistanceMeters);

        if (unseatDistance > 0f)
            rb.position += releaseNormal * unseatDistance;

        rb.WakeUp();
    }

    public void EndTetherRetrieval()
    {
        _retrievalReleaseRequested = false;
    }

    private float CalculateFlatHoldingForceNewtons(Rigidbody2D rb)
    {
        if (rb == null)
            return 0f;

        return
            Mathf.Max(0f, rb.mass) *
            Mathf.Max(0f, Physics2D.gravity.magnitude) *
            Mathf.Abs(rb.gravityScale) *
            HoldingWeightMultiplier;
    }

    private float CalculateTangentialTetherFactor(Vector2 groundTangent)
    {
        if (!TryGetTetherDirectionTowardStart(out Vector2 tetherDirection))
            return 0f;

        Vector2 tangent =
            groundTangent.sqrMagnitude > 0.000001f
                ? groundTangent.normalized
                : Vector2.right;

        return Mathf.Clamp01(
            Mathf.Abs(Vector2.Dot(tetherDirection, tangent)));
    }

    private static float CalculateJointBreakForce(
        float tangentialHoldingCapacityNewtons,
        float tangentialTetherFactor)
    {
        float capacity = Mathf.Max(0f, tangentialHoldingCapacityNewtons);

        if (capacity <= 0.0001f)
            return 0f;

        // A nearly normal/vertical rope contributes essentially no seabed drag.
        if (tangentialTetherFactor <= 0.001f)
            return Mathf.Infinity;

        return capacity / tangentialTetherFactor;
    }

    private bool TryGetTetherDirectionTowardStart(out Vector2 direction)
    {
        direction = Vector2.zero;
        CacheTetherDistanceJoint();

        Rigidbody2D rb = Rigidbody;

        if (rb == null ||
            _tetherDistanceJoint == null ||
            !_tetherDistanceJoint.enabled)
        {
            return false;
        }

        Vector2 selfAnchorWorld =
            rb.transform.TransformPoint(_tetherDistanceJoint.anchor);

        Vector2 connectedAnchorWorld =
            _tetherDistanceJoint.connectedBody != null
                ? _tetherDistanceJoint.connectedBody.transform.TransformPoint(
                    _tetherDistanceJoint.connectedAnchor)
                : _tetherDistanceJoint.connectedAnchor;

        Vector2 delta = connectedAnchorWorld - selfAnchorWorld;

        if (delta.sqrMagnitude <= 0.000001f)
            return false;

        direction = delta.normalized;
        return true;
    }

    private float ResolveAttemptedLoadDirection(
        Rigidbody2D rb,
        Vector2 groundTangent,
        float tangentialVelocity)
    {
        if (Mathf.Abs(tangentialVelocity) > 0.001f)
            return Mathf.Sign(tangentialVelocity);

        if (TryGetTetherDirectionTowardStart(out Vector2 tetherDirection))
        {
            float projected = Vector2.Dot(tetherDirection, groundTangent);

            if (Mathf.Abs(projected) > 0.001f)
                return Mathf.Sign(projected);
        }

        Vector2 gravityForce =
            Physics2D.gravity * rb.gravityScale * rb.mass;

        float gravityAlongGround =
            Vector2.Dot(gravityForce, groundTangent);

        return Mathf.Abs(gravityAlongGround) > 0.001f
            ? Mathf.Sign(gravityAlongGround)
            : 0f;
    }

    private void CacheTetherDistanceJoint()
    {
        if (_tetherDistanceJoint != null &&
            _tetherDistanceJoint.enabled)
        {
            return;
        }

        _tetherDistanceJoint = null;

        DistanceJoint2D[] joints = GetComponents<DistanceJoint2D>();

        for (int i = 0; i < joints.Length; i++)
        {
            DistanceJoint2D candidate = joints[i];

            if (candidate == null || !candidate.enabled)
                continue;

            _tetherDistanceJoint = candidate;
            return;
        }
    }

    private float CalculateSlopeHoldingMultiplier(
        Vector2 groundTangent,
        float loadDirection)
    {
        if (Mathf.Abs(loadDirection) < 0.001f)
            return 1f;

        float verticalTravel = groundTangent.y * loadDirection;
        float steepness = Mathf.Clamp01(Mathf.Abs(groundTangent.y));

        if (verticalTravel > 0.001f)
        {
            return Mathf.Lerp(
                1f,
                Mathf.Max(1f, uphillHoldingMultiplier),
                steepness);
        }

        if (verticalTravel < -0.001f)
        {
            return Mathf.Lerp(
                1f,
                Mathf.Clamp(downhillHoldingMultiplier, 0.01f, 1f),
                steepness);
        }

        return 1f;
    }

    private void ApplyDraggingResistance(
        Rigidbody2D rb,
        Vector2 groundTangent,
        float tangentialVelocity)
    {
        float absoluteSpeed = Mathf.Abs(tangentialVelocity);

        if (absoluteSpeed <= minimumSlidingSpeed)
        {
            currentDragResistanceForceNewtons = 0f;
            isDragging = false;
            return;
        }

        float dt = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        float requiredStopImpulse = -tangentialVelocity * rb.mass;
        float maximumHoldingImpulse =
            Mathf.Max(0f, currentEffectiveHoldingForceNewtons) * dt;

        float appliedImpulse =
            Mathf.Clamp(
                requiredStopImpulse,
                -maximumHoldingImpulse,
                maximumHoldingImpulse);

        if (Mathf.Abs(appliedImpulse) > 0f)
        {
            rb.AddForce(
                groundTangent * appliedImpulse,
                ForceMode2D.Impulse);
        }

        currentDragResistanceForceNewtons =
            Mathf.Abs(appliedImpulse) / dt;

        isDragging =
            Mathf.Abs(requiredStopImpulse) >
            maximumHoldingImpulse + 0.0001f;
    }

    private bool TryGetGroundFrame(out Vector2 normal, out Vector2 tangent)
    {
        normal = Vector2.zero;
        tangent = Vector2.right;

        if (_validGroundContacts.Count == 0)
            return false;

        foreach (KeyValuePair<Collider2D, Vector2> pair in _validGroundContacts)
        {
            if (pair.Key != null)
                normal += pair.Value;
        }

        if (normal.sqrMagnitude <= 0.0001f)
            return false;

        normal.Normalize();

        if (normal.y < minimumGroundNormalY)
            return false;

        tangent = new Vector2(normal.y, -normal.x);

        if (tangent.sqrMagnitude <= 0.0001f)
            tangent = Vector2.right;
        else
            tangent.Normalize();

        if (tangent.x < 0f)
            tangent = -tangent;

        return true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        EvaluateGroundContact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        EvaluateGroundContact(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision != null && collision.collider != null)
            _validGroundContacts.Remove(collision.collider);

        // Do not release an active hold here. The HingeJoint2D is authoritative
        // once set, so one-frame collision-contact flicker is irrelevant.
    }

    private void EvaluateGroundContact(Collision2D collision)
    {
        if (collision == null || collision.collider == null)
            return;

        Collider2D collider = collision.collider;
        int layerBit = 1 << collider.gameObject.layer;

        if ((validSeafloorLayers.value & layerBit) == 0)
        {
            _validGroundContacts.Remove(collider);
            return;
        }

        Vector2 summedNormal = Vector2.zero;
        int validContactCount = 0;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 contactNormal = collision.GetContact(i).normal;

            if (contactNormal.y < minimumGroundNormalY)
                continue;

            summedNormal += contactNormal;
            validContactCount++;
        }

        if (validContactCount <= 0 ||
            summedNormal.sqrMagnitude <= 0.0001f)
        {
            _validGroundContacts.Remove(collider);
            return;
        }

        _validGroundContacts[collider] = summedNormal.normalized;
    }

    private void ClearRuntimeDebug()
    {
        currentGroundNormal = Vector2.up;
        currentGroundTangent = Vector2.right;
        currentSlopeDegrees = 0f;
        currentTangentialSpeed = 0f;
        currentAnchorMass = 0f;
        currentFlatHoldingForceNewtons = 0f;
        currentSlopeHoldingMultiplier = 1f;
        currentEffectiveHoldingForceNewtons = 0f;
        currentTangentialTetherFactor = 0f;
        currentStaticJointBreakForceNewtons = 0f;
        currentDragResistanceForceNewtons = 0f;
        isDragging = false;
    }

    private void ApplyCenterOfMass()
    {
        if (centerOfMassPoint == null)
            return;

        Rigidbody2D rb =
            Rigidbody != null
                ? Rigidbody
                : GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        rb.centerOfMass =
            rb.transform.InverseTransformPoint(centerOfMassPoint.position);
    }
}
