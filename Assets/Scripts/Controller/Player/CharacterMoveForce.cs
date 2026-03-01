using UnityEngine;

/// <summary>
/// Consumes intent and applies forces/impulses via IForceBody.
/// MP-safe: intent can come from local input or network.
/// Fix: latches JumpPressed so Update-pulse input isn't missed by FixedUpdate.
/// Adds: movement scaling based on body rotation.
/// </summary>
[RequireComponent(typeof(CharacterMotor2D))]
public class CharacterMoveForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 100;

    [Header("Movement Fallover Penalty")]
    [Tooltip("At this angle from upright (deg), movement reaches crawl speed.")]
    [SerializeField] private float crawlAngleDeg = 90f;

    [Tooltip("Beyond this angle from upright (deg), movement is disabled (e.g. inverted).")]
    [SerializeField] private float disabledAngleDeg = 100f;

    [Header("Sprint (Land)")]
    [SerializeField, Min(1f)] private float sprintSpeedMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float sprintForceMultiplier = 1.35f;

    [Tooltip("Movement multiplier at/near sideways (crawl).")]
    [Range(0f, 1f)]
    [SerializeField] private float crawlMoveMultiplier = 0.2f;

    [Header("Water Wading (non-swimming)")]
    [SerializeField, Range(0f, 1f)]
    private float wadeMinMoveMultiplier = 0.4f;

    [Header("Jump Angle Gate")]
    [SerializeField] private float maxJumpAngleDeg = 60f; // > this from upright disables jump

    [Header("Jump + Energy")]
    [SerializeField] private float jumpExertionImpulse01 = 0.18f;

    [SerializeField, Range(0f, 1f)]
    private float jumpAuthorityAtLowEnergy = 0.65f; // jump strength at lowEnergyThreshold

    [SerializeField, Range(0f, 1f)]
    private float jumpAuthorityAtZeroEnergy = 0.0f; // jump impossible at 0

    private CharacterMotor2D motor;
    private ICharacterIntentSource intentSource;
    private IForceBody body;

    // Jump press latch (solves Update vs FixedUpdate pulse drop)
    private bool _jumpPressedLatched;

    public void SetEnabled(bool value) => enabledFlag = value;

    void Awake()
    {
        motor = GetComponent<CharacterMotor2D>();
        body = GetComponent<IForceBody>();
        intentSource = GetComponent<ICharacterIntentSource>();

        if (body == null)
        {
            Debug.LogError("CharacterMoveForce requires IForceBody (use ForceBody2D).");
            enabled = false;
            return;
        }
        if (intentSource == null)
        {
            Debug.LogError("CharacterMoveForce requires an ICharacterIntentSource (e.g., LocalCharacterIntentSource).");
            enabled = false;
            return;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        float dt = Time.fixedDeltaTime;
        var intent = intentSource.Current;

        // Grounded first (used for jump latch rules)
        motor.UpdateGrounded();

        // Compute "uprightness" angle once
        float absFromUpright = Mathf.Abs(Mathf.DeltaAngle(body.rb.rotation, 0f));
        bool jumpAngleOk = absFromUpright <= maxJumpAngleDeg;

        // Do not allow jump to be queued while airborne
        if (!motor.IsGrounded)
            _jumpPressedLatched = false;

        // Only latch jump when:
        // - grounded (no in-air queueing)
        // - within allowed jump angle
        if (intent.JumpPressed && motor.IsGrounded && jumpAngleOk)
            _jumpPressedLatched = true;

        // If jump was pressed but not eligible, consume it so it can't linger (local input)
        if (intent.JumpPressed && (!motor.IsGrounded || !jumpAngleOk) && intentSource is LocalCharacterIntentSource localBadPress)
            localBadPress.ConsumeJumpPressed();

        motor.TickTimers(dt, _jumpPressedLatched);

        var ee = GetComponent<PlayerExertionEnergyState>();

        // --- Jump (grounded-only + angle-gated + energy-gated) ---
        if (_jumpPressedLatched && motor.IsGrounded && jumpAngleOk)
        {
            // No jump at 0 energy
            if (ee != null && ee.Energy01 <= 0.0001f)
            {
                _jumpPressedLatched = false;
                if (intentSource is LocalCharacterIntentSource localNoJump)
                    localNoJump.ConsumeJumpPressed();
            }
            else
            {
                _jumpPressedLatched = false;

                if (intentSource is LocalCharacterIntentSource local)
                    local.ConsumeJumpPressed();

                // Scale jump force with energy
                float jumpAuth = EvaluateJumpAuthority(ee);

                var v = body.rb.linearVelocity;
                if (v.y < 0f) v.y = 0f;
                body.rb.linearVelocity = v;

                body.AddForce(Vector2.up * ((motor.jumpImpulse * jumpAuth) * body.Mass / dt));

                // Exertion impulse from jump
                if (ee != null)
                    ee.AddExertionImpulse01(jumpExertionImpulse01);

                motor.TickTimers(0f, jumpPressed: false);
            }
        }

        // -------------------------------------------------
        // Horizontal movement (scaled by "uprightness")
        // -------------------------------------------------
        float moveScale = ComputeMoveScale(body.rb.rotation);

        float targetX = intent.MoveX;

        // Scale both speed cap and force so it *feels* like crawling, not ice-skating.
        float scaledMaxSpeed = motor.maxSpeed * moveScale;
        float scaledMoveForce = motor.moveForce * moveScale;

        var sub = GetComponent<PlayerSubmersionState>();
        if (sub != null && sub.InWater && !sub.SubmergedEnoughToSwim)
        {
            // As you approach swim threshold, you slow down.
            // 0..1 => 1..0.4 (tune)
            float wadeSlow = Mathf.Lerp(1f, wadeMinMoveMultiplier, sub.Wading01);

            scaledMaxSpeed *= wadeSlow;
            scaledMoveForce *= wadeSlow;
        }

        float moveAuth = (ee != null) ? ee.MoveAuthority : 1f;
        float sprintAuth = (ee != null) ? ee.SprintAuthority : 1f;

        // Sprint is fully disabled at 0 energy.
        bool canSprint = (ee == null) ? true : ee.CanSprint;

        // Only sprint if requested AND allowed.
        bool wantsSprint = intent.SprintHeld && canSprint;

        if (wantsSprint)
        {
            // Authority scales "how much sprint you actually get"
            float sprintLerp = sprintAuth; // 1 = full sprint, 0.3 = weak sprint
            scaledMaxSpeed *= Mathf.Lerp(1f, sprintSpeedMultiplier, sprintLerp);
            scaledMoveForce *= Mathf.Lerp(1f, sprintForceMultiplier, sprintLerp);
        }
        else
        {
            // Move authority always applies when not sprinting (exhaustion sluggishness)
            scaledMaxSpeed *= moveAuth;
            scaledMoveForce *= moveAuth;
        }

        if (moveScale > 0.0001f)
        {
            float vx = body.rb.linearVelocity.x;

            // Same logic, but using the scaled speed limit
            bool underSpeedLimit = Mathf.Abs(vx) < scaledMaxSpeed || Mathf.Sign(targetX) != Mathf.Sign(vx);

            if (Mathf.Abs(targetX) > 0.01f && underSpeedLimit)
            {
                body.AddForce(Vector2.right * (targetX * scaledMoveForce));
            }
        }
    }

    private float ComputeMoveScale(float currentAngleDeg)
    {
        // 0..180 (0 = upright, 90 = sideways, 180 = upside down)
        float absFromUpright = Mathf.Abs(Mathf.DeltaAngle(currentAngleDeg, 0f));

        // Fully disabled when too inverted
        if (absFromUpright >= disabledAngleDeg)
            return 0f;

        // Between sideways and disabled: fade from crawl to 0 (prevents a hard cutoff at 100)
        if (absFromUpright >= crawlAngleDeg)
        {
            float t = Mathf.InverseLerp(crawlAngleDeg, disabledAngleDeg, absFromUpright); // 0..1
            return Mathf.Lerp(crawlMoveMultiplier, 0f, t);
        }

        // 0..crawlAngle: fade from 1 to crawl
        {
            float t = Mathf.InverseLerp(0f, crawlAngleDeg, absFromUpright); // 0..1
            return Mathf.Lerp(1f, crawlMoveMultiplier, t);
        }
    }

    private float EvaluateJumpAuthority(PlayerExertionEnergyState ee)
    {
        if (ee == null) return 1f;

        float e = ee.Energy01; // works even with discrete energy
        if (e <= 0.0001f) return 0f;

        // Above low threshold: full jump
        if (e >= ee.lowEnergyThreshold) return 1f;

        // 0..lowThreshold mapped to [jumpAuthorityAtZeroEnergy..jumpAuthorityAtLowEnergy]
        float t = (ee.lowEnergyThreshold <= 0.0001f) ? 1f : (e / ee.lowEnergyThreshold);
        return Mathf.Lerp(jumpAuthorityAtZeroEnergy, jumpAuthorityAtLowEnergy, Mathf.Clamp01(t));
    }
}