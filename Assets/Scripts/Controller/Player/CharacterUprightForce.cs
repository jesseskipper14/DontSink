using UnityEngine;

/// <summary>
/// Applies a PD upright torque to keep the character generally upright in world space.
/// Allows small movement-based lean without hard-freezing Rigidbody2D rotation.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterUprightForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 90;

    [Header("Base Upright Target")]
    [Tooltip("Base target world rotation in degrees. 0 = upright.")]
    [SerializeField] private float baseTargetAngleDeg = 0f;

    [Header("Movement Lean")]
    [SerializeField] private bool enableMovementLean = true;

    [Tooltip("Maximum lean in degrees while walking/running. Positive MoveX leans right if Lean Sign is 1.")]
    [SerializeField] private float maxLeanDeg = 4f;

    [Tooltip("Use -1 if the lean direction feels backwards.")]
    [SerializeField] private float leanSign = -1f;

    [Tooltip("How quickly the desired lean changes.")]
    [SerializeField] private float leanSmoothSpeed = 10f;

    [Tooltip("Sprint can lean slightly more if desired.")]
    [SerializeField] private float sprintLeanMultiplier = 1.35f;

    [Tooltip("Lean only applies once horizontal speed is above this threshold.")]
    [SerializeField, Min(0f)]
    private float leanMinHorizontalSpeed = 0.35f;

    [Tooltip("Horizontal speed at which lean reaches full strength.")]
    [SerializeField, Min(0.01f)]
    private float leanFullHorizontalSpeed = 2.5f;

    [Tooltip("If true, lean direction comes from actual Rigidbody velocity instead of input.")]
    [SerializeField]
    private bool leanUsesVelocityDirection = true;

    [Header("Controller Tuning")]
    [Tooltip("How strongly angle error is corrected. Higher = stands up harder.")]
    [SerializeField] private float kP = 75f;

    [Tooltip("How strongly angular velocity is damped. Higher = less wobble.")]
    [SerializeField] private float kD = 14f;

    [Tooltip("Clamp torque to avoid violent correction.")]
    [SerializeField] private float maxTorque = 250f;

    [Tooltip("Ignore tiny angle errors.")]
    [SerializeField] private float deadZoneDeg = 1.5f;

    [Header("State Scaling")]
    [SerializeField] private bool disableWhileClimbing = true;
    [SerializeField] private bool reduceWhileSwimming = true;

    [SerializeField, Range(0f, 1f)]
    private float swimmingTorqueMultiplier = 0.15f;

    [SerializeField, Range(0f, 1f)]
    private float wadingTorqueMultiplier = 0.65f;

    [Header("Optional Input Assist")]
    [SerializeField] private bool uprightInputBoost = true;

    [SerializeField, Min(1f)]
    private float uprightHeldMultiplier = 1.5f;

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
        if (stateMultiplier <= 0.0001f)
            return;

        CharacterIntent intent = _intentSource.Current;

        float targetAngleDeg = ResolveTargetAngle(intent);

        Rigidbody2D rb = body.rb;

        float current = rb.rotation;
        float error = Mathf.DeltaAngle(current, targetAngleDeg);

        if (Mathf.Abs(error) <= deadZoneDeg)
            return;

        float angVel = rb.angularVelocity;

        float torque = (error * kP) - (angVel * kD);

        if (uprightInputBoost && intent.UprightHeld)
            torque *= uprightHeldMultiplier;

        torque *= stateMultiplier;
        torque = Mathf.Clamp(torque, -maxTorque, maxTorque);

        rb.AddTorque(torque);

        if (debugLogs)
        {
            Debug.Log(
                $"[CharacterUprightForce:{name}] current={current:F1}, target={targetAngleDeg:F1}, " +
                $"error={error:F1}, lean={_smoothedLeanDeg:F1}, angVel={angVel:F1}, " +
                $"torque={torque:F1}, stateMult={stateMultiplier:F2}",
                this);
        }
    }

    private float ResolveTargetAngle(CharacterIntent intent)
    {
        if (!enableMovementLean)
        {
            _smoothedLeanDeg = Mathf.Lerp(
                _smoothedLeanDeg,
                0f,
                1f - Mathf.Exp(-leanSmoothSpeed * Time.fixedDeltaTime));

            return baseTargetAngleDeg;
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

        float speed01 = Mathf.InverseLerp(
            leanMinHorizontalSpeed,
            leanFullHorizontalSpeed,
            horizontalSpeed);

        if (horizontalSpeed < leanMinHorizontalSpeed)
            speed01 = 0f;

        float multiplier = intent.SprintHeld ? sprintLeanMultiplier : 1f;

        float desiredLean = leanDirection * maxLeanDeg * multiplier * leanSign * speed01;

        _smoothedLeanDeg = Mathf.Lerp(
            _smoothedLeanDeg,
            desiredLean,
            1f - Mathf.Exp(-leanSmoothSpeed * Time.fixedDeltaTime));

        return baseTargetAngleDeg + _smoothedLeanDeg;
    }

    private float ResolveStateMultiplier()
    {
        if (!reduceWhileSwimming || _submersion == null)
            return 1f;

        if (!_submersion.InWater)
            return 1f;

        if (_submersion.SubmergedEnoughToSwim)
            return swimmingTorqueMultiplier;

        return wadingTorqueMultiplier;
    }
}