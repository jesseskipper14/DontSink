using System;
using UnityEngine;

namespace Survival.Attributes
{
    public enum PlayerAttributeModifierMode
    {
        Add = 0,
        Multiply = 1,
        Override = 2
    }

    [Serializable]
    public struct PlayerAttributeModifier
    {
        public PlayerAttributeId attribute;
        public PlayerAttributeModifierMode mode;

        [Tooltip("Add: flat amount. Multiply: 1=no change, 1.2=+20%, 0.9=-10%. Override: final value.")]
        public float value;
    }

    [Serializable]
    public struct PlayerAttributeBaseValue
    {
        public PlayerAttributeId attribute;
        public float value;
    }
}