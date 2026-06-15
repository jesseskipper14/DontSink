using Survival.Buffs;
using System.Collections.Generic;
using UnityEngine;

namespace Survival.Attributes
{
    [DisallowMultipleComponent]
    public sealed class PlayerAttributeState : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerAttributeProfile profile;
        [SerializeField] private PlayerBuffSystem buffSystem;

        public PlayerAttributeProfile Profile => profile;
        public PlayerBuffSystem BuffSystem => buffSystem;

        private void Reset()
        {
            if (buffSystem == null)
                buffSystem = GetComponent<PlayerBuffSystem>();
        }

        private void Awake()
        {
            if (buffSystem == null)
                buffSystem =
                    GetComponent<PlayerBuffSystem>() ??
                    GetComponentInChildren<PlayerBuffSystem>(true);
        }

        public float GetFloat(PlayerAttributeId attribute, float fallback)
        {
            float baseValue = profile != null
                ? profile.GetBaseValue(attribute, fallback)
                : fallback;

            return Evaluate(attribute, baseValue);
        }

        public float GetMultiplier(PlayerAttributeId attribute)
        {
            return GetFloat(attribute, 1f);
        }

        private float Evaluate(PlayerAttributeId attribute, float baseValue)
        {
            if (buffSystem == null || buffSystem.Current == null)
                return baseValue;

            float add = 0f;
            float multiply = 1f;

            bool hasOverride = false;
            float overrideValue = baseValue;

            IReadOnlyList<PlayerBuffInstance> buffs = buffSystem.Current;

            for (int i = 0; i < buffs.Count; i++)
            {
                PlayerBuffInstance buff = buffs[i];
                PlayerBuffDefinition def = buff.definition;

                if (def == null || def.Modifiers == null)
                    continue;

                float severity = Mathf.Clamp01(buff.severity01);

                IReadOnlyList<PlayerAttributeModifier> modifiers = def.Modifiers;
                for (int m = 0; m < modifiers.Count; m++)
                {
                    PlayerAttributeModifier modifier = modifiers[m];

                    if (modifier.attribute != attribute)
                        continue;

                    switch (modifier.mode)
                    {
                        case PlayerAttributeModifierMode.Add:
                            add += modifier.value * severity;
                            break;

                        case PlayerAttributeModifierMode.Multiply:
                            multiply *= Mathf.Lerp(1f, modifier.value, severity);
                            break;

                        case PlayerAttributeModifierMode.Override:
                            hasOverride = true;
                            overrideValue = Mathf.Lerp(baseValue, modifier.value, severity);
                            break;
                    }
                }
            }

            if (hasOverride)
                return overrideValue;

            return (baseValue + add) * multiply;
        }
    }
}