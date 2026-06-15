using Survival.Attributes;
using Survival.Vitals;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerExertionEnergyState : MonoBehaviour, IExertionRead
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
    [SerializeField] private CharacterMotor2D motor;              // optional
    [SerializeField] private PlayerAttributeState attributes;     // optional, falls back to local values if missing

    private ICharacterIntentSource _intent;
    private float _externalExertionCeiling01;
    private float _externalExertionApproachRate;

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

    public float Exertion01 => exertion01;

    // -----------------------------
    // Energy model (0..1)
    // -----------------------------
    [Header("Energy (Discrete)")]
    [Tooltip("Fallback max energy if PlayerAttributeState/profile is missing the ExertionEnergyMax attribute.")]
    [Min(0f)] public float energyMax = 100f;

    [Min(0f)] public float energyCurrent = 100f;

    [Header("Effective Energy (read-only)")]
    [SerializeField] private float _effectiveEnergyMax = 100f;

    public float EffectiveEnergyMax => Mathf.Max(0.0001f, _effectiveEnergyMax);

    public float Energy01 => (EffectiveEnergyMax <= 0.0001f)
        ? 0f
        : Mathf.Clamp01(energyCurrent / EffectiveEnergyMax);

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

    private void Awake()
    {
        if (!submersion)
            submersion = GetComponent<PlayerSubmersionState>();

        if (!motor)
            motor = GetComponent<CharacterMotor2D>();

        if (!attributes)
        {
            attributes =
                GetComponent<PlayerAttributeState>() ??
                GetComponentInParent<PlayerAttributeState>() ??
                GetComponentInChildren<PlayerAttributeState>(true);
        }

        if (intentSourceBehaviour == null)
            intentSourceBehaviour = GetComponent<MonoBehaviour>(); // best effort

        _intent = intentSourceBehaviour as ICharacterIntentSource;
        if (_intent == null)
            _intent = GetComponent<ICharacterIntentSource>();

        if (_intent == null)
            Debug.LogError("PlayerExertionEnergyState requires an ICharacterIntentSource.", this);

        RefreshEffectiveEnergyMax();
    }

    private void FixedUpdate()
    {
        if (_intent == null)
            return;

        float dt = Time.fixedDeltaTime;
        RefreshEffectiveEnergyMax();

        CharacterIntent intent = _intent.Current;

        bool inSwim = (submersion != null) && submersion.SubmergedEnoughToSwim;
        bool inWater = (submersion != null) && submersion.InWater;

        bool hasMoveInput = Mathf.Abs(intent.MoveX) > 0.01f;
        bool isSprinting = intent.SprintHeld && hasMoveInput;

        // Resolve effective attribute values once per tick.
        // Local inspector values remain fallback defaults so old prefabs/scenes do not explode. Sadly, explosions are bad here.
        float effRestCeiling = Attr01(PlayerAttributeId.ExertionRestCeiling, restCeiling);
        float effWalkCeiling = Attr01(PlayerAttributeId.ExertionWalkCeiling, walkCeiling);
        float effSprintCeiling = Attr01(PlayerAttributeId.ExertionSprintCeiling, sprintCeiling);
        float effSwimCeiling = Attr01(PlayerAttributeId.ExertionSwimCeiling, swimCeiling);
        float effSprintSwimCeiling = Attr01(PlayerAttributeId.ExertionSprintSwimCeiling, sprintSwimCeiling);
        float effDiveCeilingBonus = Attr01(PlayerAttributeId.ExertionDiveCeilingBonus, diveCeilingBonus);

        float effRestApproachRate = AttrMin(PlayerAttributeId.ExertionRestApproachRate, restApproachRate, 0f);
        float effActivityApproachRate = AttrMin(PlayerAttributeId.ExertionActivityApproachRate, activityApproachRate, 0f);
        float effSprintApproachRate = AttrMin(PlayerAttributeId.ExertionSprintApproachRate, sprintApproachRate, 0f);
        float effSwimApproachRate = AttrMin(PlayerAttributeId.ExertionSwimApproachRate, swimApproachRate, 0f);
        float effSprintSwimApproachRate = AttrMin(PlayerAttributeId.ExertionSprintSwimApproachRate, sprintSwimApproachRate, 0f);
        float effTreadApproachRate = AttrMin(PlayerAttributeId.ExertionTreadApproachRate, treadApproachRate, 0f);

        float effDrainThreshold = Attr01(PlayerAttributeId.ExertionDrainThreshold, drainThreshold);
        float effBaseDrainPerSecond = AttrMin(PlayerAttributeId.ExertionBaseDrainPerSecond, baseDrainPerSecond, 0f);
        float effDrainPower = AttrMin(PlayerAttributeId.ExertionDrainPower, drainPower, 1f);
        float effRegenPerSecond = AttrMin(PlayerAttributeId.ExertionRegenPerSecond, regenPerSecond, 0f);
        float effRegenThreshold = Attr01(PlayerAttributeId.ExertionRegenThreshold, regenThreshold);
        float effRestingRegenBonus = AttrMin(PlayerAttributeId.ExertionRestingRegenBonus, restingRegenBonus, 0f);
        float effLandRegenBonus = AttrMin(PlayerAttributeId.ExertionLandRegenBonus, landRegenBonus, 0f);

        // -----------------------------
        // Exertion as "pressure toward ceiling"
        // -----------------------------
        float ceiling;
        float k; // approach rate

        if (motor != null)
            motor.UpdateGrounded();

        bool grounded = (motor != null) && motor.IsGrounded;

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
                // Underwater neutral float = can recover energy.
                ceiling = Mathf.Clamp01(effRegenThreshold * underwaterTreadFactor);
                k = effTreadApproachRate;
            }
            else
            {
                // Surface tread = just above regen threshold.
                ceiling = Mathf.Clamp01(effRegenThreshold + treadAboveRegen);
                k = effTreadApproachRate;
            }
        }
        else if (!hasMoveInput && !intent.SwimUpHeld && !intent.DiveHeld)
        {
            ceiling = effRestCeiling;
            k = effRestApproachRate;
        }
        else if (inSwim)
        {
            if (isSprinting)
            {
                ceiling = effSprintSwimCeiling;
                k = effSprintSwimApproachRate;
            }
            else
            {
                ceiling = effSwimCeiling;
                k = effSwimApproachRate;
            }

            if (intent.DiveHeld)
                ceiling = Mathf.Clamp01(ceiling + effDiveCeilingBonus);
        }
        else
        {
            if (isSprinting)
            {
                ceiling = effSprintCeiling;
                k = effSprintApproachRate;
            }
            else
            {
                ceiling = effWalkCeiling;
                k = effActivityApproachRate;
            }
        }

        // External activity sources can raise the target ceiling without owning the whole model.
        // Example: ladder climbing.
        if (_externalExertionCeiling01 > 0f)
        {
            if (_externalExertionCeiling01 > ceiling)
                ceiling = _externalExertionCeiling01;

            if (_externalExertionApproachRate > k)
                k = _externalExertionApproachRate;
        }

        // First-order response toward ceiling:
        // alpha = 1 - exp(-k*dt) gives nice "time constant" behavior.
        float alpha = 1f - Mathf.Exp(-Mathf.Max(0f, k) * dt);
        exertion01 = Mathf.Lerp(exertion01, Mathf.Clamp01(ceiling), alpha);
        exertion01 = Mathf.Clamp01(exertion01);

        // -----------------------------
        // Energy drain / regen
        // -----------------------------
        float drainUnits = 0f;
        if (exertion01 > effDrainThreshold)
        {
            float t = Mathf.InverseLerp(effDrainThreshold, 1f, exertion01);
            float scaled = Mathf.Pow(t, effDrainPower);
            drainUnits = effBaseDrainPerSecond * scaled;
        }

        float regenUnits = 0f;
        if (exertion01 < effRegenThreshold)
        {
            regenUnits = effRegenPerSecond;

            bool resting = !hasMoveInput && !intent.SwimUpHeld && !intent.DiveHeld;
            if (resting)
                regenUnits += effRestingRegenBonus;

            // Rule: prevent energy regen while floating at the surface (treading),
            // but allow regen when fully submerged (underwater neutral float).
            bool floatingAtSurface = inWater && !grounded && !fullySubmerged;

            if (floatingAtSurface)
            {
                regenUnits = 0f;
            }
            else
            {
                // "Land bonus" applies when grounded OR when fully submerged (your "recover underwater" rule).
                regenUnits += effLandRegenBonus;
            }
        }

        energyCurrent += (regenUnits - drainUnits) * dt;
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, EffectiveEnergyMax);

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

        _externalExertionCeiling01 = 0f;
        _externalExertionApproachRate = 0f;
    }

    private float EvaluateAuthority(float e01)
    {
        if (useAuthorityCurve && authorityCurve != null)
            return Mathf.Clamp01(authorityCurve.Evaluate(Mathf.Clamp01(e01)));

        float effLowEnergyThreshold = Attr01(
            PlayerAttributeId.ExertionLowEnergyThreshold,
            lowEnergyThreshold);

        float effAuthorityAtLowThreshold = Attr01(
            PlayerAttributeId.ExertionAuthorityAtLowThreshold,
            authorityAtLowThreshold);

        float effAuthorityAtZero = Attr01(
            PlayerAttributeId.ExertionAuthorityAtZero,
            authorityAtZero);

        if (e01 >= effLowEnergyThreshold)
            return 1f;

        float t = (effLowEnergyThreshold <= 0.0001f)
            ? 1f
            : (e01 / effLowEnergyThreshold);

        return Mathf.Lerp(
            effAuthorityAtZero,
            effAuthorityAtLowThreshold,
            Mathf.Clamp01(t));
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
        if (amount01 <= 0f)
            return;

        exertion01 = Mathf.Clamp01(exertion01 + amount01);
    }

    public void AddExternalExertionDemand(float ceiling01, float approachRate)
    {
        ceiling01 = Mathf.Clamp01(ceiling01);
        approachRate = Mathf.Max(0f, approachRate);

        if (ceiling01 <= 0f || approachRate <= 0f)
            return;

        // Multiple systems can request exertion in the same tick.
        // Highest ceiling wins, strongest approach rate wins.
        if (ceiling01 > _externalExertionCeiling01)
            _externalExertionCeiling01 = ceiling01;

        if (approachRate > _externalExertionApproachRate)
            _externalExertionApproachRate = approachRate;
    }

    public void ResetState()
    {
        RefreshEffectiveEnergyMax();

        // Energy back to full.
        energyCurrent = EffectiveEnergyMax;
        CurrentEnergyState = EnergyState.Full;

        // Exertion back to resting.
        exertion01 = 0f;
        CurrentState = ExertionState.Resting;

        _moveAuthority = 1f;
        _sprintAuthority = 1f;
        _swimUpAuthority = 1f;

        _externalExertionCeiling01 = 0f;
        _externalExertionApproachRate = 0f;
    }

    private void RefreshEffectiveEnergyMax()
    {
        _effectiveEnergyMax = Mathf.Max(
            0.0001f,
            Attr(PlayerAttributeId.ExertionEnergyMax, energyMax));

        if (energyCurrent > _effectiveEnergyMax)
            energyCurrent = _effectiveEnergyMax;
    }

    private float Attr(PlayerAttributeId id, float fallback)
    {
        return attributes != null
            ? attributes.GetFloat(id, fallback)
            : fallback;
    }

    private float AttrMin(PlayerAttributeId id, float fallback, float min)
    {
        return Mathf.Max(min, Attr(id, fallback));
    }

    private float Attr01(PlayerAttributeId id, float fallback)
    {
        return Mathf.Clamp01(Attr(id, fallback));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        energyMax = Mathf.Max(0f, energyMax);
        energyCurrent = Mathf.Max(0f, energyCurrent);
        _effectiveEnergyMax = Mathf.Max(0.0001f, _effectiveEnergyMax);

        restApproachRate = Mathf.Max(0f, restApproachRate);
        activityApproachRate = Mathf.Max(0f, activityApproachRate);
        sprintApproachRate = Mathf.Max(0f, sprintApproachRate);
        swimApproachRate = Mathf.Max(0f, swimApproachRate);
        sprintSwimApproachRate = Mathf.Max(0f, sprintSwimApproachRate);
        treadApproachRate = Mathf.Max(0f, treadApproachRate);

        baseDrainPerSecond = Mathf.Max(0f, baseDrainPerSecond);
        drainPower = Mathf.Max(1f, drainPower);
        regenPerSecond = Mathf.Max(0f, regenPerSecond);
        restingRegenBonus = Mathf.Max(0f, restingRegenBonus);
        landRegenBonus = Mathf.Max(0f, landRegenBonus);
        underwaterTreadFactor = Mathf.Max(0f, underwaterTreadFactor);
    }
#endif
}
