using System.Collections.Generic;
using UnityEngine;

public static class CelestialFieldQuery
{
    public static void Query(
        CelestialField field,
        Rect worldArea,
        List<CelestialObject> results,
        bool clearResults = true)
    {
        if (results == null)
            return;

        if (clearResults)
            results.Clear();

        if (field == null || !field.IsValid)
            return;

        var topology = new WorldTopology(field.WorldBounds);
        float radius = Mathf.Max(field.Config.nebulaRadiusWorldMax, field.Config.deepSkyRadiusWorldMax);
        if (worldArea.yMax + radius < field.WorldBounds.yMin || worldArea.yMin - radius > field.WorldBounds.yMax)
            return;
        Rect clippedArea = Rect.MinMaxRect(field.WorldBounds.xMin,
            Mathf.Max(field.WorldBounds.yMin, worldArea.yMin - radius), field.WorldBounds.xMax,
            Mathf.Min(field.WorldBounds.yMax, worldArea.yMax + radius));
        float cellSize = field.CellSizeWorld;

        int minCellX = Mathf.Clamp(
            Mathf.FloorToInt((clippedArea.xMin - field.WorldBounds.xMin) / cellSize),
            0,
            field.CellCountX - 1);

        int maxCellX = Mathf.Clamp(
            Mathf.FloorToInt((clippedArea.xMax - field.WorldBounds.xMin) / cellSize),
            0,
            field.CellCountX - 1);

        int minCellY = Mathf.Clamp(
            Mathf.FloorToInt((clippedArea.yMin - field.WorldBounds.yMin) / cellSize),
            0,
            field.CellCountY - 1);

        int maxCellY = Mathf.Clamp(
            Mathf.FloorToInt((clippedArea.yMax - field.WorldBounds.yMin) / cellSize),
            0,
            field.CellCountY - 1);

        float maxExtendedRadius = Mathf.Max(
            field.Config.nebulaRadiusWorldMax,
            field.Config.deepSkyRadiusWorldMax);

        int expansionCells = Mathf.CeilToInt(maxExtendedRadius / Mathf.Max(0.0001f, cellSize));

        minCellX = Mathf.Max(0, minCellX - expansionCells);
        maxCellX = Mathf.Min(field.CellCountX - 1, maxCellX + expansionCells);
        minCellY = Mathf.Max(0, minCellY - expansionCells);
        maxCellY = Mathf.Min(field.CellCountY - 1, maxCellY + expansionCells);

        var cellObjects = new List<CelestialObject>();

        for (int y = minCellY; y <= maxCellY; y++)
        {
            for (int x = minCellX; x <= maxCellX; x++)
            {
                // Check wrapped cell bounds before generating truth. Each stable cell is visited once.
                float cellLeft = field.WorldBounds.xMin + x * cellSize;
                float cellRight = Mathf.Min(cellLeft + cellSize, field.WorldBounds.xMax);
                float nearestCenter = topology.Nearest(new Vector2((cellLeft + cellRight) * .5f, 0f), worldArea.center).x;
                if (worldArea.width < field.WorldBounds.width &&
                    Mathf.Abs(nearestCenter - worldArea.center.x) > worldArea.width * .5f + (cellRight - cellLeft) * .5f + radius)
                    continue;
                field.GetCellObjects(x, y, cellObjects);

                for (int i = 0; i < cellObjects.Count; i++)
                {
                    CelestialObject celestialObject = cellObjects[i];
                    if (celestialObject != null && IntersectsWrapped(celestialObject, worldArea, topology))
                        results.Add(celestialObject);
                }
            }
        }
    }

    private static bool IntersectsWrapped(CelestialObject obj, Rect area, WorldTopology topology)
    {
        Vector2 point = topology.Nearest(obj.WorldPosition, area.center);
        float dx = area.width >= topology.Bounds.width ? 0f : point.x - Mathf.Clamp(point.x, area.xMin, area.xMax);
        float dy = point.y - Mathf.Clamp(point.y, area.yMin, area.yMax);
        return dx * dx + dy * dy <= obj.FootprintRadiusWorld * obj.FootprintRadiusWorld;
    }

    private static bool TryIntersect(Rect a, Rect b, out Rect intersection)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);

        if (xMax < xMin || yMax < yMin)
        {
            intersection = default;
            return false;
        }

        intersection = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return intersection.width >= 0f && intersection.height >= 0f;
    }
}
