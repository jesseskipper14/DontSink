namespace Survival.Attributes
{
    public enum PlayerAttributeId
    {
        // Physical movement
        WalkMaxSpeed = 0,
        WalkMoveForce = 1,
        RunSpeedMultiplier = 2,
        RunForceMultiplier = 3,
        JumpImpulse = 4,

        // Swimming
        SwimMaxSpeedX = 20,
        SwimAccelX = 21,
        SwimUpAccel = 22,
        DiveAccel = 23,
        SwimMaxSpeedY = 24,

        // Air / oxygen
        MaxAir = 40,
        AirQualityConsumePerSecond = 41,
        AirConsumptionMultiplier = 42,
        LungQualityRecoverPerSecond = 43,

        // Exertion / energy
        ExertionEnergyMax = 60,

        ExertionRestCeiling = 61,
        ExertionWalkCeiling = 62,
        ExertionSprintCeiling = 63,
        ExertionSwimCeiling = 64,
        ExertionSprintSwimCeiling = 65,
        ExertionDiveCeilingBonus = 66,

        ExertionRestApproachRate = 70,
        ExertionActivityApproachRate = 71,
        ExertionSprintApproachRate = 72,
        ExertionSwimApproachRate = 73,
        ExertionSprintSwimApproachRate = 74,
        ExertionTreadApproachRate = 75,

        ExertionDrainThreshold = 80,
        ExertionBaseDrainPerSecond = 81,
        ExertionDrainPower = 82,
        ExertionRegenPerSecond = 83,
        ExertionRegenThreshold = 84,
        ExertionRestingRegenBonus = 85,
        ExertionLandRegenBonus = 86,

        ExertionLowEnergyThreshold = 90,
        ExertionAuthorityAtLowThreshold = 91,
        ExertionAuthorityAtZero = 92,

        // Economy / social
        BuyPriceMultiplier = 100,
        SellPriceMultiplier = 101,
        TradeOfferQualityMultiplier = 102,

        // Balance / upright control
        CharacterUprightTargetAngleOffsetDeg = 110,

        CharacterUprightStrength = 111,
        CharacterUprightDamping = 112,
        CharacterUprightMaxTorque = 113,
        CharacterUprightDeadZoneDeg = 114,

        CharacterMovementLeanMaxDeg = 115,
        CharacterLeanSmoothSpeed = 116,
        CharacterSprintLeanMultiplier = 117,
        CharacterLeanMinHorizontalSpeed = 118,
        CharacterLeanFullHorizontalSpeed = 119,

        CharacterWadingTorqueMultiplier = 120,
        CharacterSwimmingTorqueMultiplier = 121,
        CharacterUprightHeldMultiplier = 122,

        // Mini-games
        MiniGameTimingWindowMultiplier = 140,
        MiniGameProgressMultiplier = 141,
        MiniGameFailurePenaltyMultiplier = 142,

        // Boat handling
        PilotingControlMultiplier = 180,
        HelmResponsivenessMultiplier = 181,
        SteeringAuthorityMultiplier = 182,
        EngineEfficiencyMultiplier = 183,
        PumpEfficiencyMultiplier = 184
    }
}