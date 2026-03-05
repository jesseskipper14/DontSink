using UnityEngine;
using System.Collections.Generic;

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

        void Awake()
        {
            if (!exertion) exertion = GetComponent<PlayerExertionEnergyState>();

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

            // Track surface time
            if (!IsUnderwater) _timeSinceSurfaced += dt;
            else _timeSinceSurfaced = 0f;

            // Sum flows from sources (to detect underwater oxygenation supply)
            float sourceFlow = 0f;
            for (int i = 0; i < _sources.Count; i++)
                sourceFlow += _sources[i].GetAirFlowPerSecond(this, dt);

            _lastSourceFlowPerSecond = sourceFlow;

            bool ambientOxygenation = !IsUnderwater && _timeSinceSurfaced >= surfaceRegenDelay;
            bool sourceProvides = IsUnderwater && _lastSourceFlowPerSecond > 0.001f;

            CanOxygenate = ambientOxygenation || sourceProvides;

            if (CanOxygenate)
            {
                // Recover lung gas quality toward environment/source quality
                float target = Mathf.Clamp01(OxygenQuality01);
                lungGasQuality01 = MoveTowardExp(lungGasQuality01, target, lungQualityRecoverPerSecond, dt);

                // For now, lung volume stays full when oxygenating
                airCurrent = MaxAir;
            }
            else
            {
                // Holding breath: consume lung gas quality based on exertion-demand
                var tier = exertion != null ? exertion.CurrentState : PlayerExertionEnergyState.ExertionState.Calm;
                float demandMul = Mathf.Max(0f, demandByExertion.Get(tier));

                float consume = baseQualityConsumePerSecond * demandMul;
                lungGasQuality01 = Mathf.Max(lungQualityMin, lungGasQuality01 - consume * dt);

                // Volume intentionally unchanged while holding breath
            }

            // Safety clamps
            lungGasQuality01 = Mathf.Clamp01(lungGasQuality01);
            airCurrent = Mathf.Clamp(airCurrent, 0f, MaxAir);

            CurrentState = ComputeState(lungGasQuality01);
        }

        private void RecomputeMaxAir()
        {
            float max = baseMaxAir;
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
            CurrentState = AirState.HighQuality;
        }
    }
}