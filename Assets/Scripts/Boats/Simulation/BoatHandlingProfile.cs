using UnityEngine;

[System.Serializable]
public struct BoatHandlingProfile
{
    [SerializeField] private float maxTurnAngle;
    [SerializeField] private float turnEfficiency;
    [SerializeField] private float lateralDisturbanceResistance;
    [SerializeField] private float yawDisturbanceResistance;

    public float MaxTurnAngle => Mathf.Max(0f, maxTurnAngle);
    public float TurnEfficiency => Mathf.Max(0f, turnEfficiency);

    /// <summary>
    /// Fraction of simulation-owned lateral environmental disturbance resisted.
    /// 0 = none, 1 = fully resisted.
    /// </summary>
    public float LateralDisturbanceResistance =>
        Mathf.Clamp01(lateralDisturbanceResistance);

    /// <summary>
    /// Fraction of simulation-owned yaw environmental disturbance resisted.
    /// 0 = none, 1 = fully resisted.
    /// </summary>
    public float YawDisturbanceResistance =>
        Mathf.Clamp01(yawDisturbanceResistance);

    public bool HasSteering =>
        MaxTurnAngle > 0.0001f &&
        TurnEfficiency > 0.0001f;

    public BoatHandlingProfile(
        float maxTurnAngle,
        float turnEfficiency,
        float lateralDisturbanceResistance,
        float yawDisturbanceResistance)
    {
        this.maxTurnAngle =
            Mathf.Max(
                0f,
                maxTurnAngle);

        this.turnEfficiency =
            Mathf.Max(
                0f,
                turnEfficiency);

        this.lateralDisturbanceResistance =
            Mathf.Clamp01(
                lateralDisturbanceResistance);

        this.yawDisturbanceResistance =
            Mathf.Clamp01(
                yawDisturbanceResistance);
    }

    public static BoatHandlingProfile Empty =>
        new BoatHandlingProfile(
            0f,
            0f,
            0f,
            0f);
}

/// <summary>
/// Implemented by installed module components that contribute steering behavior
/// to the boat's aggregate virtual handling profile.
///
/// Keep this interface deliberately tiny.
/// </summary>
public interface IBoatHandlingContributor
{
    bool IsHandlingContributionActive { get; }

    /// <summary>
    /// Maximum steering angle this contributor supports.
    /// Aggregation uses the highest active contribution, not the sum.
    /// </summary>
    float MaxTurnAngleContribution { get; }

    /// <summary>
    /// Steering authority contributed by this part.
    /// Aggregation adds active contributions together.
    /// A standard single-rudder baseline is 1.
    /// </summary>
    float TurnEfficiencyContribution { get; }
}

/// <summary>
/// Implemented by installed module components that resist simulation-owned
/// environmental course disturbance.
///
/// Keels are the first user, but the interface stays part-oriented so future
/// modules can contribute without BoatPilotingSimulation knowing what they are.
/// </summary>
public interface IBoatDisturbanceResistanceContributor
{
    bool IsHandlingContributionActive { get; }

    /// <summary>
    /// Fraction of lateral environmental disturbance resisted by this contributor.
    /// Values are clamped to 0..1 by the aggregator.
    /// </summary>
    float LateralDisturbanceResistanceContribution { get; }

    /// <summary>
    /// Fraction of yaw environmental disturbance resisted by this contributor.
    /// Values are clamped to 0..1 by the aggregator.
    /// </summary>
    float YawDisturbanceResistanceContribution { get; }
}
