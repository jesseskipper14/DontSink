using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Installed steering/control infrastructure.
///
/// The PilotChair is only a control station. The installed HelmModule decides
/// how many configured PilotChair stations are actually connected/usable.
///
/// Connection order is the owning Helm Hardpoint's Controllers array order.
/// Example:
///     capacity 1 -> first linked PilotChair is active
///     capacity 2 -> first two linked PilotChairs are active
///
/// Future helm responsibilities (damage, maintenance, command response,
/// autopilot) can grow here without moving rudder/engine authority into the helm.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InstalledModule))]
public sealed class HelmModule : MonoBehaviour, IInstalledModuleLifecycle
{
    public enum HelmCondition
    {
        Healthy = 0,
        Damaged = 1,
        Destroyed = 2
    }

    [Header("Control Stations")]
    [Tooltip("Maximum number of linked PilotChair stations this helm can support. " +
             "Linked station priority follows the owning hardpoint's Controllers array order.")]
    [SerializeField, Min(1)] private int stationConnectionCapacity = 1;

    [Header("Condition")]
    [Tooltip("Runtime/test hook for helm health. Damaged helms remain online. Destroyed helms are offline. " +
             "A future module-damage system should drive this through SetCondition.")]
    [SerializeField] private HelmCondition condition = HelmCondition.Healthy;

    private Hardpoint _ownerHardpoint;
    private BoatPilotingSimulation _pilotingSimulation;

    public int StationConnectionCapacity =>
        Mathf.Max(1, stationConnectionCapacity);

    public Hardpoint OwnerHardpoint
    {
        get
        {
            ResolveOwnerHardpoint();
            return _ownerHardpoint;
        }
    }

    public HelmCondition Condition => condition;
    public bool IsDamaged => condition == HelmCondition.Damaged;
    public bool IsDestroyed => condition == HelmCondition.Destroyed;

    /// <summary>
    /// Healthy and Damaged helms are online. Destroyed, disabled, detached, or
    /// otherwise non-installed helms are offline.
    /// </summary>
    public bool IsOnline
    {
        get
        {
            ResolveOwnerHardpoint();

            if (!isActiveAndEnabled ||
                condition == HelmCondition.Destroyed ||
                _ownerHardpoint == null)
            {
                return false;
            }

            if (!_ownerHardpoint.HasInstalledModule ||
                _ownerHardpoint.InstalledModule == null)
            {
                return false;
            }

            return _ownerHardpoint.InstalledModule.GetComponent<HelmModule>() == this;
        }
    }

    // Compatibility name for code that asks whether this installed helm can
    // participate in control at all.
    public bool IsFunctional => IsOnline;

    /// <summary>
    /// Read-only authority hook for the future HelmCartridge.
    /// The BoatPilotingSimulation remains the authority source of truth.
    /// </summary>
    public object ControlAuthority
    {
        get
        {
            ResolvePilotingSimulation();
            return _pilotingSimulation != null
                ? _pilotingSimulation.ControlOwner
                : null;
        }
    }

    public void SetCondition(HelmCondition newCondition)
    {
        condition = newCondition;
    }

    public int ConfiguredPilotStationCount
    {
        get
        {
            ResolveOwnerHardpoint();

            if (_ownerHardpoint == null ||
                _ownerHardpoint.Controllers == null)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<MonoBehaviour> controllers =
                _ownerHardpoint.Controllers;

            for (int i = 0; i < controllers.Count; i++)
            {
                if (controllers[i] is PilotChairInteractable)
                    count++;
            }

            return count;
        }
    }

    public int ActivePilotStationCount =>
        Mathf.Min(
            ConfiguredPilotStationCount,
            StationConnectionCapacity);

    private void Awake()
    {
        ResolveOwnerHardpoint();
    }

    private void OnValidate()
    {
        stationConnectionCapacity =
            Mathf.Max(1, stationConnectionCapacity);
    }

    public void OnInstalled(Hardpoint owner)
    {
        _ownerHardpoint = owner;
        _pilotingSimulation = null;
    }

    public void OnRemoved()
    {
        _ownerHardpoint = null;
        _pilotingSimulation = null;
    }

    /// <summary>
    /// Returns the station's zero-based connection slot among PilotChair
    /// controllers only. Non-chair controllers do not consume helm capacity.
    /// </summary>
    public bool TryGetStationSlot(
        PilotChairInteractable station,
        out int slotIndex)
    {
        slotIndex = -1;

        if (station == null)
            return false;

        ResolveOwnerHardpoint();

        if (_ownerHardpoint == null ||
            _ownerHardpoint.Controllers == null)
        {
            return false;
        }

        IReadOnlyList<MonoBehaviour> controllers =
            _ownerHardpoint.Controllers;

        int pilotSlot = 0;

        for (int i = 0; i < controllers.Count; i++)
        {
            if (controllers[i] is not PilotChairInteractable candidate)
                continue;

            if (candidate == null)
                continue;

            if (ReferenceEquals(candidate, station))
            {
                slotIndex = pilotSlot;
                return true;
            }

            pilotSlot++;
        }

        return false;
    }

    public bool CanServeStation(
        PilotChairInteractable station)
    {
        if (!IsFunctional)
            return false;

        if (!TryGetStationSlot(
                station,
                out int slotIndex))
        {
            return false;
        }

        return slotIndex >= 0 &&
               slotIndex < StationConnectionCapacity;
    }

    private void ResolvePilotingSimulation()
    {
        if (_pilotingSimulation != null)
            return;

        ResolveOwnerHardpoint();

        if (_ownerHardpoint == null)
            return;

        Boat boat =
            _ownerHardpoint.GetComponentInParent<Boat>();

        if (boat == null)
            return;

        _pilotingSimulation =
            boat.GetComponent<BoatPilotingSimulation>() ??
            boat.GetComponentInChildren<BoatPilotingSimulation>(true);
    }

    private void ResolveOwnerHardpoint()
    {
        if (_ownerHardpoint != null)
            return;

        InstalledModule installed =
            GetComponent<InstalledModule>();

        if (installed != null &&
            installed.OwnerHardpoint != null)
        {
            _ownerHardpoint =
                installed.OwnerHardpoint;
            return;
        }

        _ownerHardpoint =
            GetComponentInParent<Hardpoint>();
    }
}