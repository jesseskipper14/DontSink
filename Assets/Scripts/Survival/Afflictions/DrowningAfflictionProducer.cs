using System.Collections.Generic;
using Survival.Vitals;
using UnityEngine;

namespace Survival.Afflictions
{
    /// <summary>
    /// Emits a drowning affliction while PlayerDrowningForce says the player is
    /// exhausted in water and under drowning tug pressure.
    ///
    /// PlayerDrowningForce remains the source of truth for the physical tug.
    /// This producer only bridges that state into the AfflictionSystem/HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DrowningAfflictionProducer : MonoBehaviour, IAfflictionProducer
    {
        [Header("Refs")]
        [SerializeField] private global::PlayerDrowningForce drowningForce;

        [Header("Affliction")]
        [SerializeField] private AfflictionDefinition drowningAffliction;

        [Tooltip("Fallback stable ID used only if no AfflictionDefinition is assigned.")]
        [SerializeField] private string fallbackStableId = "affliction.drowning";

        [Header("Producer")]
        [SerializeField] private string producerId = "producer.drowning";

        [Tooltip("Used so the affliction appears immediately when drowning starts, even before escalation has built up.")]
        [SerializeField, Range(0f, 1f)] private float minimumEmittedSeverity01 = 0.10f;

        [Header("Debug")]
        [SerializeField] private bool warnIfMissingRefs = true;

        private bool _warnedMissingForce;
        private bool _warnedMissingStableId;

        public string ProducerId => string.IsNullOrWhiteSpace(producerId)
            ? "producer.drowning"
            : producerId;

        private void Reset()
        {
            AutoAssignRefs();
        }

        private void Awake()
        {
            AutoAssignRefs();
        }

        private void AutoAssignRefs()
        {
            if (!drowningForce)
            {
                drowningForce =
                    GetComponent<global::PlayerDrowningForce>() ??
                    GetComponentInParent<global::PlayerDrowningForce>() ??
                    GetComponentInChildren<global::PlayerDrowningForce>(true);
            }
        }

        public void Produce(List<AfflictionInstance> outList, float dt)
        {
            if (outList == null)
                return;

            if (drowningForce == null)
            {
                if (warnIfMissingRefs && !_warnedMissingForce)
                {
                    Debug.LogWarning(
                        $"{nameof(DrowningAfflictionProducer)} has no PlayerDrowningForce assigned.",
                        this);
                    _warnedMissingForce = true;
                }

                return;
            }

            if (!drowningForce.IsDrowning)
                return;

            string stableId = ResolveStableId();
            if (string.IsNullOrWhiteSpace(stableId))
            {
                if (warnIfMissingRefs && !_warnedMissingStableId)
                {
                    Debug.LogWarning(
                        $"{nameof(DrowningAfflictionProducer)} has no drowning affliction stable ID. Assign an AfflictionDefinition or set a fallback stable ID.",
                        this);
                    _warnedMissingStableId = true;
                }

                return;
            }

            float severity = Mathf.Clamp01(drowningForce.DrowningSeverity01);
            severity = Mathf.Max(severity, Mathf.Clamp01(minimumEmittedSeverity01));

            outList.Add(new AfflictionInstance
            {
                stableId = MakeStableId(stableId),
                severity01 = severity
            });
        }

        private string ResolveStableId()
        {
            if (drowningAffliction != null && !string.IsNullOrWhiteSpace(drowningAffliction.stableId))
                return drowningAffliction.stableId;

            return fallbackStableId;
        }

        private static StableId MakeStableId(string value)
        {
            StableId id = default;
            id.value = value;
            return id;
        }
    }
}
