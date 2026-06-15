using Survival.Attributes;
using UnityEngine;

/// <summary>
/// Applies a PD upright torque to keep the character generally upright in world space.
/// Allows small movement-based lean without hard-freezing Rigidbody2D rotation.
///
/// Profile-driven:
/// - Numeric balance/upright tuning reads from PlayerAttributeState when available.
/// - Local inspector fields are fallback defaults only.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterUprightForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 90;

    [Header("Refs")]
    [Tooltip("Optional. If assigned/found, numeric upright tuning reads effective values from PlayerAttributeProfile + PlayerBuff modifiers. Local fields below are fallbacks.")]
    [SerializeField] private PlayerAttributeState attributes;

    [Header("Base Upright Target")]
    [Tooltip("FALLBACK ONLY. Base target world rotation in degrees. 0 = upright. Profile attribute CharacterUprightTargetAngleOffsetDeg is added on top.")]
    [SerializeField] private float baseTargetAngleDeg = 0f;

    [Header("Movement Lean")]
    [SerializeField] private bool enableMovementLean = true;

    [Tooltip("FALLBACK ONLY. Maximum lean in degrees while walking/running. Profile: CharacterMovementLeanMaxDeg.")]
    [SerializeField] private float maxLeanDeg = 4f;

    [Tooltip("Local behavior setting. Use -1 if the lean direction feels backwards.")]
    [SerializeField] private float leanSign = -1f;

    [Tooltip("FALLBACK ONLY. How quickly the desired lean changes. Profile: CharacterLeanSmoothSpeed.")]
    [SerializeField] private float leanSmoothSpeed = 10f;

    [Tooltip("FALLBACK ONLY. Sprint can lean slightly more if desired. Profile: CharacterSprintLeanMultiplier.")]
    [SerializeField] private float sprintLeanMultiplier = 1.35f;

    [Tooltip("FALLBACK ONLY. Lean only applies once horizontal speed is above this threshold. Profile: CharacterLeanMinHorizontalSpeed.")]
    [SerializeField, Min(0f)]
    private float leanMinHorizontalSpeed = 0.35f;

    [Tooltip("FALLBACK ONLY. Horizontal speed at which lean reaches full strength. Profile: CharacterLeanFullHorizontalSpeed.")]
    [SerializeField, Min(0.01f)]
    private float leanFullHorizontalSpeed = 2.5f;

    [Tooltip("Local behavior setting. If true, lean direction comes from actual Rigidbody velocity instead of input.")]
    [SerializeField]
    private bool leanUsesVelocityDirection = true;

    [Header("Controller Tuning")]
    [Tooltip("FALLBACK ONLY. How strongly angle error is corrected. Higher = stands up harder. Profile: CharacterUprightStrength.")]
    [SerializeField] private float kP = 75f;

    [Tooltip("FALLBACK ONLY. How strongly angular velocity is damped. Higher = less wobble. Profile: CharacterUprightDamping.")]
    [SerializeField] private float kD = 14f;

    [Tooltip("FALLBACK ONLY. Clamp torque to avoid violent correction. Profile: CharacterUprightMaxTorque.")]
    [SerializeField] private float maxTorque = 250f;

    [Tooltip("FALLBACK ONLY. Ignore tiny angle errors. Profile: CharacterUprightDeadZoneDeg.")]
    [SerializeField] private float deadZoneDeg = 1.5f;

    [Header("State Scaling")]
    [SerializeField] private bool disableWhileClimbing = true;
    [SerializeField] private bool reduceWhileSwimming = true;

    [Tooltip("FALLBACK ONLY. Torque multiplier while swimming. Profile: CharacterSwimmingTorqueMultiplier.")]
    [SerializeField, Range(0f, 1f)]
    private float swimmingTorqueMultiplier = 0.15f;

    [Tooltip("FALLBACK ONLY. Torque multiplier while wading. Profile: CharacterWadingTorqueMultiplier.")]
    [SerializeField, Range(0f, 1f)]
    private float wadingTorqueMultiplier = 0.65f;

    [Header("Optional Input Assist")]
    [SerializeField] private bool uprightInputBoost = true;

    [Tooltip("FALLBACK ONLY. Upright-held torque multiplier. Profile: CharacterUprightHeldMultiplier.")]
    [SerializeField, Min(1f)]
    private float uprightHeldMultiplier = 1.5f;

    [Header("Effective Values (read-only debug)")]
    [SerializeField] private float _effectiveTargetAngleOffsetDeg;
    [SerializeField] private float _effectiveKP;
    [SerializeField] private float _effectiveKD;
    [SerializeField] private float _effectiveMaxTorque;
    [SerializeField] private float _effectiveDeadZoneDeg;
    [SerializeField] private float _effectiveStateMultiplier = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private ICharacterIntentSource _intentSource;
    private IForceBody _body;
    private PlayerLadderClimber _ladderClimber;
    private PlayerSubmersionState _submersion;

    private float _smoothedLeanDeg;

    private void Awake()
    {
        _intentSource = GetComponent<ICharacterIntentSource>();
        _body = GetComponent<IForceBody>();
        _ladderClimber = GetComponent<PlayerLadderClimber>();
        _submersion = GetComponent<PlayerSubmersionState>();

        if (!attributes)
        {
            attributes =
                GetComponent<PlayerAttributeState>() ??
                GetComponentInParent<PlayerAttributeState>() ??
                GetComponentInChildren<PlayerAttributeState>(true);
        }

        if (_body == null)
        {
            Debug.LogError("[CharacterUprightForce] Requires IForceBody, e.g. ForceBody2D.", this);
            enabled = false;
            return;
        }

        if (_intentSource == null)
        {
            Debug.LogError("[CharacterUprightForce] Requires ICharacterIntentSource.", this);
            enabled = false;
            return;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag)
            return;

        if (body == null || body.rb == null)
            return;

        if (disableWhileClimbing && _ladderClimber != null && _ladderClimber.IsClimbing)
            return;

        float stateMultiplier = ResolveStateMultiplier();
        _effectiveStateMultiplier = stateMultiplier;

        if (stateMultiplier <= 0.0001f)
            return;

        CharacterIntent intent = _intentSource.Current;

        float targetAngleDeg = ResolveTargetAngle(intent);

        Rigidbody2D rb = body.rb;

        float current = rb.rotation;
        float error = Mathf.DeltaAngle(current, targetAngleDeg);

        float effDeadZoneDeg = Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterUprightDeadZoneDeg, deadZoneDeg));

        _effectiveDeadZoneDeg = effDeadZoneDeg;

        if (Mathf.Abs(error) <= effDeadZoneDeg)
            return;

        float effKP = Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterUprightStrength, kP));

        float effKD = Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterUprightDamping, kD));

        float effMaxTorque = Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterUprightMaxTorque, maxTorque));

        float effUprightHeldMultiplier = Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterUprightHeldMultiplier, uprightHeldMultiplier));

        _effectiveKP = effKP;
        _effectiveKD = effKD;
        _effectiveMaxTorque = effMaxTorque;

        float angVel = rb.angularVelocity;

        float torque = (error * effKP) - (angVel * effKD);

        if (uprightInputBoost && intent.UprightHeld)
            torque *= effUprightHeldMultiplier;

        torque *= stateMultiplier;
        torque = Mathf.Clamp(torque, -effMaxTorque, effMaxTorque);

        rb.AddTorque(torque);

        if (debugLogs)
        {
            Debug.Log(
                $"[CharacterUprightForce:{name}] current={current:F1}, target={targetAngleDeg:F1}, " +
                $"error={error:F1}, lean={_smoothedLeanDeg:F1}, angVel={angVel:F1}, " +
                $"torque={torque:F1}, stateMult={stateMultiplier:F2}, " +
                $"kP={effKP:F1}, kD={effKD:F1}, maxTorque={effMaxTorque:F1}",
                this);
        }
    }

    private float ResolveTargetAngle(CharacterIntent intent)
    {
        float targetOffsetDeg = Attr(
            PlayerAttributeId.CharacterUprightTargetAngleOffsetDeg,
            0f);

        _effectiveTargetAngleOffsetDeg = targetOffsetDeg;

        if (!enableMovementLean)
        {
            float effLeanSmoothSpeed = Mathf.Max(
                0f,
                Attr(PlayerAttributeId.CharacterLeanSmoothSpeed, leanSmoothSpeed));

            _smoothedLeanDeg = Mathf.Lerp(
                _smoothedLeanDeg,
                0f,
                1f - Mathf.Exp(-effLeanSmoothSpeed * Time.fixedDeltaTime));

            return baseTargetAngleDeg + targetOffsetDeg;
        }

        float horizontalSpeed = 0f;
        float leanDirection = 0f;

        if (_body != null && _body.rb != null)
        {
            horizontalSpeed = Mathf.Abs(_body.rb.linearVelocity.x);

            if (leanUsesVelocityDirection)
                leanDirection = Mathf.Sign(_body.rb.linearVelocity.x);
            else
                leanDirection = Mathf.Sign(intent.MoveX);
        }
        else
        {
            horizontalSpeed = Mathf.Abs(intent.MoveX);
            leanDirection = Mathf.Sign(intent.MoveX);
        }

        float effLeanMinHorizontalSpeed = Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterLeanMinHorizontalSpeed, leanMinHorizontalSpeed));

        float effLeanFullHorizontalSpeed = Mathf.Max(
            effLeanMinHorizontalSpeed + 0.01f,
            Attr(PlayerAttributeId.CharacterLeanFullHorizontalSpeed, leanFullHorizontalSpeed));

        float speed01 = Mathf.InverseLerp(
            effLeanMinHorizontalSpeed,
            effLeanFullHorizontalSpeed,
            horizontalSpeed);

        if (horizontalSpeed < effLeanMinHorizontalSpeed)
            speed01 = 0f;

        float effSprintLeanMultiplier = Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterSprintLeanMultiplier, sprintLeanMultiplier));

        float effMaxLeanDeg = Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterMovementLeanMaxDeg, maxLeanDeg));

        float multiplier = intent.SprintHeld ? effSprintLeanMultiplier : 1f;

        float desiredLean = leanDirection * effMaxLeanDeg * multiplier * leanSign * speed01;

        float effLeanSmoothSpeed2 = Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterLeanSmoothSpeed, leanSmoothSpeed));

        _smoothedLeanDeg = Mathf.Lerp(
            _smoothedLeanDeg,
            desiredLean,
            1f - Mathf.Exp(-effLeanSmoothSpeed2 * Time.fixedDeltaTime));

        return baseTargetAngleDeg + targetOffsetDeg + _smoothedLeanDeg;
    }

    private float ResolveStateMultiplier()
    {
        if (!reduceWhileSwimming || _submersion == null)
            return 1f;

        if (!_submersion.InWater)
            return 1f;

        if (_submersion.SubmergedEnoughToSwim)
        {
            return Mathf.Max(
                0f,
                Attr(PlayerAttributeId.CharacterSwimmingTorqueMultiplier, swimmingTorqueMultiplier));
        }

        return Mathf.Max(
            0f,
            Attr(PlayerAttributeId.CharacterWadingTorqueMultiplier, wadingTorqueMultiplier));
    }

    private float Attr(PlayerAttributeId id, float fallback)
    {
        return attributes != null
            ? attributes.GetFloat(id, fallback)
            : fallback;
    }
}
