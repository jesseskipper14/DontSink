using System.Collections.Generic;
using UnityEngine;
using Survival.Vitals;

namespace Survival.Afflictions
{
    [DisallowMultipleComponent]
    public sealed class SpO2AfflictionProducer : MonoBehaviour, IAfflictionProducer
    {
        [SerializeField] private PlayerOxygenationState oxygenation;
        [SerializeField] private MonoBehaviour airRead; // IAirOxygenationRead (PlayerAirState)

        private IAirOxygenationRead Air => airRead as IAirOxygenationRead;

        [Header("IDs")]
        [SerializeField] private string lowOxygenId = "aff.low_oxygen";
        [SerializeField] private string criticalOxygenId = "aff.critical_oxygen";
        [SerializeField] private string unconsciousId = "aff.unconscious";

        [Header("Unconscious Rule")]
        [SerializeField] private float criticalSecondsToUnconscious = 12f;

        [Tooltip("How quickly critical-hypoxia debt recovers while oxygenating (surface/tank).")]
        [Min(0f)][SerializeField] private float debtRecoveryRateWhileOxygenating = 3.0f;

        [Tooltip("Optional extra recovery when SpO2 is above low threshold.")]
        [Min(0f)][SerializeField] private float debtRecoveryRateWhenNotLow = 6.0f;

        private float _criticalDebtSeconds;

        public string ProducerId => "core.aff.spo2";

        private void Awake()
        {
            if (oxygenation == null) oxygenation = GetComponentInChildren<PlayerOxygenationState>();
            if (airRead == null) airRead = GetComponentInChildren<PlayerAirState>(); // or any IAirOxygenationRead
        }

        public void Produce(List<AfflictionInstance> outList, float dt)
        {
            if (oxygenation == null) return;

            float spo2 = oxygenation.SpO201;
            float low = oxygenation.lowThresh;
            float crit = oxygenation.criticalThresh;

            // ----------------------------
            // Low oxygen (severity ramp)
            // ----------------------------
            if (spo2 < low)
            {
                float sev = 1f - Mathf.Clamp01(Mathf.InverseLerp(crit, low, spo2));
                outList.Add(new AfflictionInstance { stableId = lowOxygenId, severity01 = sev, stacks = 0, secondsLeft = -1f });
            }

            // ----------------------------
            // Critical oxygen (separate affliction if you want)
            // ----------------------------
            if (spo2 < crit)
            {
                float sevCrit = 1f - Mathf.Clamp01(Mathf.InverseLerp(0f, crit, spo2));
                outList.Add(new AfflictionInstance { stableId = criticalOxygenId, severity01 = sevCrit, stacks = 0, secondsLeft = -1f });
            }

            // ----------------------------
            // Unconsciousness debt model
            // ----------------------------
            bool oxygenating = (Air != null) && Air.CanOxygenate;

            if (spo2 < crit && !oxygenating)
            {
                _criticalDebtSeconds += dt; // you are still drowning / not recovering
            }
            else
            {
                // Recover debt if you're oxygenating OR at least not critically hypoxic.
                float recoverRate = 1f;

                if (oxygenating) recoverRate = debtRecoveryRateWhileOxygenating;
                if (spo2 >= low) recoverRate = Mathf.Max(recoverRate, debtRecoveryRateWhenNotLow);

                _criticalDebtSeconds = Mathf.Max(0f, _criticalDebtSeconds - recoverRate * dt);
            }

            if (_criticalDebtSeconds >= criticalSecondsToUnconscious)
            {
                outList.Add(new AfflictionInstance { stableId = unconsciousId, severity01 = 1f, stacks = 0, secondsLeft = -1f });
            }
        }
    }
}