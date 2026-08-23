using UnityEngine;

/// <summary>
/// First real installed contributor to BoatHandlingProfile.
///
/// This is intentionally not a hydrodynamics simulator. The rudder provides
/// only the maximum supported steering angle and steering authority.
/// </summary>
[DisallowMultipleComponent]
public sealed class RudderModule : MonoBehaviour, IBoatHandlingContributor
{
    [Header("Handling")]
    [Tooltip("Maximum steering angle this rudder supports in either direction.")]
    [SerializeField, Min(0f)] private float maxTurnAngle = 35f;

    [Tooltip("Steering authority contributed by this rudder. A normal baseline rudder is 1.")]
    [SerializeField, Min(0f)] private float turnEfficiency = 1f;

    public bool IsHandlingContributionActive =>
        isActiveAndEnabled;

    public float MaxTurnAngleContribution =>
        Mathf.Max(0f, maxTurnAngle);

    public float TurnEfficiencyContribution =>
        Mathf.Max(0f, turnEfficiency);

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxTurnAngle = Mathf.Max(0f, maxTurnAngle);
        turnEfficiency = Mathf.Max(0f, turnEfficiency);
    }
#endif
}