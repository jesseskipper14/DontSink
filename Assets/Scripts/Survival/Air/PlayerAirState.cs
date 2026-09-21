using Survival.Attributes;
using System.Collections.Generic;
using UnityEngine;

namespace Survival.Vitals
{
    [DisallowMultipleComponent]
    public sealed class PlayerAirState : MonoBehaviour, IAirOxygenationRead
    {
        public enum AirState
        {
            HighQuality,
            NormalQuality,
            LowQuality,
            BadQuality,
            CriticalQuality
        }

        [Header("Attributes")]
        [SerializeField] private PlayerAttributeState attributes;

        [Header("Air Capacity (Units)")]
        [Min(0.1f)] public float baseMaxAir = 100f;
        [Min(0f)] public float airCurrent = 100f;

        public float MaxAir { get; private set; }
        public float Air01 => (MaxAir <= 0.0001f) ? 0f : Mathf.Clamp01(airCurrent / MaxAir);

        [Header("Lung Gas Quality (Holding Breath Model)")]
        [Range(0f, 1f)] public float lungGasQuality01 = 1f;
        public float LungGasQuality01 => lungGasQuality01;

        [Tooltip("How quickly lung gas quality recovers per second when oxygenating (surface/tank).")]
        [Min(0f)] public float lungQualityRecoverPerSecond = 1.25f;

        [Tooltip("Minimum lung quality floor.")]
        [Range(0f, 1f)] public float lungQualityMin = 0.0f;

        [Tooltip("Optional: delay before surface oxygenation is considered available.")]
        [Min(0f)] public float surfaceRegenDelay = 0f;

        private float _timeSinceSurfaced = 999f;

        [Header("Breath Hold Consumption (Demand-Based)")]
        [SerializeField] private PlayerExertionEnergyState exertion;

        [Tooltip("Quality consumed per second at demand=1 while holding breath.")]
        [Min(0f)] public float baseQualityConsumePerSecond = 0.05f;

        [Tooltip("Demand multiplier by exertion tier while holding breath.")]
        public ExertionTierMultipliers demandByExertion = new ExertionTierMultipliers
        {
            resting = 1.0f,
            calm = 1.1f,
            active = 1.3f,
            winded = 1.5f,
            exerted = 1.7f,
            redlining = 2.0f
        };

        [Header("State Thresholds (UI + Behaviors)")]
        [Range(0f, 1f)] public float lowThreshold = 0.35f;
        [Range(0f, 1f)] public float criticalThreshold = 0.15f;

        public AirState CurrentState { get; private set; } = AirState.HighQuality;

        [Header("Sources")]
        public bool autoCollectSources = true;

        private readonly List<IAirSource> _sources = new();
        public IReadOnlyList<IAirSource> Sources => _sources;

        private float _lastSourceFlowPerSecond;
        public bool CanOxygenate { get; private set; }

        [Range(0f, 1f)] public float oxygenQuality01 = 1f;
        public float OxygenQuality01 => oxygenQuality01;

        public bool IsUnderwater { get; set; }

        [Header("Diving Bell Ambient Context")]
        [Tooltip(
            "When enabled, a player occupying a DivingBellAirVolume uses that bell's " +
            "internal waterline and trapped-air quality instead of the global ocean " +
            "surface for ambient breathing.")]
        [SerializeField] private bool useDivingBellAmbientContext = true;

        private PlayerBellOccupantState _bellOccupantState;
        private PlayerSubmersionState _submersionState;
        private bool _divingBellAmbientOverrideActive;

        void Awake()
        {
            if (!attributes)
                attributes = GetComponent<PlayerAttributeState>();

            if (!exertion) exertion = GetComponent<PlayerExertionEnergyState>();

            ResolveDivingBellContextRefs();

            RebuildSources();
            RecomputeMaxAir();

            airCurrent = Mathf.Clamp(airCurrent, 0f, MaxAir);
        }

