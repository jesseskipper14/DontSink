using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only renderer for the recommended piloting route.
///
/// The original route remains owned by BoatPilotingRouteState. During a
/// positional recovery, the renderer draws the temporary recovery curve first
/// and only reveals the original route from the selected rejoin point onward.
/// </summary>
public sealed class PilotingRouteRenderer
{
    private static readonly Color RoutePathColor =
        new Color(
            0.34f,
            0.78f,
            0.92f,
            1f);

    public void Draw(
        PilotingViewProjection view,
        BoatPilotingRouteState route)
    {
        if (route == null ||
            !route.IsInitialized)
        {
            return;
        }

        // Reality still owns the route when the player is lost. Presentation
        // simply stops revealing it.
        float routeVisibility01 =
            Mathf.Clamp01(
                route.NavigationCertainty01);

        if (routeVisibility01 <= 0.0001f)
            return;

        float worldUnitsPerPixel =
            Mathf.Max(
                0.0001f,
                view.WorldUnitsPerPixelY);

        // The renderer consumes the simulation-owned adherence width so visible
        // guidance and scoring cannot silently become different routes.
        float fullWidthPixels =
            Mathf.Max(
                2f,
                route.AdherencePathWorldWidth /
                worldUnitsPerPixel);

        float outerWidth =
            fullWidthPixels;

        float middleWidth =
            fullWidthPixels *
            0.62f;

        float coreWidth =
            fullWidthPixels *
            0.28f;

        Color previous =
            GUI.color;

        if (route.IsRecovering)
        {
            DrawPath(
                view,
                route.RecoveryPoints,
                0f,
                routeVisibility01,
                outerWidth,
                middleWidth,
                coreWidth);
        }

        DrawPath(
            view,
            route.Points,
            route.VisibleOriginalRouteStartDistance,
            routeVisibility01,
            outerWidth,
            middleWidth,
            coreWidth);

        GUI.color =
            previous;
    }

    private static void DrawPath(
        PilotingViewProjection view,
        IReadOnlyList<BoatPilotingRouteState.RoutePoint> points,
        float minimumCumulativeDistance,
        float routeVisibility01,
        float outerWidth,
        float middleWidth,
        float coreWidth)
    {
        if (points == null ||
            points.Count < 2)
        {
            return;
        }

        float minimumDistance =
            Mathf.Max(
                0f,
                minimumCumulativeDistance);

        for (int i = 0;
             i < points.Count - 1;
             i++)
        {
            BoatPilotingRouteState.RoutePoint aPoint =
                points[i];

            BoatPilotingRouteState.RoutePoint bPoint =
                points[i + 1];

            if (bPoint.cumulativeDistance <
                minimumDistance)
            {
                continue;
            }

            Vector2 aWorld =
                aPoint.position;

            Vector2 bWorld =
                bPoint.position;

            // Trim the first visible original-route segment exactly to the
            // recovery rejoin distance so old pre-fix route history does not
            // quietly reappear.
            if (aPoint.cumulativeDistance <
                    minimumDistance &&
                bPoint.cumulativeDistance >
                    aPoint.cumulativeDistance)
            {
                float t =
                    Mathf.Clamp01(
                        (minimumDistance -
                         aPoint.cumulativeDistance) /
                        (bPoint.cumulativeDistance -
                         aPoint.cumulativeDistance));

                aWorld =
                    Vector2.Lerp(
                        aPoint.position,
                        bPoint.position,
                        t);
            }

            if (bWorld.y <
                    view.Bottom ||
                aWorld.y >
                    view.Top)
            {
                continue;
            }

            Vector2 a =
                view.WorldToPanel(
                    aWorld);

            Vector2 b =
                view.WorldToPanel(
                    bWorld);

            GUI.color =
                WithAlpha(
                    RoutePathColor,
                    0.055f *
                    routeVisibility01);

            DrawArbitraryLine(
                a,
                b,
                outerWidth);

            GUI.color =
                WithAlpha(
                    RoutePathColor,
                    0.085f *
                    routeVisibility01);

            DrawArbitraryLine(
                a,
                b,
                middleWidth);

            GUI.color =
                WithAlpha(
                    RoutePathColor,
                    0.14f *
                    routeVisibility01);

            DrawArbitraryLine(
                a,
                b,
                coreWidth);
        }
    }

    private static Color WithAlpha(
        Color color,
        float alpha)
    {
        return new Color(
            color.r,
            color.g,
            color.b,
            alpha);
    }

    private static void DrawArbitraryLine(
        Vector2 a,
        Vector2 b,
        float thickness)
    {
        Vector2 delta =
            b -
            a;

        float length =
            delta.magnitude;

        if (length <= 0.01f)
            return;

        float angle =
            Mathf.Atan2(
                delta.y,
                delta.x) *
            Mathf.Rad2Deg;

        Matrix4x4 previous =
            GUI.matrix;

        GUIUtility.RotateAroundPivot(
            angle,
            a);

        GUI.DrawTexture(
            new Rect(
                a.x,
                a.y -
                    thickness * 0.5f,
                length,
                thickness),
            Texture2D.whiteTexture);

        GUI.matrix =
            previous;
    }
}
