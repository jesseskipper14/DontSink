using UnityEngine;

/// <summary>
/// Presentation-only piloting HUD.
///
/// Normal controls remain visible. Developer diagnostics are intentionally
/// hidden behind the cartridge's DBG menu and never use periodic console logs.
/// </summary>
public sealed class PilotingHudRenderer
{
    public void Draw(
        Rect panel,
        Rect playArea,
        PilotingViewProjection view,
        BoatPilotingState state,
        BoatPilotingSimulation simulation,
        PilotingWaveRenderer waves,
        bool zoomLocked,
        bool debugMenuOpen)
    {
        if (state == null ||
            simulation == null)
        {
            return;
        }

        DrawControls(
            panel,
            playArea,
            state,
            simulation);

        if (debugMenuOpen)
        {
            DrawDebugPanel(
                playArea,
                view,
                state,
                simulation,
                waves,
                zoomLocked);
        }
    }

    private static void DrawControls(
        Rect panel,
        Rect playArea,
        BoatPilotingState state,
        BoatPilotingSimulation simulation)
    {
        float controlsY =
            playArea.yMax + 28f;

        float leftX =
            panel.x + 14f;

        float width =
            panel.width - 28f;

        float half =
            (width - 14f) *
            0.5f;

        DrawThrottleBar(
            new Rect(
                leftX,
                controlsY,
                half,
                16f),
            state);

        DrawRudderBar(
            new Rect(
                leftX + half + 14f,
                controlsY,
                half,
                16f),
            state,
            simulation);
    }

