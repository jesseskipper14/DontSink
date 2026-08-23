using UnityEngine;

[System.Serializable]
public struct BoatHandlingProfile
{
    [SerializeField] private float maxTurnAngle;
    [SerializeField] private float turnEfficiency;

    public float MaxTurnAngle => Mathf.Max(0f, maxTurnAngle);
    public float TurnEfficiency => Mathf.Max(0f, turnEfficiency);

    public bool HasSteering =>
        MaxTurnAngle > 0.0001f &&
        TurnEfficiency > 0.0001f;

    public BoatHandlingProfile(
        float maxTurnAngle,
        float turnEfficiency)
    {
        this.maxTurnAngle = Mathf.Max(0f, maxTurnAngle);
        this.turnEfficiency = Mathf.Max(0f, turnEfficiency);
    }

    public static BoatHandlingProfile Empty =>
        new BoatHandlingProfile(0f, 0f);
}

/// <summary>
/// Implemented by installed module components that contribute to the boat's
/// aggregate virtual handling profile.
///
/// Keep this interface deliberately tiny. Add new values only when gameplay
/// actually needs them.
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