using UnityEngine;

public interface IBoatControlAuthorityDisplayNameProvider
{
    string ControlAuthorityDisplayName { get; }
}

public struct HelmReadoutSnapshot
{
    public bool HasHelm;
    public bool HelmOnline;
    public bool HelmDamaged;

    public string ControlAuthority;

    public int ConfiguredStations;
    public int ActiveStations;
    public int StationCapacity;

    public bool HasPilotingState;
    public float Throttle;
    public float RudderDegrees;

    public bool HasSteering;
    public float MaxRudderDegrees;

    public int InstalledPropulsionSources;
    public int ActivePropulsionSources;

    public HelmNavigationSnapshot Navigation;
}

/// <summary>
/// Collects the live ship/helm state consumed by HelmCartridge.
/// HelmCartridge stays presentation-only and does not rummage through every
/// boat component itself.
/// </summary>
public sealed class HelmReadoutSource
{
    private readonly Hardpoint _hardpoint;
    private readonly HelmModule _helm;
    private readonly BoatPilotingSimulation _simulation;
    private readonly IHelmNavigationReadoutSource _navigationSource;

    public HelmReadoutSource(
        Hardpoint hardpoint,
        HelmModule helm,
        BoatPilotingSimulation simulation,
        IHelmNavigationReadoutSource navigationSource)
    {
        _hardpoint = hardpoint;
        _helm = helm;
        _simulation = simulation;

        _navigationSource =
            navigationSource ??
            new DefaultHelmNavigationReadoutSource(
                simulation);
    }

    public HelmReadoutSnapshot Capture()
    {
        HelmReadoutSnapshot snapshot =
            new HelmReadoutSnapshot
            {
                HasHelm = _helm != null,
                ControlAuthority =
                    ResolveAuthorityLabel(
                        _simulation != null
                            ? _simulation.ControlOwner
                            : null)
            };

        if (_helm != null)
        {
            snapshot.HelmOnline =
                _helm.IsOnline;

            snapshot.HelmDamaged =
                _helm.IsDamaged;

            snapshot.ConfiguredStations =
                _helm.ConfiguredPilotStationCount;

            snapshot.ActiveStations =
                _helm.ActivePilotStationCount;

            snapshot.StationCapacity =
                _helm.StationConnectionCapacity;
        }

        if (_simulation != null)
        {
            snapshot.InstalledPropulsionSources =
                _simulation.InstalledPropulsionSources;

            snapshot.ActivePropulsionSources =
                _simulation.ActivePropulsionSources;

            snapshot.HasSteering =
                _simulation.HasSteering;

            snapshot.MaxRudderDegrees =
                _simulation.MaxRudderDegrees;

            BoatPilotingState state =
                _simulation.State;

            if (state != null)
            {
                snapshot.HasPilotingState = true;
                snapshot.Throttle =
                    state.Throttle;
                snapshot.RudderDegrees =
                    state.RudderDegrees;
            }
        }

        if (_navigationSource != null)
        {
            snapshot.Navigation =
                _navigationSource
                    .CaptureHelmNavigationReadout();
        }

        return snapshot;
    }

    private static string ResolveAuthorityLabel(
        object owner)
    {
        if (owner == null)
            return "NONE";

        if (owner is IBoatControlAuthorityDisplayNameProvider named)
        {
            string supplied =
                named.ControlAuthorityDisplayName;

            if (!string.IsNullOrWhiteSpace(
                    supplied))
            {
                return supplied;
            }
        }

        if (owner is Component component &&
            component != null)
        {
            return CleanDisplayName(
                component.name);
        }

        if (owner is GameObject go &&
            go != null)
        {
            return CleanDisplayName(
                go.name);
        }

        string typeName =
            owner.GetType().Name;

        return CleanDisplayName(
            typeName);
    }

    private static string CleanDisplayName(
        string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "UNKNOWN";

        string s =
            raw.Trim()
               .Replace("(Clone)", "")
               .Replace("_", " ");

        return s.Trim()
                .ToUpperInvariant();
    }
}