        public void RebuildSources()
        {
            _sources.Clear();
            if (!autoCollectSources) return;

            var monos = GetComponents<MonoBehaviour>();
            foreach (var m in monos)
                if (m is IAirSource src) _sources.Add(src);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            RecomputeMaxAir();

            // Establish bell ambient context BEFORE polling sources, then reassert it
            // after every source. Legacy ambient sources are allowed to update
            // IsUnderwater, so without this guard component order could make a scuba
            // source consume tank air while Steve's head is actually in a bell air pocket.
            ApplyDivingBellAmbientContext();

            float sourceFlow = 0f;
            for (int i = 0; i < _sources.Count; i++)
            {
                ApplyDivingBellAmbientContext();
                sourceFlow += _sources[i].GetAirFlowPerSecond(this, dt);
                ApplyDivingBellAmbientContext();
            }

            _lastSourceFlowPerSecond = sourceFlow;

            // Track surface time using the freshly resolved underwater state.
            if (!IsUnderwater) _timeSinceSurfaced += dt;
            else _timeSinceSurfaced = 0f;

            bool ambientOxygenation = !IsUnderwater && _timeSinceSurfaced >= surfaceRegenDelay;
            bool sourceProvides = IsUnderwater && _lastSourceFlowPerSecond > 0.001f;

            CanOxygenate = ambientOxygenation || sourceProvides;

            if (CanOxygenate)
            {
                // Recover lung gas quality toward environment/source quality
                float target = Mathf.Clamp01(OxygenQuality01);
                float recoverPerSecond = attributes != null
                    ? attributes.GetFloat(PlayerAttributeId.LungQualityRecoverPerSecond, lungQualityRecoverPerSecond)
                    : lungQualityRecoverPerSecond;

                lungGasQuality01 = MoveTowardExp(lungGasQuality01, target, recoverPerSecond, dt);

                // For now, lung volume stays full when oxygenating
                airCurrent = MaxAir;
            }
            else
            {
                // Holding breath: consume lung gas quality based on exertion-demand
                var tier = exertion != null ? exertion.CurrentState : PlayerExertionEnergyState.ExertionState.Calm;
                float demandMul = Mathf.Max(0f, demandByExertion.Get(tier));

                float baseConsume = attributes != null
                    ? attributes.GetFloat(PlayerAttributeId.AirQualityConsumePerSecond, baseQualityConsumePerSecond)
                    : baseQualityConsumePerSecond;

                float airUseMultiplier = attributes != null
                    ? attributes.GetMultiplier(PlayerAttributeId.AirConsumptionMultiplier)
                    : 1f;

                float consume = baseConsume * demandMul * Mathf.Max(0.01f, airUseMultiplier);
                lungGasQuality01 = Mathf.Max(lungQualityMin, lungGasQuality01 - consume * dt);

                // Volume intentionally unchanged while holding breath
            }

            // Safety clamps
            lungGasQuality01 = Mathf.Clamp01(lungGasQuality01);
            airCurrent = Mathf.Clamp(airCurrent, 0f, MaxAir);

            CurrentState = ComputeState(lungGasQuality01);
        }

        private bool ApplyDivingBellAmbientContext()
        {
            if (!useDivingBellAmbientContext)
            {
                ClearDivingBellAmbientQualityOverride();
                return false;
            }

            ResolveDivingBellContextRefs();

            if (_bellOccupantState == null ||
                !_bellOccupantState.IsInsideBell ||
                _bellOccupantState.CurrentBell == null)
            {
                ClearDivingBellAmbientQualityOverride();
                return false;
            }

            DivingBellOccupancy bell =
                _bellOccupantState.CurrentBell;

            DivingBellAirVolume bellAir =
                bell.GetComponent<DivingBellAirVolume>() ??
                bell.GetComponentInChildren<DivingBellAirVolume>(true);

            if (bellAir == null)
            {
                ClearDivingBellAmbientQualityOverride();
                return false;
            }

            Vector2 headWorld =
                _submersionState != null
                    ? _submersionState.HeadWorldPosition
                    : (Vector2)_bellOccupantState.transform.position;

            if (!bellAir.TryResolveBreathingEnvironment(
                    _bellOccupantState.gameObject,
                    headWorld,
                    out bool headUnderwater,
                    out float ambientQuality01))
            {
                ClearDivingBellAmbientQualityOverride();
                return false;
            }

            IsUnderwater =
                headUnderwater;

            // Bell air quality only applies while the player's head is actually in
            // the air pocket. If submerged, tank/source quality remains the ordinary
            // clean-air value used by the existing source model.
            oxygenQuality01 =
                headUnderwater
                    ? 1f
                    : Mathf.Clamp01(ambientQuality01);

            _divingBellAmbientOverrideActive =
                true;

            return true;
        }

        private void ResolveDivingBellContextRefs()
        {
            if (_bellOccupantState == null)
            {
                _bellOccupantState =
                    GetComponentInParent<PlayerBellOccupantState>() ??
                    GetComponentInChildren<PlayerBellOccupantState>(true);
            }

            if (_submersionState == null)
            {
                _submersionState =
                    GetComponentInParent<PlayerSubmersionState>() ??
                    GetComponentInChildren<PlayerSubmersionState>(true);
            }
        }

        private void ClearDivingBellAmbientQualityOverride()
        {
            if (!_divingBellAmbientOverrideActive)
                return;

            _divingBellAmbientOverrideActive =
                false;

            // Bell air is the only ambient-quality override in this pass. Return
            // to the existing clean ambient/source default when that context ends.
            oxygenQuality01 =
                1f;
        }

        private void RecomputeMaxAir()
        {
            float max = attributes != null
                ? attributes.GetFloat(PlayerAttributeId.MaxAir, baseMaxAir)
                : baseMaxAir;

            for (int i = 0; i < _sources.Count; i++)
                max += Mathf.Max(0f, _sources[i].MaxAirBonus);

            MaxAir = Mathf.Max(0.1f, max);
            if (airCurrent > MaxAir) airCurrent = MaxAir;
        }

        private AirState ComputeState(float q01)
        {
            if (q01 <= 0.0001f) return AirState.CriticalQuality;
            if (q01 < criticalThreshold) return AirState.BadQuality;
            if (q01 < lowThreshold) return AirState.LowQuality;
            if (q01 < 0.90f) return AirState.NormalQuality;
            return AirState.HighQuality;
        }

        private static float MoveTowardExp(float current, float target, float ratePerSecond, float dt)
        {
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, ratePerSecond) * dt);
            return Mathf.Lerp(current, target, t);
        }

        public void ResetState()
        {
            lungGasQuality01 = 1f;
            RecomputeMaxAir();
            airCurrent = MaxAir;
            _timeSinceSurfaced = 999f;
            CanOxygenate = true;
            oxygenQuality01 = 1f;
            _divingBellAmbientOverrideActive = false;
            CurrentState = AirState.HighQuality;
        }
    }
}