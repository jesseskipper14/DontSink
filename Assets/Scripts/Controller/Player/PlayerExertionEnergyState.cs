using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerExertionEnergyState : MonoBehaviour
{
    public enum ExertionState
    {
        Resting,
        Calm,
        Active,
        Winded,
        Exerted,
        Redlining
    }

    public enum EnergyState
    {
        Full,
        Good,
        Okay,
        Low,
        Critical,
        Empty
    }

    public EnergyState CurrentEnergyState { get; private set; } = EnergyState.Full;

    [Header("Refs")]
    [SerializeField] private MonoBehaviour intentSourceBehaviour; // must implement ICharacterIntentSource
    [SerializeField] private PlayerSubmersionState submersion;    // optional but recommended
    [SerializeField] private CharacterMotor2D motor; // optional

    private ICharacterIntentSource _intent;

    // -----------------------------
    // Exertion model (0..1)
    // -----------------------------
    [Header("Exertion")]
    [Range(0f, 1f)] public float exertion01 = 0f;

    [Header("Exertion Ceilings (0..1)")]
    [Range(0f, 1f)] public float restCeiling = 0.08f;
    [Range(0f, 1f)] public float walkCeiling = 0.45f;         // can walk forever, never redlines
    [Range(0f, 1f)] public float sprintCeiling = 0.98f;       // sustained sprint trends toward redline
    [Range(0f, 1f)] public float swimCeiling = 0.75f;         // normal swim trends to exerted
    [Range(0f, 1f)] public float sprintSwimCeiling = 1.00f;   // sprint-swim can reach max

    [Range(0f, 1f)] public float diveCeilingBonus = 0.08f;    // extra ceiling while diving

    [Header("Exertion Rates (per second)")]
    [Min(0f)] public float restApproachRate = 2.0f;           // how fast you return to resting baseline
    [Min(0f)] public float activityApproachRate = 0.8f;       // how fast exertion rises toward ceiling during activity
    [Min(0f)] public float sprintApproachRate = 1.2f;         // sprint rises faster
    [Min(0f)] public float swimApproachRate = 1.0f;           // swim rises moderately
    [Min(0f)] public float sprintSwimApproachRate = 1.6f;     // sprint-swim rises fast (panic mode)

    [Header("Treading Water")]
    public bool enableTreadingWater = true;

    [Tooltip("How far above regenThreshold we sit while treading water (prevents energy regen).")]
    [Range(0f, 0.3f)] public float treadAboveRegen = 0.05f;

    [Tooltip("How quickly exertion approaches the tread ceiling.")]
    [Min(0f)] public float treadApproachRate = 1.0f;

    [Tooltip("Factor of exertion of regular treading needed to tread underwater")]
    [Min(0f)] public float underwaterTreadFactor = 0.2f;

    // -----------------------------
    // Energy model (0..1)
    // -----------------------------
    [Header("Energy (Discrete)")]
    [Min(0f)] public float energyMax = 100f;
    [Min(0f)] public float energyCurrent = 100f;

    public float Energy01 => (energyMax <= 0.0001f) ? 0f : Mathf.Clamp01(energyCurrent / energyMax);
    public bool CanSprint => energyCurrent > 0.0001f;

    [Tooltip("Exertion above this begins draining energy.")]
    [Range(0f, 1f)] public float drainThreshold = 0.70f;

    [Tooltip("Base drain rate (energy per second) at threshold. Actual drain scales by curve below.")]
    [Min(0f)] public float baseDrainPerSecond = 4f;

    [Tooltip("How strongly drain ramps as exertion approaches 1.0. (Power curve).")]
    [Min(1f)] public float drainPower = 2.0f;

    [Tooltip("Energy regen per second while calm (exertion below regenThreshold).")]
    [Min(0f)] public float regenPerSecond = 3f;

    [Tooltip("Exertion below this allows energy regen.")]
    [Range(0f, 1f)] public float regenThreshold = 0.40f;

    [Tooltip("Extra regen per second when fully resting (no movement input).")]
    [Min(0f)] public float restingRegenBonus = 2f;

    [Tooltip("Optional land bonus regen. If you want 'land always feels recoverable'.")]
    [Min(0f)] public float landRegenBonus = 1f;

    [Header("Energy State Thresholds (UI)")]
    [Range(0f, 1f)] public float energyGood = 0.80f;
    [Range(0f, 1f)] public float energyOkay = 0.60f;
    [Range(0f, 1f)] public float energyLow = 0.40f;
    [Range(0f, 1f)] public float energyCritical = 0.20f;

    // -----------------------------
    // Control authority scaling
    // -----------------------------
    [Header("Control Authority (Energy -> Output)")]
    [Tooltip("Below this energy, authority starts to fall off.")]
    [Range(0f, 1f)] public float lowEnergyThreshold = 0.20f;

    [Tooltip("Authority scale at lowEnergyThreshold (e.g. 0.2 energy => 0.65 authority).")]
    [Range(0f, 1f)] public float authorityAtLowThreshold = 0.65f;

    [Tooltip("Authority scale at 0 energy.")]
    [Range(0f, 1f)] public float authorityAtZero = 0.30f;

    [Tooltip("Optional non-linear mapping. X=energy01 (0..1), Y=authority (0..1). If enabled, overrides threshold mapping.")]
    public bool useAuthorityCurve = false;

    public AnimationCurve authorityCurve = AnimationCurve.Linear(0f, 0.3f, 1f, 1f);

    [Header("Derived Scales (read-only)")]
    [SerializeField] private float _moveAuthority = 1f;
    [SerializeField] private float _sprintAuthority = 1f;
    [SerializeField] private float _swimUpAuthority = 1f;

    public float MoveAuthority => _moveAuthority;
    public float SprintAuthority => _sprintAuthority;
    public float SwimUpAuthority => _swimUpAuthority;

    public ExertionState CurrentState { get; private set; } = ExertionState.Calm;

    void Awake()
    {
        if (!submersion) submersion = GetComponent<PlayerSubmersionState>();

        if (intentSourceBehaviour == null)
            intentSourceBehaviour = GetComponent<MonoBehaviour>(); // best effort

        _intent = intentSourceBehaviour as ICharacterIntentSource;
        if (_intent == null)
            _intent = GetComponent<ICharacterIntentSource>();

        if (_intent == null)
            Debug.LogError("PlayerExertionEnergyState requires an ICharacterIntentSource.");

        if (!motor) motor = GetComponent<CharacterMotor2D>();
    }

    void FixedUpdate()
    {
        if (_intent == null) return;

        float dt = Time.fixedDeltaTime;
        var intent = _intent.Current;

        bool inSwim = (submersion != null) && submersion.SubmergedEnoughToSwim;
        bool inWater = (submersion != null) && submersion.InWater;

        bool hasMoveInput = Mathf.Abs(intent.MoveX) > 0.01f;
        bool isSprinting = intent.SprintHeld && hasMoveInput;

        // -----------------------------
        // Exertion as "pressure toward ceiling"
        // -----------------------------

        float ceiling;
        float k; // approach rate

        if (motor != null)
            motor.UpdateGrounded();
        bool grounded = (motor != null) ? motor.IsGrounded : false;

        // "Treading water" = in water, not grounded, and not actively trying to swim up/dive/move.
        // Goal: exertion sits just above regen threshold so energy can't regen by idling in water.
        bool fullySubmerged = submersion != null && submersion.SubmergedEnoughToSwim;

        bool treading =
            enableTreadingWater &&
            inWater &&
            !grounded &&
            !hasMoveInput &&
            !intent.SwimUpHeld &&
            !intent.DiveHeld;

        if (treading)
        {
            if (fullySubmerged)
            {
                // Underwater neutral float = can recover energy
                ceiling = regenThreshold * underwaterTreadFactor; // below regen threshold
                k = treadApproachRate;
            }
            else
            {
                // Surface tread = just above regen threshold
                ceiling = Mathf.Clamp01(regenThreshold + treadAboveRegen);
                k = treadApproachRate;
            }
        }
        else if (!hasMoveInput && !intent.SwimUpHeld && !intent.DiveHeld)
        {
            ceiling = restCeiling;
            k = restApproachRate;
        }
        else if (inSwim)
        {
            if (isSprinting)
            {
                ceiling = sprintSwimCeiling;
                k = sprintSwimApproachRate;
            }
            else
            {
                ceiling = swimCeiling;
                k = swimApproachRate;
            }

            if (intent.DiveHeld)
                ceiling = Mathf.Clamp01(ceiling + diveCeilingBonus);
        }
        else
        {
            if (isSprinting)
            {
                ceiling = sprintCeiling;
                k = sprintApproachRate;
            }
            else
            {
                ceiling = walkCeiling;
                k = activityApproachRate;
            }
        }

        // First-order response toward ceiling:
        // alpha = 1 - exp(-k*dt) gives nice "time constant" behavior.
        float alpha = 1f - Mathf.Exp(-k * dt);
        exertion01 = Mathf.Lerp(exertion01, ceiling, alpha);
        exertion01 = Mathf.Clamp01(exertion01);

        // -----------------------------
        // Energy drain / regen
        // -----------------------------
        float drainUnits = 0f;
        if (exertion01 > drainThreshold)
        {
            float t = Mathf.InverseLerp(drainThreshold, 1f, exertion01);
            float scaled = Mathf.Pow(t, drainPower);
            drainUnits = baseDrainPerSecond * scaled;
        }

        float regenUnits = 0f;
        if (exertion01 < regenThreshold)
        {
            regenUnits = regenPerSecond;

            bool resting = !hasMoveInput && !intent.SwimUpHeld && !intent.DiveHeld;
            if (resting) regenUnits += restingRegenBonus;

            // ✅ Key rule: if you're floating in water (not grounded), no regen.
            bool floatingInWater = inWater && !grounded;
            if (floatingInWater)
                regenUnits = 0f;
            else
                regenUnits += landRegenBonus; // grounded on land OR grounded in shallow water
        }

        energyCurrent += (regenUnits - drainUnits) * dt;
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, energyMax);

        // -----------------------------
        // Authority scales
        // -----------------------------
        float authority = EvaluateAuthority(Energy01);

        // For now, use the same authority for move/sprint/swim-up.
        // Later you can separate curves if you want: sprint collapses faster than walk, etc.
        _moveAuthority = authority;
        _sprintAuthority = authority;
        _swimUpAuthority = authority;

        // -----------------------------
        // State label
        // -----------------------------
        CurrentState = ComputeState(exertion01);
        CurrentEnergyState = ComputeEnergyState(Energy01);
    }

    private float EvaluateAuthority(float e01)
    {
        if (useAuthorityCurve && authorityCurve != null)
            return Mathf.Clamp01(authorityCurve.Evaluate(Mathf.Clamp01(e01)));

        if (e01 >= lowEnergyThreshold) return 1f;

        // Map [0..lowThreshold] to [authorityAtZero..authorityAtLowThreshold]
        float t = (lowEnergyThreshold <= 0.0001f) ? 1f : (e01 / lowEnergyThreshold);
        return Mathf.Lerp(authorityAtZero, authorityAtLowThreshold, Mathf.Clamp01(t));
    }

    private ExertionState ComputeState(float e01)
    {
        // Tunable later if desired; these are just labels for now.
        if (e01 < 0.10f) return ExertionState.Resting;
        if (e01 < 0.30f) return ExertionState.Calm;
        if (e01 < 0.55f) return ExertionState.Active;
        if (e01 < 0.75f) return ExertionState.Winded;
        if (e01 < 0.90f) return ExertionState.Exerted;
        return ExertionState.Redlining;
    }

    private EnergyState ComputeEnergyState(float e01)
    {
        if (e01 <= 0.0001f) return EnergyState.Empty;
        if (e01 < energyCritical) return EnergyState.Critical;
        if (e01 < energyLow) return EnergyState.Low;
        if (e01 < energyOkay) return EnergyState.Okay;
        if (e01 < energyGood) return EnergyState.Good;
        return EnergyState.Full;
    }

    public void AddExertionImpulse01(float amount01)
    {
        if (amount01 <= 0f) return;
        exertion01 = Mathf.Clamp01(exertion01 + amount01);
    }
}