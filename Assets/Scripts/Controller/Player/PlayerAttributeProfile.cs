using UnityEngine;

namespace Survival.Attributes
{
    [CreateAssetMenu(
        fileName = "PlayerAttributeProfile",
        menuName = "Survival/Player Attribute Profile")]
    public sealed class PlayerAttributeProfile : ScriptableObject
    {
        [SerializeField]
        private PlayerAttributeBaseValue[] baseValues =
        {
            new() { attribute = PlayerAttributeId.WalkMaxSpeed, value = 4.5f },
            new() { attribute = PlayerAttributeId.WalkMoveForce, value = 40f },
            new() { attribute = PlayerAttributeId.RunSpeedMultiplier, value = 1.5f },
            new() { attribute = PlayerAttributeId.RunForceMultiplier, value = 1.35f },
            new() { attribute = PlayerAttributeId.JumpImpulse, value = 6.5f },

            new() { attribute = PlayerAttributeId.SwimMaxSpeedX, value = 2.2f },
            new() { attribute = PlayerAttributeId.SwimAccelX, value = 25f },
            new() { attribute = PlayerAttributeId.SwimUpAccel, value = 18f },
            new() { attribute = PlayerAttributeId.DiveAccel, value = 22f },
            new() { attribute = PlayerAttributeId.SwimMaxSpeedY, value = 3.0f },

            new() { attribute = PlayerAttributeId.MaxAir, value = 100f },
            new() { attribute = PlayerAttributeId.AirQualityConsumePerSecond, value = 0.05f },
            new() { attribute = PlayerAttributeId.AirConsumptionMultiplier, value = 1f },
            new() { attribute = PlayerAttributeId.LungQualityRecoverPerSecond, value = 1.25f },

            new() { attribute = PlayerAttributeId.BuyPriceMultiplier, value = 1f },
            new() { attribute = PlayerAttributeId.SellPriceMultiplier, value = 1f },
            new() { attribute = PlayerAttributeId.MiniGameTimingWindowMultiplier, value = 1f },
            new() { attribute = PlayerAttributeId.MiniGameProgressMultiplier, value = 1f },
            new() { attribute = PlayerAttributeId.PilotingControlMultiplier, value = 1f },

            new() { attribute = PlayerAttributeId.ExertionEnergyMax, value = 100f },

            // Exertion
            new() { attribute = PlayerAttributeId.ExertionRestCeiling, value = 0.08f },
            new() { attribute = PlayerAttributeId.ExertionWalkCeiling, value = 0.45f },
            new() { attribute = PlayerAttributeId.ExertionSprintCeiling, value = 0.98f },
            new() { attribute = PlayerAttributeId.ExertionSwimCeiling, value = 0.75f },
            new() { attribute = PlayerAttributeId.ExertionSprintSwimCeiling, value = 1.00f },
            new() { attribute = PlayerAttributeId.ExertionDiveCeilingBonus, value = 0.08f },

            new() { attribute = PlayerAttributeId.ExertionRestApproachRate, value = 2.0f },
            new() { attribute = PlayerAttributeId.ExertionActivityApproachRate, value = 0.8f },
            new() { attribute = PlayerAttributeId.ExertionSprintApproachRate, value = 1.2f },
            new() { attribute = PlayerAttributeId.ExertionSwimApproachRate, value = 1.0f },
            new() { attribute = PlayerAttributeId.ExertionSprintSwimApproachRate, value = 1.6f },
            new() { attribute = PlayerAttributeId.ExertionTreadApproachRate, value = 1.0f },

            new() { attribute = PlayerAttributeId.ExertionDrainThreshold, value = 0.70f },
            new() { attribute = PlayerAttributeId.ExertionBaseDrainPerSecond, value = 4f },
            new() { attribute = PlayerAttributeId.ExertionDrainPower, value = 2.0f },
            new() { attribute = PlayerAttributeId.ExertionRegenPerSecond, value = 3f },
            new() { attribute = PlayerAttributeId.ExertionRegenThreshold, value = 0.40f },
            new() { attribute = PlayerAttributeId.ExertionRestingRegenBonus, value = 2f },
            new() { attribute = PlayerAttributeId.ExertionLandRegenBonus, value = 1f },

            new() { attribute = PlayerAttributeId.ExertionLowEnergyThreshold, value = 0.20f },
            new() { attribute = PlayerAttributeId.ExertionAuthorityAtLowThreshold, value = 0.65f },
            new() { attribute = PlayerAttributeId.ExertionAuthorityAtZero, value = 0.30f },
        };

        public float GetBaseValue(PlayerAttributeId attribute, float fallback)
        {
            if (baseValues == null)
                return fallback;

            for (int i = 0; i < baseValues.Length; i++)
            {
                if (baseValues[i].attribute == attribute)
                    return baseValues[i].value;
            }

            return fallback;
        }
    }
}