using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boat-level authority for the tiny aggregate handling profile.
///
/// It reads installed module components through hardpoints and exposes one
/// result to piloting. BoatPilotingSimulation does not need to know what a
/// rudder, keel, helm, or future bizarre player invention actually is.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoatHandlingAggregator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;

    [Header("Runtime")]
    [SerializeField] private BoatHandlingProfile currentProfile;

    private Hardpoint[] hardpoints;
    private readonly List<MonoBehaviour> moduleComponents = new();

    public BoatHandlingProfile CurrentProfile => currentProfile;

    private void Reset()
    {
        ResolveBoat();
        RefreshHardpoints();
        RefreshProfile();
    }

    private void Awake()
    {
        ResolveBoat();
        RefreshHardpoints();
        RefreshProfile();
    }

    private void OnEnable()
    {
        ResolveBoat();

        if (hardpoints == null || hardpoints.Length == 0)
            RefreshHardpoints();

        RefreshProfile();
    }

    /// <summary>
    /// Rebuild this cache if hardpoints themselves are structurally added or removed.
    /// Installing/removing modules does not require a hardpoint cache refresh.
    /// </summary>
    public void RefreshHardpoints()
    {
        ResolveBoat();

        Transform root =
            boat != null
                ? boat.transform
                : transform;

        hardpoints =
            root.GetComponentsInChildren<Hardpoint>(true);
    }

    /// <summary>
    /// Recomputes the profile from currently installed active contributors.
    ///
    /// Steering:
    /// - max turn angle uses the best active supported angle
    /// - turn efficiency sums active steering authority
    ///
    /// Environmental resistance:
    /// - active contributions combine with diminishing returns
    /// - e.g. two 30% contributors produce 51% total resistance, not 60%
    /// </summary>
    public BoatHandlingProfile RefreshProfile()
    {
        if (hardpoints == null)
            RefreshHardpoints();

        float maxTurnAngle = 0f;
        float turnEfficiency = 0f;
        float lateralDisturbanceResistance = 0f;
        float yawDisturbanceResistance = 0f;

        if (hardpoints != null)
        {
            for (int i = 0; i < hardpoints.Length; i++)
            {
                Hardpoint hardpoint =
                    hardpoints[i];

                if (hardpoint == null ||
                    !hardpoint.HasInstalledModule ||
                    hardpoint.InstalledModule == null)
                {
                    continue;
                }

                moduleComponents.Clear();

                hardpoint.InstalledModule.GetComponents(
                    moduleComponents);

                for (int j = 0;
                     j < moduleComponents.Count;
                     j++)
                {
                    MonoBehaviour component =
                        moduleComponents[j];

                    if (component == null)
                        continue;

                    if (component is IBoatHandlingContributor steeringContributor &&
                        steeringContributor.IsHandlingContributionActive)
                    {
                        maxTurnAngle =
                            Mathf.Max(
                                maxTurnAngle,
                                Mathf.Max(
                                    0f,
                                    steeringContributor.MaxTurnAngleContribution));

                        turnEfficiency +=
                            Mathf.Max(
                                0f,
                                steeringContributor.TurnEfficiencyContribution);
                    }

                    if (component is IBoatDisturbanceResistanceContributor resistanceContributor &&
                        resistanceContributor.IsHandlingContributionActive)
                    {
                        lateralDisturbanceResistance =
                            CombineResistance(
                                lateralDisturbanceResistance,
                                resistanceContributor.LateralDisturbanceResistanceContribution);

                        yawDisturbanceResistance =
                            CombineResistance(
                                yawDisturbanceResistance,
                                resistanceContributor.YawDisturbanceResistanceContribution);
                    }
                }
            }
        }

        currentProfile =
            new BoatHandlingProfile(
                maxTurnAngle,
                turnEfficiency,
                lateralDisturbanceResistance,
                yawDisturbanceResistance);

        return currentProfile;
    }

    /// <summary>
    /// Combines independent resistance contributions without allowing multiple
    /// modules to exceed 100% resistance through simple addition.
    /// </summary>
    private static float CombineResistance(
        float currentResistance,
        float addedResistance)
    {
        float current =
            Mathf.Clamp01(
                currentResistance);

        float added =
            Mathf.Clamp01(
                addedResistance);

        return
            1f -
            (1f - current) *
            (1f - added);
    }

    private void ResolveBoat()
    {
        if (boat != null)
            return;

        boat =
            GetComponent<Boat>() ??
            GetComponentInParent<Boat>();
    }
}