    private static void DrawDebugPanel(
        Rect playArea,
        PilotingViewProjection view,
        BoatPilotingState state,
        BoatPilotingSimulation simulation,
        PilotingWaveRenderer waves,
        bool zoomLocked)
    {
        float width =
            Mathf.Min(
                430f,
                playArea.width - 20f);

        float height =
            Mathf.Min(
                390f,
                playArea.height - 20f);

        Rect debugRect =
            new Rect(
                playArea.xMax - width - 10f,
                playArea.y + 10f,
                width,
                height);

        Color previousColor =
            GUI.color;

        GUI.color =
            new Color(
                0f,
                0f,
                0f,
                0.82f);

        GUI.DrawTexture(
            debugRect,
            Texture2D.whiteTexture);

        GUI.color =
            previousColor;

        GUI.Box(
            debugRect,
            GUIContent.none);

        float x =
            debugRect.x + 12f;

        float y =
            debugRect.y + 8f;

        float labelWidth =
            debugRect.width - 24f;

        const float line =
            19f;

        GUI.Label(
            new Rect(
                x,
                y,
                labelWidth,
                line),
            "PILOTING DEBUG");

        y +=
            line + 2f;

        BoatPilotingRouteState route =
            simulation.RouteGuidance;

        if (route != null)
        {
            string recentQuality =
                route.HasCourseQualitySamples
                    ? $"{route.RecentCourseQuality01 * 100f:0.0}%"
                    : "--";

            string voyageAverage =
                route.PhysicalTravelDistance > 0.0001f
                    ? $"{route.VoyageAverageAdherence01 * 100f:0.0}%"
                    : "--";

            string routeEfficiency =
                route.PhysicalTravelDistance > 0.0001f
                    ? $"{route.RouteTravelEfficiency01 * 100f:0.0}%"
                    : "--";

            string insidePath =
                route.PhysicalTravelDistance > 0.0001f
                    ? $"{route.DistanceInsideAdherencePath01 * 100f:0.0}%"
                    : "--";

            GUI.Label(
                new Rect(
                    x,
                    y,
                    labelWidth,
                    line),
                $"COURSE   Now {route.CurrentAdherence01 * 100f:0.0}%   " +
                $"Recent {recentQuality}   " +
                $"Instability {route.CourseInstability01 * 100f:0.0}%   " +
                $"Handling {route.HandlingEfficiency01 * 100f:0.0}%");

            y +=
                line;

            GUI.Label(
                new Rect(
                    x,
                    y,
                    labelWidth,
                    line),
                $"PATH     Off {route.OffRouteDistance:0.00}   " +
                $"Wake width {route.AdherencePathWorldWidth:0.00}   " +
                $"{(route.IsInsideAdherencePath ? "IN WAKE" : "OUTSIDE WAKE")}");

            y +=
                line;

            GUI.Label(
                new Rect(
                    x,
                    y,
                    labelWidth,
                    line),
                $"TRAVEL   Physical {route.PhysicalTravelDistance:0.0}   " +
                $"Current Route {route.CurrentRouteDistance:0.0}   " +
                $"Efficiency {routeEfficiency}");

            y +=
                line;

            GUI.Label(
                new Rect(
                    x,
                    y,
                    labelWidth,
                    line),
                $"SUMMARY  Voyage avg {voyageAverage}   " +
                $"Distance in wake {insidePath}");

            y +=
                line;

            GUI.Label(
                new Rect(
                    x,
                    y,
                    labelWidth,
                    line),
                $"ROUTE    Current {route.CurrentRouteDistance:0.0}   " +
                $"Farthest {route.FarthestRouteDistance:0.0}   " +
                $"Tracking radius {route.ProgressCorridor:0.0}   " +
                $"{(route.IsInsideProgressCorridor ? "TRACKING" : "FARTHEST PAUSED")}");

            y +=
                line + 4f;
        }

        GUI.Label(
            new Rect(
                x,
                y,
                labelWidth,
                line),
            $"NAV      Pos ({state.NavigationPosition.x:0.00}, {state.NavigationPosition.y:0.00})   " +
            $"Heading {state.HeadingDegrees:0.0}°   " +
            $"Nav speed {state.NavigationVelocity.magnitude:0.00}");

        y +=
            line;

        GUI.Label(
            new Rect(
                x,
                y,
                labelWidth,
                line),
            $"BOAT     Physical fwd {simulation.PhysicalForwardSpeed:+0.00;-0.00;0.00}   " +
            $"Propulsion {simulation.ActivePropulsionSources}/{simulation.InstalledPropulsionSources}");

        y +=
            line + 4f;

        GUI.Label(
            new Rect(
                x,
                y,
                labelWidth,
                line),
            $"SEA      Amp {waves.Amplitude:0.00} (temporary storm proxy)   " +
            $"Wave speed {waves.WorldSpeed:0.00}   Spacing {waves.Spacing:0.00}");

        y +=
            line;

        GUI.Label(
            new Rect(
                x,
                y,
                labelWidth,
                line),
            $"DISTURB  Severity {simulation.EnvironmentalSeverity01 * 100f:0}%   " +
            $"Beam {simulation.EnvironmentalBeamExposure01 * 100f:0}%   " +
            $"Angle {simulation.EnvironmentalEncounterBroadsideDegrees:0}°   " +
            $"Pulses {simulation.EnvironmentalPulseCount}");

        y +=
            line;

        Vector2 lastLateralKick = simulation.LastEnvironmentalLateralVelocityKick;

        GUI.Label(
            new Rect(
                x,
                y,
                labelWidth,
                line),
            $"DRIFT    Vel ({simulation.EnvironmentalNavigationVelocity.x:+0.00;-0.00;0.00}," +
            $"{simulation.EnvironmentalNavigationVelocity.y:+0.00;-0.00;0.00})   " +
            $"Last Lat {lastLateralKick.magnitude:0.00}   " +
            $"Last Yaw {simulation.LastEnvironmentalYawVelocityKickDegrees:+0.0;-0.0;0.0}°/s   " +
            $"Next {simulation.EnvironmentalSecondsUntilNextPulse:0.0}s");

        y +=
            line;

        GUI.Label(
            new Rect(
                x,
                y,
                labelWidth,
                line),
            $"VIEW     {view.VisibleWorldWidth:0}x{view.VisibleWorldHeight:0} " +
            $"{(zoomLocked ? "LOCKED" : "UNLOCKED")}   " +
            $"WaveTex {waves.MeasuredTextureBuildHz:0}Hz / {waves.LastTextureBuildMs:0.0}ms");
    }

