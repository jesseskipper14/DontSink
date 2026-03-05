using System;
using UnityEngine;

namespace Survival.Afflictions
{
    [CreateAssetMenu(menuName = "Survival/Affliction Definition")]
    public sealed class AfflictionDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stableId;                 // e.g. "aff.low_oxygen"
        public string displayName;              // e.g. "Low Oxygen"
        [TextArea] public string description;   // short, player-facing
        public Sprite icon;

        [Header("Severity Tiers (0..1)")]
        public Tier[] tiers = new Tier[]
        {
            new Tier { minSeverity = 0.10f, label = "Mild" },
            new Tier { minSeverity = 0.35f, label = "Moderate" },
            new Tier { minSeverity = 0.70f, label = "Severe" },
        };

        [Serializable]
        public struct Tier
        {
            [Range(0f, 1f)] public float minSeverity;
            public string label;
        }

        public string GetTierLabel(float severity01)
        {
            string label = string.Empty;
            float best = -1f;

            for (int i = 0; i < tiers.Length; i++)
            {
                if (severity01 >= tiers[i].minSeverity && tiers[i].minSeverity > best)
                {
                    best = tiers[i].minSeverity;
                    label = tiers[i].label;
                }
            }

            return label;
        }
    }
}