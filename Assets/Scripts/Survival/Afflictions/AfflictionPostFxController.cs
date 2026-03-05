using Survival.Vitals;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Survival.Afflictions
{
    [DisallowMultipleComponent]
    public sealed class AfflictionPostFxController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MonoBehaviour afflictionReadBehaviour; // IAfflictionRead
        [SerializeField] private Volume volume;

        private IAfflictionRead Aff => afflictionReadBehaviour as IAfflictionRead;

        [Serializable]
        public sealed class Binding
        {
            [Header("Affliction")]
            public StableId afflictionId;

            [Tooltip("If true, multiply ramp by affliction severity01.")]
            public bool scaleBySeverity = true;

            [Range(0.01f, 1f)]
            public float severityToFull = 1f;

            [Header("Ramp")]
            [Min(0.01f)] public float timeToMaxIn = 15f;
            [Min(0.01f)] public float timeToZeroOut = 10f;

            [Header("Channels")]
            public bool affectSaturation = false;
            public bool affectVignette = false;
            public bool affectPostExposure = false;

            [Tooltip("PostExposure at max strength. Negative darkens the whole screen.")]
            [Range(-5f, 0f)] public float postExposureAtMax = -1.2f;

            [Header("Saturation")]
            [Range(-100f, 0f)] public float saturationAtMax = -85f;

            [Header("Vignette")]
            [Range(0f, 1f)] public float vignetteIntensityAtMax = 0.55f;
            [Range(0f, 1f)] public float vignetteSmoothnessAtMax = 0.9f;

            // runtime
            [NonSerialized] public float ramp01;
        }

        [Header("Bindings")]
        public List<Binding> bindings = new();

        [Header("Mixing")]
        [Tooltip("If true, uses max() across bindings per channel. If false, adds then clamps.")]
        public bool useMaxMix = true;

        private ColorAdjustments _color;
        private Vignette _vignette;

        private void Reset()
        {
            volume = FindAnyObjectByType<Volume>();
        }

        private void Awake()
        {
            if (!volume) volume = FindAnyObjectByType<Volume>();
            BindVolume();
        }

        private void OnEnable()
        {
            BindVolume();
            ApplyFinal(0f, 0f, 0f, 0f);
            ResetRamps();
        }

        private void OnDisable()
        {
            ApplyFinal(0f, 0f, 0f, 0f);
            ResetRamps();
        }

        private void LateUpdate()
        {
            if (Aff == null || volume == null) return;
            if (_color == null || _vignette == null) BindVolume();

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Accumulate desired channel targets
            float satStrength01 = 0f; // 0..1
            float satAtMax = 0f;      // negative value blended later

            float expStrength01 = 0f;  // 0..1
            float expAtMax = 0f;       // negative

            float vigStrength01 = 0f; // 0..1
            float vigIntensityMax = 0f;
            float vigSmoothnessMax = 0f;

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null) continue;
                if (string.IsNullOrEmpty(b.afflictionId.value)) { b.ramp01 = 0f; continue; }

                bool active = Aff.Has(b.afflictionId);
                float sev = 0f;
                if (active && b.scaleBySeverity)
                    Aff.TryGetSeverity01(b.afflictionId, out sev);

                // ramp
                float inRate = 1f / Mathf.Max(0.01f, b.timeToMaxIn);
                float outRate = 1f / Mathf.Max(0.01f, b.timeToZeroOut);
                float target = active ? 1f : 0f;

                b.ramp01 = Mathf.MoveTowards(b.ramp01, target, (active ? inRate : outRate) * dt);

                float sevMul = 1f;
                if (b.scaleBySeverity)
                    sevMul = Mathf.Clamp01(sev / Mathf.Max(0.01f, b.severityToFull));

                float strength = Mathf.Clamp01(b.ramp01 * sevMul);

                if (b.affectPostExposure)
                {
                    Mix(ref expStrength01, strength, useMaxMix);
                    expAtMax = Mathf.Min(expAtMax, b.postExposureAtMax); // more negative = darker
                }

                // channel contributions
                if (b.affectSaturation)
                {
                    Mix(ref satStrength01, strength, useMaxMix);
                    // For ranges, take the “strongest max” when using max mix; for additive, take max too.
                    satAtMax = Mathf.Min(satAtMax, b.saturationAtMax); // more negative = stronger desat
                }

                if (b.affectVignette)
                {
                    Mix(ref vigStrength01, strength, useMaxMix);
                    vigIntensityMax = Mathf.Max(vigIntensityMax, b.vignetteIntensityAtMax);
                    vigSmoothnessMax = Mathf.Max(vigSmoothnessMax, b.vignetteSmoothnessAtMax);
                }
            }

            ApplyFinal(satStrength01, satAtMax, vigStrength01, vigIntensityMax, vigSmoothnessMax, expStrength01, expAtMax);
        }

        private void BindVolume()
        {
            if (volume == null || volume.profile == null) return;
            volume.profile.TryGet(out _color);
            volume.profile.TryGet(out _vignette);
        }

        private void ResetRamps()
        {
            for (int i = 0; i < bindings.Count; i++)
                if (bindings[i] != null) bindings[i].ramp01 = 0f;
        }

        private static void Mix(ref float accum, float add, bool useMax)
        {
            if (useMax) accum = Mathf.Max(accum, add);
            else accum = Mathf.Clamp01(accum + add);
        }

        private void ApplyFinal(float satStrength01, float satMax, float vigStrength01, float vigIntensityMax, float vigSmoothnessMax, float expStrength01, float expAtMax)
        {
            // Saturation
            if (_color != null)
                _color.saturation.value = Mathf.Lerp(0f, satMax, Mathf.Clamp01(satStrength01));

            // Vignette
            if (_vignette != null)
            {
                _vignette.intensity.value = Mathf.Lerp(0f, vigIntensityMax, Mathf.Clamp01(vigStrength01));
                _vignette.smoothness.value = Mathf.Lerp(0.2f, vigSmoothnessMax, Mathf.Clamp01(vigStrength01));
            }

            if (_color != null)
                _color.postExposure.value = Mathf.Lerp(0f, expAtMax, Mathf.Clamp01(expStrength01));
        }

        // Overload used in OnEnable/OnDisable
        private void ApplyFinal(float satStrength01, float satMax, float vigStrength01, float vigIntensityMax)
        {
            ApplyFinal(satStrength01, satMax, vigStrength01, vigIntensityMax, 0.9f, 0f, 0f);
        }
    }
}