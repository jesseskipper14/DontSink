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
    /// Max turn angle uses the best active supported angle.
    /// Turn efficiency sums active steering authority.
    /// </summary>
    public BoatHandlingProfile RefreshProfile()
    {
        if (hardpoints == null)
            RefreshHardpoints();

        float maxTurnAngle = 0f;
        float turnEfficiency = 0f;

        if (hardpoints != null)
        {
            for (int i = 0; i < hardpoints.Length; i++)
            {
                Hardpoint hardpoint = hardpoints[i];

                if (hardpoint == null ||
                    !hardpoint.HasInstalledModule ||
                    hardpoint.InstalledModule == null)
                {
                    continue;
                }

                moduleComponents.Clear();
                hardpoint.InstalledModule.GetComponents(moduleComponents);

                for (int j = 0; j < moduleComponents.Count; j++)
                {
                    MonoBehaviour component = moduleComponents[j];

                    if (!(component is IBoatHandlingContributor contributor))
                        continue;

                    if (!contributor.IsHandlingContributionActive)
                        continue;

                    maxTurnAngle =
                        Mathf.Max(
                            maxTurnAngle,
                            Mathf.Max(0f, contributor.MaxTurnAngleContribution));

                    turnEfficiency +=
                        Mathf.Max(
                            0f,
                            contributor.TurnEfficiencyContribution);
                }
            }
        }

        currentProfile =
            new BoatHandlingProfile(
                maxTurnAngle,
                turnEfficiency);

        return currentProfile;
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