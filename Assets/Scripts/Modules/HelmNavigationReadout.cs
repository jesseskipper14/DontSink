using UnityEngine;

/// <summary>
/// Player-facing navigation estimate consumed by the Helm UI.
///
/// IMPORTANT:
/// This is NOT authoritative navigation truth. Future lost-navigation/celestial
/// work should provide an IHelmNavigationReadoutSource that degrades these
/// estimates, confidence values, and availability.
/// </summary>
public enum HelmNavigationStatus
{
    Docked = 0,
    RoutePending = 1,
    Underway = 2
}

public struct HelmNavigationSnapshot
{
    public HelmNavigationStatus Status;

    public bool HasHeadingEstimate;
    public float EstimatedHeadingDegrees;
    public float HeadingConfidence01;

    public bool HasDesiredCourse;
    public float DesiredCourseDegrees;
    public float DesiredCourseConfidence01;

    public bool HasCourseError;
    public float CourseErrorDegrees;
    public float CourseErrorConfidence01;

    public bool HasCrossTrackEstimate;
    public float CrossTrackError;
    public float CrossTrackConfidence01;

    public float NavigationConfidence01;
}

public interface IHelmNavigationReadoutSource
{
    HelmNavigationSnapshot CaptureHelmNavigationReadout();
}

/// <summary>
/// Temporary Step-2 fallback.
///
/// It exposes the current piloting simulation as a perfect estimate so the
/// HelmCartridge is useful immediately. It deliberately sits behind
/// IHelmNavigationReadoutSource so the future lost-navigation pass can replace
/// it without touching HelmCartridge.
///
/// Do NOT turn this into a permanent "compass." The world has no working
/// compasses.
/// </summary>
public sealed class DefaultHelmNavigationReadoutSource :
    IHelmNavigationReadoutSource
{
    private readonly BoatPilotingSimulation _simulation;

    public DefaultHelmNavigationReadoutSource(
        BoatPilotingSimulation simulation)
    {
        _simulation = simulation;
    }

    public HelmNavigationSnapshot CaptureHelmNavigationReadout()
    {
        HelmNavigationSnapshot snapshot =
            new HelmNavigationSnapshot
            {
                Status =
                    ResolveNavigationStatus(),
                NavigationConfidence01 = 0f
            };

        // Docked and route-pending are voyage-context states, not poor-quality
        // navigation fixes. They expose no heading/course/cross-track estimates.
        if (snapshot.Status !=
            HelmNavigationStatus.Underway)
        {
            return snapshot;
        }

        if (_simulation == null ||
            _simulation.State == null)
        {
            return snapshot;
        }

        BoatPilotingState state =
            _simulation.State;

        snapshot.HasHeadingEstimate = true;
        snapshot.EstimatedHeadingDegrees =
            Normalize360(state.HeadingDegrees);
        snapshot.HeadingConfidence01 = 1f;
        snapshot.NavigationConfidence01 = 1f;

        BoatPilotingRouteState route =
            _simulation.RouteGuidance;

        if (route == null ||
            !route.IsInitialized)
        {
            return snapshot;
        }

        if (!route.TrySampleAtWorldY(
                state.NavigationPosition.y,
                out Vector2 routePosition,
                out float desiredHeading))
        {
            return snapshot;
        }

        desiredHeading =
            Normalize360(desiredHeading);

        snapshot.HasDesiredCourse = true;
        snapshot.DesiredCourseDegrees =
            desiredHeading;
        snapshot.DesiredCourseConfidence01 = 1f;

        snapshot.HasCourseError = true;
        snapshot.CourseErrorDegrees =
            Mathf.DeltaAngle(
                Normalize360(state.HeadingDegrees),
                desiredHeading);
        snapshot.CourseErrorConfidence01 = 1f;

        // Navigation +X is starboard/cross-track in the current piloting space.
        snapshot.HasCrossTrackEstimate = true;
        snapshot.CrossTrackError =
            state.NavigationPosition.x -
            routePosition.x;
        snapshot.CrossTrackConfidence01 = 1f;

        return snapshot;
    }

    private static HelmNavigationStatus ResolveNavigationStatus()
    {
        GameState gameState =
            GameState.I;

        if (gameState != null &&
            gameState.activeTravel != null)
        {
            return HelmNavigationStatus.Underway;
        }

        if (gameState != null &&
            gameState.player != null &&
            !string.IsNullOrWhiteSpace(
                gameState.player.lockedDestinationNodeId))
        {
            return HelmNavigationStatus.RoutePending;
        }

        return HelmNavigationStatus.Docked;
    }

    private static float Normalize360(
        float degrees)
    {
        return Mathf.Repeat(
            degrees,
            360f);
    }
}