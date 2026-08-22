using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only renderer for the recommended piloting route.
/// The route itself and route progress remain owned by BoatPilotingRouteState.
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

        IReadOnlyList<
            BoatPilotingRouteState.RoutePoint> points =
            route.Points;

        if (points == null ||
            points.Count < 2)
        {
            return;
        }

        float worldUnitsPerPixel =
            Mathf.Max(
                0.0001f,
                view.WorldUnitsPerPixelY);

        // The renderer consumes the simulation-owned adherence width so the
        // visible wake and the scoring corridor cannot silently drift apart.
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

        for (int i = 0;
             i < points.Count - 1;
             i++)
        {
            Vector2 aWorld =
                points[i].position;

            Vector2 bWorld =
                points[i + 1].position;

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
                    0.055f);

            DrawArbitraryLine(
                a,
                b,
                outerWidth);

            GUI.color =
                WithAlpha(
                    RoutePathColor,
                    0.085f);

            DrawArbitraryLine(
                a,
                b,
                middleWidth);

            GUI.color =
                WithAlpha(
                    RoutePathColor,
                    0.14f);

            DrawArbitraryLine(
                a,
                b,
                coreWidth);
        }

        GUI.color =
            previous;
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