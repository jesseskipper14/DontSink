using System.Collections.Generic;
using UnityEngine;

namespace Survival.Buffs
{
    /// <summary>
    /// Emits exactly one visible PlayerBuff based on PlayerExertionEnergyState.CurrentState.
    ///
    /// Source of truth stays in PlayerExertionEnergyState:
    /// exertion01 -> CurrentState -> this producer -> PlayerBuffSystem/HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExertionStatePlayerBuffProducer : MonoBehaviour, IPlayerBuffProducer
    {
        [Header("Refs")]
        [SerializeField] private global::PlayerExertionEnergyState exertion;

        [Header("Producer")]
        [SerializeField] private string producerId = "producer.exertion_state";

        [Tooltip("State buffs should usually be severity 1 because the state itself is active/true.")]
        [SerializeField, Range(0f, 1f)] private float emittedSeverity01 = 1f;

        [Header("State Buffs")]
        [SerializeField] private PlayerBuffDefinition restingBuff;
        [SerializeField] private PlayerBuffDefinition calmBuff;
        [SerializeField] private PlayerBuffDefinition activeBuff;
        [SerializeField] private PlayerBuffDefinition windedBuff;
        [SerializeField] private PlayerBuffDefinition exertedBuff;
        [SerializeField] private PlayerBuffDefinition redliningBuff;

        [Header("Debug")]
        [SerializeField] private bool warnIfMissingBuff = true;

        private bool _warnedMissingExertion;
        private bool _warnedMissingBuff;

        public string ProducerId => string.IsNullOrWhiteSpace(producerId)
            ? "producer.exertion_state"
            : producerId;

        private void Reset()
        {
            if (!exertion)
            {
                exertion =
                    GetComponent<global::PlayerExertionEnergyState>() ??
                    GetComponentInParent<global::PlayerExertionEnergyState>() ??
                    GetComponentInChildren<global::PlayerExertionEnergyState>(true);
            }
        }

        private void Awake()
        {
            if (!exertion)
            {
                exertion =
                    GetComponent<global::PlayerExertionEnergyState>() ??
                    GetComponentInParent<global::PlayerExertionEnergyState>() ??
                    GetComponentInChildren<global::PlayerExertionEnergyState>(true);
            }
        }

        public void Produce(List<PlayerBuffInstance> outList, float dt)
        {
            if (outList == null)
                return;

            if (exertion == null)
            {
                if (!_warnedMissingExertion)
                {
                    Debug.LogWarning(
                        $"{nameof(ExertionStatePlayerBuffProducer)} has no PlayerExertionEnergyState assigned.",
                        this);
                    _warnedMissingExertion = true;
                }

                return;
            }

            PlayerBuffDefinition definition = GetBuffForState(exertion.CurrentState);

            if (definition == null)
            {
                if (warnIfMissingBuff && !_warnedMissingBuff)
                {
                    Debug.LogWarning(
                        $"{nameof(ExertionStatePlayerBuffProducer)} has no PlayerBuffDefinition assigned for exertion state '{exertion.CurrentState}'.",
                        this);
                    _warnedMissingBuff = true;
                }

                return;
            }

            outList.Add(new PlayerBuffInstance
            {
                definition = definition,
                sourceId = ProducerId,
                severity01 = Mathf.Clamp01(emittedSeverity01)
            });
        }

        private PlayerBuffDefinition GetBuffForState(global::PlayerExertionEnergyState.ExertionState state)
        {
            return state switch
            {
                global::PlayerExertionEnergyState.ExertionState.Resting => restingBuff,
                global::PlayerExertionEnergyState.ExertionState.Calm => calmBuff,
                global::PlayerExertionEnergyState.ExertionState.Active => activeBuff,
                global::PlayerExertionEnergyState.ExertionState.Winded => windedBuff,
                global::PlayerExertionEnergyState.ExertionState.Exerted => exertedBuff,
                global::PlayerExertionEnergyState.ExertionState.Redlining => redliningBuff,
                _ => null
            };
        }
    }
}
