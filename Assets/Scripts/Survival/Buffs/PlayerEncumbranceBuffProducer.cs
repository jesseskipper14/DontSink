using System.Collections.Generic;
using UnityEngine;

namespace Survival.Buffs
{
    /// <summary>
    /// Converts PlayerLoadState overload into one scalable Encumbered buff.
    ///
    /// Capacity itself remains an ordinary player attribute and can be modified
    /// by equipment buffs. This producer only reacts to the final resolved
    /// capacity and MUST NOT change EncumbranceCapacity itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerEncumbranceBuffProducer :
        MonoBehaviour,
        IPlayerBuffProducer
    {
        [Header("Refs")]
        [SerializeField] private global::PlayerLoadState loadState;
        [SerializeField] private PlayerBuffDefinition encumberedBuff;

        [Header("Encumbrance Curve")]
        [Tooltip(
            "Load at or below normal capacity has zero encumbrance severity. " +
            "Example: capacity 30 means carried mass <= 30 is completely normal.")]
        [SerializeField, Min(1f)]
        private float encumbranceStartsAtCapacityMultiplier = 1f;

        [Tooltip(
            "Carried-mass multiple that produces severity 1. " +
            "Default 2 means: capacity 30 -> full encumbrance at mass 60. " +
            "Changing base capacity automatically moves the full-severity point.")]
        [SerializeField, Min(1.01f)]
        private float fullSeverityAtCapacityMultiplier = 2f;

        [Tooltip(
            "Shapes severity between the start and full-severity thresholds. " +
            "X = normalized overload progress (0..1). Y = buff severity (0..1). " +
            "Linear means 150% capacity is half severity when start=1x/full=2x.")]
        [SerializeField]
        private AnimationCurve severityCurve =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Producer")]
        [SerializeField]
        private string producerId = "producer.encumbrance";

        [Header("Runtime Debug (overwritten while playing)")]
        [SerializeField] private float currentCarriedMass;
        [SerializeField] private float currentCapacity;
        [SerializeField] private float currentCapacityMultiple;
        [SerializeField, Range(0f, 1f)] private float currentSeverity01;

        public string ProducerId =>
            string.IsNullOrWhiteSpace(producerId)
                ? "producer.encumbrance"
                : producerId;

        public float CurrentSeverity01 => currentSeverity01;

        private void Reset()
        {
            ResolveLoadState();
        }

        private void Awake()
        {
            ResolveLoadState();
        }

        public void Produce(
            List<PlayerBuffInstance> outList,
            float dt)
        {
            if (outList == null)
                return;

            ResolveLoadState();

            if (loadState == null)
            {
                ClearDebug();
                return;
            }

            currentCarriedMass =
                Mathf.Max(
                    0f,
                    loadState.CarriedMass);

            currentCapacity =
                Mathf.Max(
                    0f,
                    loadState.EncumbranceCapacity);

            currentSeverity01 =
                EvaluateSeverity(
                    currentCarriedMass,
                    currentCapacity,
                    out float capacityMultiple);

            currentCapacityMultiple =
                capacityMultiple;

            if (currentSeverity01 <= 0f ||
                encumberedBuff == null)
            {
                return;
            }

            outList.Add(
                new PlayerBuffInstance
                {
                    definition = encumberedBuff,
                    sourceId = ProducerId,
                    severity01 = currentSeverity01
                });
        }

        public float EvaluateSeverity(
            float carriedMass,
            float capacity,
            out float capacityMultiple)
        {
            carriedMass =
                Mathf.Max(
                    0f,
                    carriedMass);

            capacity =
                Mathf.Max(
                    0f,
                    capacity);

            if (capacity <= 0.0001f)
            {
                capacityMultiple =
                    carriedMass > 0f
                        ? float.PositiveInfinity
                        : 0f;

                return carriedMass > 0f
                    ? 1f
                    : 0f;
            }

            capacityMultiple =
                carriedMass / capacity;

            float start =
                Mathf.Max(
                    1f,
                    encumbranceStartsAtCapacityMultiplier);

            float full =
                Mathf.Max(
                    start + 0.01f,
                    fullSeverityAtCapacityMultiplier);

            if (capacityMultiple <= start)
                return 0f;

            float normalized =
                Mathf.InverseLerp(
                    start,
                    full,
                    capacityMultiple);

            float severity =
                severityCurve != null
                    ? severityCurve.Evaluate(normalized)
                    : normalized;

            return Mathf.Clamp01(severity);
        }

        private void ResolveLoadState()
        {
            if (loadState != null)
                return;

            loadState =
                GetComponent<global::PlayerLoadState>() ??
                GetComponentInParent<global::PlayerLoadState>() ??
                GetComponentInChildren<global::PlayerLoadState>(true);
        }

        private void ClearDebug()
        {
            currentCarriedMass = 0f;
            currentCapacity = 0f;
            currentCapacityMultiple = 0f;
            currentSeverity01 = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            encumbranceStartsAtCapacityMultiplier =
                Mathf.Max(
                    1f,
                    encumbranceStartsAtCapacityMultiplier);

            fullSeverityAtCapacityMultiplier =
                Mathf.Max(
                    encumbranceStartsAtCapacityMultiplier + 0.01f,
                    fullSeverityAtCapacityMultiplier);

            if (severityCurve == null ||
                severityCurve.length == 0)
            {
                severityCurve =
                    AnimationCurve.Linear(
                        0f,
                        0f,
                        1f,
                        1f);
            }
        }
#endif
    }
}
