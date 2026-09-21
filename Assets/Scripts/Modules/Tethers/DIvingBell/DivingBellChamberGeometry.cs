using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared geometry helpers for the usable air/water chamber inside a diving bell.
///
/// The important contract is that WaterFill01 is a chamber AREA/VOLUME fraction,
/// not merely a linear interpolation between two arbitrary authored transforms.
/// For a horizontal water surface, this utility finds the world Y that produces
/// the requested filled fraction of the authored chamber collider.
///
/// Supported V1 geometry:
/// - BoxCollider2D
/// - single-path PolygonCollider2D
/// </summary>
public static class DivingBellChamberGeometry
{
    public static bool IsSupported(
        Collider2D chamberCollider)
    {
        if (chamberCollider is BoxCollider2D)
            return true;

        if (chamberCollider is PolygonCollider2D polygon)
        {
            return
                polygon.pathCount > 0 &&
                polygon.GetPath(0) != null &&
                polygon.GetPath(0).Length >= 3;
        }

        return false;
    }

    public static bool TryResolveHorizontalSurfaceY(
        Collider2D chamberCollider,
        float targetWaterFill01,
        List<Vector2> worldPolygon,
        List<Vector2> clippedPolygon,
        out float surfaceWorldY)
    {
        surfaceWorldY = 0f;

        if (worldPolygon == null ||
            clippedPolygon == null ||
            !TryBuildWorldPolygon(
                chamberCollider,
                worldPolygon))
        {
            return false;
        }

        float totalArea =
            PolygonArea(
                worldPolygon);

        if (totalArea <= 0.000001f)
            return false;

        float minY =
            float.MaxValue;

        float maxY =
            float.MinValue;

        for (int i = 0;
             i < worldPolygon.Count;
             i++)
        {
            float y =
                worldPolygon[i].y;

            minY =
                Mathf.Min(
                    minY,
                    y);

            maxY =
                Mathf.Max(
                    maxY,
                    y);
        }

        targetWaterFill01 =
            Mathf.Clamp01(
                targetWaterFill01);

        if (targetWaterFill01 <= 0.000001f)
        {
            surfaceWorldY = minY;
            return true;
        }

        if (targetWaterFill01 >= 0.999999f)
        {
            surfaceWorldY = maxY;
            return true;
        }

        float lowY = minY;
        float highY = maxY;

        // Filled polygon area below a horizontal clipping plane is monotonic in Y.
        // Binary-searching it keeps the gameplay waterline, breathing checks,
        // buoyancy and renderer all tied to the SAME chamber geometry.
        for (int iteration = 0;
             iteration < 18;
             iteration++)
        {
            float midY =
                (lowY + highY) *
                0.5f;

            ClipBelowWorldY(
                worldPolygon,
                midY,
                clippedPolygon);

            float filledArea =
                PolygonArea(
                    clippedPolygon);

            float actualFill01 =
                Mathf.Clamp01(
                    filledArea /
                    totalArea);

            if (actualFill01 <
                targetWaterFill01)
            {
                lowY = midY;
            }
            else
            {
                highY = midY;
            }
        }

        surfaceWorldY =
            (lowY + highY) *
            0.5f;

        return true;
    }

    public static bool TryBuildWorldPolygon(
        Collider2D chamberCollider,
        List<Vector2> result)
    {
        if (result == null)
            return false;

        result.Clear();

        if (chamberCollider is BoxCollider2D box)
        {
            Vector2 center =
                box.offset;

            Vector2 half =
                box.size *
                0.5f;

            AddWorldPoint(
                result,
                box.transform,
                center + new Vector2(-half.x, half.y));

            AddWorldPoint(
                result,
                box.transform,
                center + new Vector2(half.x, half.y));

            AddWorldPoint(
                result,
                box.transform,
                center + new Vector2(half.x, -half.y));

            AddWorldPoint(
                result,
                box.transform,
                center + new Vector2(-half.x, -half.y));

            return true;
        }

        if (chamberCollider is PolygonCollider2D polygon)
        {
            if (polygon.pathCount <= 0)
                return false;

            Vector2[] path =
                polygon.GetPath(0);

            if (path == null ||
                path.Length < 3)
            {
                return false;
            }

            for (int i = 0;
                 i < path.Length;
                 i++)
            {
                AddWorldPoint(
                    result,
                    polygon.transform,
                    path[i] +
                    polygon.offset);
            }

            return true;
        }

        return false;
    }

    public static void ClipBelowWorldY(
        List<Vector2> input,
        float waterSurfaceWorldY,
        List<Vector2> output)
    {
        if (output == null)
            return;

        output.Clear();

        if (input == null ||
            input.Count < 3)
        {
            return;
        }

        Vector2 previous =
            input[input.Count - 1];

        bool previousInside =
            previous.y <=
            waterSurfaceWorldY;

        for (int i = 0;
             i < input.Count;
             i++)
        {
            Vector2 current =
                input[i];

            bool currentInside =
                current.y <=
                waterSurfaceWorldY;

            if (currentInside !=
                previousInside)
            {
                float dy =
                    current.y -
                    previous.y;

                float t =
                    Mathf.Abs(dy) <= 0.000001f
                        ? 0f
                        : (waterSurfaceWorldY - previous.y) / dy;

                output.Add(
                    Vector2.Lerp(
                        previous,
                        current,
                        Mathf.Clamp01(t)));
            }

            if (currentInside)
            {
                output.Add(
                    current);
            }

            previous =
                current;

            previousInside =
                currentInside;
        }
    }

    public static float PolygonArea(
        List<Vector2> polygon)
    {
        if (polygon == null ||
            polygon.Count < 3)
        {
            return 0f;
        }

        float twiceSignedArea =
            0f;

        for (int i = 0;
             i < polygon.Count;
             i++)
        {
            Vector2 a =
                polygon[i];

            Vector2 b =
                polygon[
                    (i + 1) %
                    polygon.Count];

            twiceSignedArea +=
                a.x * b.y -
                b.x * a.y;
        }

        return
            Mathf.Abs(
                twiceSignedArea) *
            0.5f;
    }

    private static void AddWorldPoint(
        List<Vector2> points,
        Transform sourceTransform,
        Vector2 localPoint)
    {
        points.Add(
            sourceTransform.TransformPoint(
                localPoint));
    }
}
