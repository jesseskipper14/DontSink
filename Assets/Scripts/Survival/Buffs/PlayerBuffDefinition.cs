using Survival.Attributes;
using System.Collections.Generic;
using UnityEngine;

namespace Survival.Buffs
{
    [CreateAssetMenu(
        fileName = "BuffDefinition",
        menuName = "Survival/Buff Definition")]
    public sealed class PlayerBuffDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        [Header("HUD Presentation")]
        [SerializeField] private PlayerBuffPolarity polarity = PlayerBuffPolarity.Positive;

        public PlayerBuffPolarity Polarity => polarity;

        [Header("Attribute Modifiers")]
        [SerializeField] private PlayerAttributeModifier[] modifiers;

        public string StableId => stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public IReadOnlyList<PlayerAttributeModifier> Modifiers => modifiers;
    }

    public enum PlayerBuffPolarity
    {
        Neutral = 0,
        Positive = 1,
        Negative = 2
    }
}