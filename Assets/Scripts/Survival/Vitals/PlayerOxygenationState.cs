using UnityEngine;

namespace Survival.Vitals
{
    [DisallowMultipleComponent]
    public sealed class PlayerOxygenationState : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MonoBehaviour airRead;  // IAirOxygenationRead
        [SerializeField] private MonoBehaviour bodyRead; // IOxygenationBodyRead (optional; can be null)

        [Header("SpO2 Dynamics")]
        [Min(0f)] public float riseRate = 0.8f;
        [Min(0f)] public float fallRate = 1.2f;
        [Range(0f, 1f)] public float minTargetSpO2 = 0.40f;

        public bool useFixedUpdate = true;

        [Header("Exposure Thresholds")]
        [Range(0f, 1f)] public float lowThresh = 0.90f;
        [Range(0f, 1f)] public float criticalThresh = 0.70f;

        [Header("Exposure Recovery")]
        [Min(0f)] public float exposureRecoveryRate = 0.35f;

        [Header("Oxygen Saturation Curve")]
        [Tooltip("Maps effective oxygen delivery (0..1) to SpO2 (0..1).")]
        public AnimationCurve saturationCurve = new AnimationCurve(
            new Keyframe(0f, 0.40f),
            new Keyframe(0.30f, 0.55f),
            new Keyframe(0.50f, 0.85f),
            new Keyframe(0.75f, 0.97f),
            new Keyframe(1f, 1f)
        );

        public float SpO201 { get; private set; } = 1f;
        public int SpO2Percent => Mathf.RoundToInt(SpO201 * 100f);

        public float SecondsBelowLow { get; private set; }
        public float SecondsBelowCritical { get; private set; }

        public bool IsLow => SpO201 < lowThresh;
        public bool IsCritical => SpO201 < criticalThresh;
        // Add near your public properties:

        private IAirOxygenationRead Air => airRead as IAirOxygenationRead;
        private IOxygenationBodyRead Body => bodyRead as IOxygenationBodyRead;

        private void Reset()
        {
            // Best-effort auto-find (still recommend explicit assignment)
            airRead = GetComponentInChildren<MonoBehaviour>();
            bodyRead = GetComponentInChildren<MonoBehaviour>();
        }

        private void FixedUpdate()
        {
            if (!useFixedUpdate) return;
            Tick(Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (useFixedUpdate) return;
            Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (Air == null) return;

            // Defaults for v1 if no body component exists yet.
            float lungEff = 1f;
            float perf = 1f;
            float bloodQ = 1f;
            float demand = 1f;

            if (Body != null)
            {
                lungEff = Mathf.Clamp01(Body.LungEffectiveness01);
                perf = Mathf.Clamp01(Body.Perfusion01);
                bloodQ = Mathf.Clamp01(Body.BloodQuality01);
                demand = Mathf.Max(0.1f, Body.DemandMul);
            }

            float air01 = Mathf.Clamp01(Air.Air01);

            // Choose which "quality" applies:
            // - If oxygenating (surface/tank), environmental/source quality dominates.
            // - If holding breath, lung gas quality dominates.
            float envQuality = Mathf.Clamp01(Air.OxygenQuality01);
            float lungQuality = Mathf.Clamp01(Air.LungGasQuality01);
            float quality = Air.CanOxygenate ? envQuality : lungQuality;

            // Volume is only a gate (if you later add aspiration/pressure reducing volume).
            float supply = (air01 > 0.0001f) ? quality : 0f;

            // Body modifiers
            supply *= lungEff;

            float delivery = supply * perf * bloodQ / demand;
            delivery = Mathf.Clamp01(delivery);

            float target = saturationCurve != null
                ? Mathf.Clamp01(saturationCurve.Evaluate(delivery))
                : Mathf.Lerp(minTargetSpO2, 1f, delivery);

            float rate = (target >= SpO201) ? riseRate : fallRate;
            SpO201 = MoveTowardExp(SpO201, target, rate, dt);

            UpdateExposure(dt);
        }

        private void UpdateExposure(float dt)
        {
            if (SpO201 < lowThresh) SecondsBelowLow += dt;
            else SecondsBelowLow = Mathf.Max(0f, SecondsBelowLow - exposureRecoveryRate * dt);

            if (SpO201 < criticalThresh) SecondsBelowCritical += dt;
            else SecondsBelowCritical = Mathf.Max(0f, SecondsBelowCritical - exposureRecoveryRate * dt);
        }

        private static float MoveTowardExp(float current, float target, float ratePerSecond, float dt)
        {
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, ratePerSecond) * dt);
            return Mathf.Lerp(current, target, t);
        }

        public void ResetState(float spo2 = 1f)
        {
            SpO201 = Mathf.Clamp01(spo2);
            SecondsBelowLow = 0f;
            SecondsBelowCritical = 0f;
        }
    }
}