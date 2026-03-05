using UnityEngine;

namespace Survival.Vitals
{
    [System.Serializable]
    public struct ExertionTierMultipliers
    {
        [Min(0f)] public float resting;
        [Min(0f)] public float calm;
        [Min(0f)] public float active;
        [Min(0f)] public float winded;
        [Min(0f)] public float exerted;
        [Min(0f)] public float redlining;

        public float Get(PlayerExertionEnergyState.ExertionState s)
        {
            return s switch
            {
                PlayerExertionEnergyState.ExertionState.Resting => resting,
                PlayerExertionEnergyState.ExertionState.Calm => calm,
                PlayerExertionEnergyState.ExertionState.Active => active,
                PlayerExertionEnergyState.ExertionState.Winded => winded,
                PlayerExertionEnergyState.ExertionState.Exerted => exerted,
                PlayerExertionEnergyState.ExertionState.Redlining => redlining,
                _ => 1f
            };
        }
    }
}