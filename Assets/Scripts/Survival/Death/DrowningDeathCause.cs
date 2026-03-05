using UnityEngine;
using Survival.Afflictions;
using Survival.Vitals;

namespace Survival.Death
{
    [DisallowMultipleComponent]
    public sealed class DrowningDeathCause : MonoBehaviour, IDeathCause
    {
        [Header("Refs")]
        [SerializeField] private PlayerOxygenationState oxygenation;
        [SerializeField] private MonoBehaviour afflictionReadBehaviour; // IAfflictionRead

        private IAfflictionRead Aff => afflictionReadBehaviour as IAfflictionRead;

        [Header("Rule")]
        [SerializeField] private StableId unconsciousId = "aff.unconscious";
        [Min(0.1f)] public float unconsciousCriticalSecondsToDie = 120f; // 2 minutes
        [Range(0f, 1f)] public float criticalSpo2Threshold = 0.70f; // default aligns with oxygenation.criticalThresh if you want.

        private float _timer;

        public string CauseId => "cause.drown";

        private void Awake()
        {
            if (!oxygenation) oxygenation = GetComponentInChildren<PlayerOxygenationState>();
            if (afflictionReadBehaviour == null) afflictionReadBehaviour = GetComponentInChildren<AfflictionSystem>();
            if (oxygenation != null) criticalSpo2Threshold = oxygenation.criticalThresh; // optional: auto-align
        }

        public bool Evaluate(float dt, out DeathInfo info)
        {
            info = default;

            if (oxygenation == null || Aff == null)
                return false;

            bool unconscious = Aff.Has(unconsciousId);
            bool critical = oxygenation.SpO201 < criticalSpo2Threshold;

            if (unconscious && critical)
                _timer += dt;
            else
                _timer = Mathf.Max(0f, _timer - dt * 2f); // recover faster than you die (tunable)

            if (_timer >= unconsciousCriticalSecondsToDie)
            {
                info = new DeathInfo(
                    causeId: CauseId,
                    message: "Drowned",
                    timeOfDeath: Time.time
                );
                return true;
            }

            return false;
        }
    }
}