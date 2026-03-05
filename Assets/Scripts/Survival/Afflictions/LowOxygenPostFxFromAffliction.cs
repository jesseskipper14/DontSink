using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Survival.Afflictions
{
    [DisallowMultipleComponent]
    public sealed class LowOxygenPostFxFromAfflictionCurved : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MonoBehaviour afflictionReadBehaviour; // IAfflictionRead
        [SerializeField] private Volume volume;

        [Header("Affliction ID")]
        [SerializeField] private string lowOxygenId = "aff.low_oxygen";

        [Header("Mapping (Severity -> Effect 0..1)")]
        [Tooltip("X=severity01, Y=saturationStrength01 (0=no change, 1=max desaturation).")]
        public AnimationCurve saturationBySeverity = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 0f),  // ~85% start (if your producer maps 90..70)
            new Keyframe(1f, 1f)      // max at severe
        );

        [Tooltip("X=severity01, Y=vignetteStrength01 (0=off, 1=max vignette).")]
        public AnimationCurve vignetteBySeverity = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.75f, 0f),  // later kick-in
            new Keyframe(1f, 1f)
        );

        [Header("Effect Ranges")]
        [Range(-100f, 0f)] public float saturationAtMax = -85f;
        [Range(0f, 1f)] public float vignetteIntensityAtMax = 0.55f;
        [Range(0f, 1f)] public float vignetteSmoothnessAtMax = 0.9f;

        [Header("Response (No timers, just smoothing)")]
        [Tooltip("How quickly effects ramp IN as severity rises.")]
        [Min(0f)] public float rampInRate = 8f;

        [Tooltip("How quickly effects ramp OUT as severity falls (set lower for slower recovery).")]
        [Min(0f)] public float rampOutRate = 3f;

        private IAfflictionRead Aff => afflictionReadBehaviour as IAfflictionRead;

        private ColorAdjustments _color;
        private Vignette _vignette;

        private float _satApplied01;
        private float _vigApplied01;

        private void Reset()
        {
            volume = FindAnyObjectByType<Volume>();
        }

        private void Awake()
        {
            if (!volume) volume = FindAnyObjectByType<Volume>();
            Bind();
        }

        private void OnEnable()
        {
            Bind();
            ApplyImmediate(0f, 0f);
        }

        private void OnDisable()
        {
            ApplyImmediate(0f, 0f);
        }

        private void LateUpdate()
        {
            if (Aff == null || volume == null) return;
            if (_color == null || _vignette == null) Bind();

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Input: single source of truth
            float sev = 0f;
            if (Aff.TryGetSeverity01(lowOxygenId, out var s))
                sev = Mathf.Clamp01(s);

            // Map severity -> desired effect strengths
            float satTarget01 = Evaluate01(saturationBySeverity, sev);
            float vigTarget01 = Evaluate01(vignetteBySeverity, sev);

            // Smooth both directions (recovery feels gradual)
            _satApplied01 = SmoothAsymmetric(_satApplied01, satTarget01, rampInRate, rampOutRate, dt);
            _vigApplied01 = SmoothAsymmetric(_vigApplied01, vigTarget01, rampInRate, rampOutRate, dt);

            Apply(_satApplied01, _vigApplied01);
        }

        private void Bind()
        {
            if (volume == null || volume.profile == null) return;
            volume.profile.TryGet(out _color);
            volume.profile.TryGet(out _vignette);
        }

        private void Apply(float sat01, float vig01)
        {
            if (_color != null)
                _color.saturation.value = Mathf.Lerp(0f, saturationAtMax, sat01);

            if (_vignette != null)
            {
                _vignette.intensity.value = Mathf.Lerp(0f, vignetteIntensityAtMax, vig01);
                _vignette.smoothness.value = Mathf.Lerp(0.2f, vignetteSmoothnessAtMax, vig01);
            }
        }

        private void ApplyImmediate(float sat01, float vig01)
        {
            _satApplied01 = Mathf.Clamp01(sat01);
            _vigApplied01 = Mathf.Clamp01(vig01);
            Apply(_satApplied01, _vigApplied01);
        }

        private static float Evaluate01(AnimationCurve curve, float x01)
        {
            if (curve == null) return x01;
            return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(x01)));
        }

        private static float SmoothAsymmetric(float current, float target, float inRate, float outRate, float dt)
        {
            float rate = (target > current) ? inRate : outRate;
            if (rate <= 0f) return target;

            float t = 1f - Mathf.Exp(-rate * dt);
            return Mathf.Lerp(current, target, t);
        }
    }
}