    private static void DrawThrottleBar(
        Rect rect,
        BoatPilotingState state)
    {
        float throttle =
            Mathf.Clamp(
                state.Throttle,
                -1f,
                1f);

        string direction =
            throttle > 0.005f
                ? "FWD"
                : throttle < -0.005f
                    ? "REV"
                    : "NEUTRAL";

        GUI.Label(
            new Rect(
                rect.x,
                rect.y - 2f,
                105f,
                20f),
            $"Throttle {direction} {Mathf.Abs(throttle) * 100f:0}%");

        Rect bar =
            new Rect(
                rect.x + 112f,
                rect.y + 3f,
                Mathf.Max(
                    1f,
                    rect.width - 112f),
                10f);

        DrawBarBackground(
            bar);

        float centerX =
            bar.center.x;

        float knobX =
            Mathf.Lerp(
                bar.x,
                bar.xMax,
                (throttle + 1f) *
                0.5f);

        Color previous =
            GUI.color;

        GUI.color =
            new Color(
                1f,
                1f,
                1f,
                0.35f);

        GUI.DrawTexture(
            new Rect(
                centerX - 1f,
                bar.y - 2f,
                2f,
                bar.height + 4f),
            Texture2D.whiteTexture);

        GUI.color =
            new Color(
                1f,
                1f,
                1f,
                0.55f);

        float fillX =
            Mathf.Min(
                centerX,
                knobX);

        float fillWidth =
            Mathf.Abs(
                knobX -
                centerX);

        if (fillWidth > 0.5f)
        {
            GUI.DrawTexture(
                new Rect(
                    fillX,
                    bar.y,
                    fillWidth,
                    bar.height),
                Texture2D.whiteTexture);
        }

        GUI.color =
            Color.white;

        GUI.DrawTexture(
            new Rect(
                knobX - 3f,
                bar.y - 3f,
                6f,
                bar.height + 6f),
            Texture2D.whiteTexture);

        GUI.color =
            previous;
    }

    private static void DrawRudderBar(
        Rect rect,
        BoatPilotingState state,
        BoatPilotingSimulation simulation)
    {
        GUI.Label(
            new Rect(
                rect.x,
                rect.y - 2f,
                90f,
                20f),
            $"Rudder {state.RudderDegrees:0.0}°");

        Rect bar =
            new Rect(
                rect.x + 98f,
                rect.y + 3f,
                Mathf.Max(
                    1f,
                    rect.width - 98f),
                10f);

        DrawBarBackground(
            bar);

        float maxRudder =
            Mathf.Max(
                0.1f,
                simulation.MaxRudderDegrees);

        float normalized =
            Mathf.Clamp(
                state.RudderDegrees /
                maxRudder,
                -1f,
                1f);

        float centerX =
            bar.center.x;

        float knobX =
            Mathf.Lerp(
                bar.x,
                bar.xMax,
                (normalized + 1f) *
                0.5f);

        Color previous =
            GUI.color;

        GUI.color =
            new Color(
                1f,
                1f,
                1f,
                0.35f);

        GUI.DrawTexture(
            new Rect(
                centerX - 1f,
                bar.y - 2f,
                2f,
                bar.height + 4f),
            Texture2D.whiteTexture);

        GUI.color =
            Color.white;

        GUI.DrawTexture(
            new Rect(
                knobX - 3f,
                bar.y - 3f,
                6f,
                bar.height + 6f),
            Texture2D.whiteTexture);

        GUI.color =
            previous;
    }

    private static void DrawBarBackground(
        Rect rect)
    {
        Color previous =
            GUI.color;

        GUI.color =
            new Color(
                1f,
                1f,
                1f,
                0.14f);

        GUI.DrawTexture(
            rect,
            Texture2D.whiteTexture);

        GUI.color =
            previous;
    }
}
