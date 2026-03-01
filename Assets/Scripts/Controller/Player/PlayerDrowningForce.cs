using UnityEngine;

/// <summary>
/// Applies a slow "drowning tug" when energy is depleted in water.
/// This does NOT kill the player; it just makes maintaining the surface hard,
/// so the Air/Breath system becomes the actual death mechanism.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDrowningForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 60;

    [Header("Refs")]
    [SerializeField] private PlayerExertionEnergyState energy;
    [SerializeField] private PlayerSubmersionState submersion;
    [SerializeField] private CharacterMotor2D motor; // for grounded checks (shallow water)

    [Header("Activation")]
    [Tooltip("Only apply while not grounded (prevents shallow-water grief).")]
    public bool requireNotGrounded = true;

    [Tooltip("Only apply when energy is at/under this value (units).")]
    public float energyZeroEpsilon = 0.001f;

    [Tooltip("Need at least this much submersion to count as 'in water' for tugging.")]
    [Range(0f, 1f)] public float minSubmersionToApply = 0.10f;

    [Header("Tug Episode Timing")]
    [Tooltip("Seconds before the next tug episode begins (average).")]
    [Min(0.05f)] public float timeBetweenTugs = 1.0f;

    [Tooltip("Random +/- jitter on timeBetweenTugs.")]
    [Min(0f)] public float betweenTugsJitter = 0.35f;

    [Tooltip("How long a tug episode lasts.")]
    [Min(0.05f)] public float tugDuration = 2.5f;

    [Tooltip("Random +/- jitter on tugDuration.")]
    [Min(0f)] public float tugDurationJitter = 0.4f;

    [Header("Force")]
    [Tooltip("Downward acceleration while tugging (m/s^2). Scaled by mass internally for stability.")]
    [Min(0f)] public float tugDownAcceleration = 7.0f;

    [Tooltip("If true, scale tug strength by how submerged you are (more submerged => stronger).")]
    public bool scaleBySubmersion = true;

    [Tooltip("Minimum and maximum scale when scaleBySubmersion is enabled.")]
    [Range(0f, 2f)] public float submersionScaleMin = 0.55f;
    [Range(0f, 2f)] public float submersionScaleMax = 1.15f;

    [Header("Smoothing")]
    [Tooltip("0..0.5 fraction of episode used to ease in/out (higher = softer tug edges).")]
    [Range(0f, 0.5f)] public float easeFraction = 0.25f;

    [Header("Escalation (0 energy ramp)")]
    [Tooltip("Seconds continuously at 0 energy (in water) to reach max escalation.")]
    [Min(0.1f)] public float escalationTimeToMax = 12f;

    [Tooltip("Curve mapping 0..1 escalation -> multiplier. If not using curve, uses linear.")]
    public bool useEscalationCurve = true;

    public AnimationCurve escalationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 2.25f);

    [Tooltip("Maximum multiplier applied to downward acceleration.")]
    [Min(1f)] public float maxAccelMultiplier = 3.0f;

    [Tooltip("Maximum multiplier applied to tug duration (longer tugs).")]
    [Min(1f)] public float maxDurationMultiplier = 1.6f;

    [Tooltip("Minimum multiplier applied to time between tugs (smaller = more frequent).")]
    [Range(0.05f, 1f)] public float minBetweenTugsMultiplier = 0.45f;

    [Tooltip("If true, escalation resets when grounded (useful if you consider 'standing' a recovery).")]
    public bool resetEscalationWhenGrounded = true;

    // Accumulates while continuously "eligible" (0 energy, in water, etc.)
    private float _zeroEnergySeconds;

    // Episode state
    private float _cooldownTimer;
    private float _tugTimer;
    private float _tugTotalDuration;

    void Awake()
    {
        if (!energy) energy = GetComponent<PlayerExertionEnergyState>();
        if (!submersion) submersion = GetComponent<PlayerSubmersionState>();
        if (!motor) motor = GetComponent<CharacterMotor2D>();

        ResetEpisodeOnly();
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;
        if (energy == null || submersion == null) return;

        // Refresh grounded if possible (motor might be stale otherwise)
        if (motor != null)
            motor.UpdateGrounded();

        bool inWater = submersion.InWater;
        if (!inWater)
        {
            ResetAll();
            return;
        }

        if (energy.energyCurrent > energyZeroEpsilon)
        {
            ResetAll();
            return;
        }

        bool grounded = (motor != null) && motor.IsGrounded;
        if (requireNotGrounded && grounded)
        {
            // You chose whether grounded should reset escalation.
            if (resetEscalationWhenGrounded) ResetAll();
            else ResetEpisodeOnly();
            return;
        }

        float sub01 = Mathf.Clamp01(submersion.Submersion01);
        if (sub01 < minSubmersionToApply)
        {
            ResetAll();
            return;
        }

        float dt = Time.fixedDeltaTime;

        // Escalation accumulates while continuously at 0 energy in water.
        _zeroEnergySeconds += dt;

        // If not currently tugging, count down cooldown and start a tug.
        if (_tugTimer <= 0f)
        {
            _cooldownTimer -= dt;
            if (_cooldownTimer <= 0f)
                StartTugEpisode();
            else
                return;
        }

        // Tug in progress
        _tugTimer -= dt;

        float progress01 = 1f - Mathf.Clamp01(_tugTimer / Mathf.Max(0.0001f, _tugTotalDuration));
        float envelope = EvaluateEnvelope(progress01); // smooth in/out

        float subScale = 1f;
        if (scaleBySubmersion)
            subScale = Mathf.Lerp(submersionScaleMin, submersionScaleMax, sub01);

        // Escalation multiplier (ramps toward a max)
        float esc = EvaluateEscalationMultiplier();
        float accelMult = Mathf.Clamp(esc, 1f, maxAccelMultiplier);

        // Convert acceleration to force: F = m*a
        float downForce = body.Mass * tugDownAcceleration * accelMult * subScale * envelope;

        body.rb.AddForce(Vector2.down * downForce, ForceMode2D.Force);

        // If the episode ended, schedule next cooldown (without resetting escalation!)
        if (_tugTimer <= 0f)
            ResetEpisodeOnly();
    }

    private void StartTugEpisode()
    {
        float esc = EvaluateEscalationMultiplier();
        float durMult = Mathf.Clamp(esc, 1f, maxDurationMultiplier);

        _tugTotalDuration = Mathf.Max(
            0.05f,
            (tugDuration * durMult) + Jitter(tugDurationJitter)
        );

        _tugTimer = _tugTotalDuration;
    }

    // Resets just the episode timers (cooldown/tug), but preserves escalation.
    private void ResetEpisodeOnly()
    {
        _tugTimer = 0f;
        _tugTotalDuration = 0f;

        _cooldownTimer = Mathf.Max(0.05f, timeBetweenTugs + Jitter(betweenTugsJitter));

        // More escalation => shorter downtime between tugs (more frequent).
        float betweenMult = Mathf.Lerp(1f, minBetweenTugsMultiplier, Escalation01);
        _cooldownTimer *= betweenMult;
    }

    // Resets both episode timers AND escalation.
    private void ResetAll()
    {
        _zeroEnergySeconds = 0f;
        ResetEpisodeOnly();
    }

    private float Jitter(float amount)
    {
        if (amount <= 0f) return 0f;
        return Random.Range(-amount, amount);
    }

    // Smooth envelope: ramps up, holds, ramps down.
    private float EvaluateEnvelope(float t01)
    {
        t01 = Mathf.Clamp01(t01);

        float e = Mathf.Clamp01(easeFraction);
        if (e <= 0.0001f) return 1f;

        float inEnd = e;
        float outStart = 1f - e;

        if (t01 < inEnd)
        {
            float x = t01 / inEnd;
            return Smooth01(x);
        }

        if (t01 > outStart)
        {
            float x = (1f - t01) / e;
            return Smooth01(x);
        }

        return 1f;
    }

    private float Smooth01(float x)
    {
        // Smoothstep
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private float Escalation01 => Mathf.Clamp01(_zeroEnergySeconds / Mathf.Max(0.001f, escalationTimeToMax));

    private float EvaluateEscalationMultiplier()
    {
        float t = Escalation01;

        // If no curve, do a basic linear ramp to ~2.25 (matches your curve default).
        float m = (useEscalationCurve && escalationCurve != null)
            ? escalationCurve.Evaluate(t)
            : Mathf.Lerp(1f, 2.25f, t);

        // Never less than 1 during eligible drowning, and clamp later by max multipliers.
        return Mathf.Max(1f, m);
    }
}