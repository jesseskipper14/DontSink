using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class LocalIslandChartLimits
{
    [Range(16, 1024)] public int analysisResolution = 512;
    [Min(0f)] public float offshoreBuffer = 4f;
    [Min(.1f)] public float landAssociationRadius = 16f;
    [Min(.1f)] public float maxLandArea = 5000f;
    [Min(.1f)] public float maxLandExtent = 150f;
    [Min(.1f)] public float maxRevealRadius = 100f;
    [Min(.1f)] public float fallbackRadius = 28f;
    [Min(.1f)] public float fallbackAreaBudget = 2000f;
    public bool IsValid => analysisResolution >= 16 && analysisResolution <= 1024 &&
        FinitePositive(landAssociationRadius) && FinitePositive(maxLandArea) && FinitePositive(maxLandExtent) &&
        FinitePositive(maxRevealRadius) && FinitePositive(fallbackRadius) && FinitePositive(fallbackAreaBudget) &&
        WorldTopology.IsFinite(offshoreBuffer) && offshoreBuffer >= 0f;
    private static bool FinitePositive(float value) => WorldTopology.IsFinite(value) && value > 0f;
}

public sealed class LocalIslandChartResult
{
    public WorldMapCoverageMask Coverage { get; internal set; }
    public bool UsedFallback { get; internal set; }
    public float LandArea { get; internal set; }
    public float CoverageArea { get; internal set; }
}

/// <summary>Reconstructible surface-only chart. Four-connected land, wrapping X, finite Y.
/// Offshore dilation crosses water only; it cannot grant a neighboring island.</summary>
public static class LocalIslandChartBuilder
{
    public static bool TryBuild(WorldMapTopographyField field, float seaLevel, Vector2 nodePosition,
        LocalIslandChartLimits limits, out LocalIslandChartResult result, out string reason)
    {
        result = null; reason = null;
        if (field == null || !field.IsValid || !new WorldTopology(field.WorldBounds).IsValid ||
            limits == null || !limits.IsValid || !WorldTopology.IsFinite(seaLevel) || seaLevel <= 0f || seaLevel >= 1f ||
            !WorldTopology.IsFinite(nodePosition.x) || !WorldTopology.IsFinite(nodePosition.y))
        { reason = "Invalid local-chart topography, registration or limits."; return false; }
        var topology = new WorldTopology(field.WorldBounds);
        if (!topology.ContainsY(nodePosition.y)) { reason = "The node is outside the world's finite latitude range."; return false; }
        nodePosition = topology.Normalize(nodePosition);
        int w = Mathf.Min(field.Width, limits.analysisResolution), h = Mathf.Min(field.Height, limits.analysisResolution);
        var bounds = field.WorldBounds;
        float dx = bounds.width / w, dy = bounds.height / h, cellArea = dx * dy;
        int count = w * h;
        Vector2 Position(int index) => new Vector2(bounds.xMin + (index % w + .5f) * dx, bounds.yMin + (index / w + .5f) * dy);
        int Neighbor(int index, int ox, int oy)
        {
            int y = index / w + oy;
            return y < 0 || y >= h ? -1 : y * w + WorldTopology.WrapIndex(index % w + ox, w);
        }
        var land = new bool[count];
        int seed = -1;
        float nearest = limits.landAssociationRadius * limits.landAssociationRadius;
        for (int i = 0; i < count; i++)
        {
            land[i] = field.Sample01World(Position(i)) >= seaLevel;
            float distance = topology.Delta(nodePosition, Position(i)).sqrMagnitude;
            if (land[i] && distance <= nearest) { nearest = distance; seed = i; }
        }
        if (seed < 0) { reason = "No island is associated with this node within the configured search distance."; return false; }
        var component = new bool[count];
        var queue = new Queue<int>();
        int cells = 0;
        bool fallback = false;
        var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        void Enqueue(int index, float radius)
        {
            if (index < 0 || component[index] || !land[index] ||
                topology.Delta(nodePosition, Position(index)).sqrMagnitude > radius * radius) return;
            component[index] = true; queue.Enqueue(index);
        }
        component[seed] = true; queue.Enqueue(seed);
        while (queue.Count > 0)
        {
            int index = queue.Dequeue(); cells++;
            var delta = topology.Delta(nodePosition, Position(index));
            minimum = Vector2.Min(minimum, delta); maximum = Vector2.Max(maximum, delta);
            if (cells * cellArea > limits.maxLandArea ||
                Mathf.Max(maximum.x - minimum.x, maximum.y - minimum.y) > limits.maxLandExtent ||
                delta.magnitude + limits.offshoreBuffer > limits.maxRevealRadius)
            { fallback = true; break; }
            // No radius filtering in the complete-island search: crossing a limit triggers fallback.
            Enqueue(Neighbor(index, -1, 0), float.PositiveInfinity); Enqueue(Neighbor(index, 1, 0), float.PositiveInfinity);
            Enqueue(Neighbor(index, 0, -1), float.PositiveInfinity); Enqueue(Neighbor(index, 0, 1), float.PositiveInfinity);
        }
        float radiusCap = limits.maxRevealRadius;
        if (fallback)
        {
            Array.Clear(component, 0, count); queue.Clear(); cells = 0;
            radiusCap = Mathf.Min(limits.fallbackRadius, limits.maxRevealRadius);
            // Reserve room for offshore water; a fallback never expands to other connected continents.
            int landBudget = Mathf.FloorToInt(limits.fallbackAreaBudget * .6f / cellArea);
            Enqueue(seed, radiusCap);
            var accepted = new bool[count];
            while (queue.Count > 0 && cells < landBudget)
            {
                int index = queue.Dequeue(); accepted[index] = true; cells++;
                Enqueue(Neighbor(index, -1, 0), radiusCap); Enqueue(Neighbor(index, 1, 0), radiusCap);
                Enqueue(Neighbor(index, 0, -1), radiusCap); Enqueue(Neighbor(index, 0, 1), radiusCap);
            }
            component = accepted;
            if (cells == 0) { reason = "The local fallback cannot fit land in its radius/area budget."; return false; }
        }
        var coverage = (bool[])component.Clone();
        int coverageCells = cells;
        var distanceToCoast = new float[count];
        for (int i = 0; i < count; i++) distanceToCoast[i] = float.PositiveInfinity;
        var frontier = new SortedSet<(float distance, int index)>(Comparer<(float distance, int index)>.Create((a, b) =>
        { int compare = a.distance.CompareTo(b.distance); return compare != 0 ? compare : a.index.CompareTo(b.index); }));
        for (int i = 0; i < count; i++)
        {
            if (!component[i]) continue;
            // Only coast seeds need to enter the distance frontier.
            bool coast = false;
            for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
            { int next = Neighbor(i, ox, oy); if (next >= 0 && !land[next]) coast = true; }
            if (coast) { distanceToCoast[i] = 0f; frontier.Add((0f, i)); }
        }
        while (frontier.Count > 0)
        {
            var entry = frontier.Min; frontier.Remove(entry);
            if (entry.distance != distanceToCoast[entry.index]) continue;
            if (!component[entry.index])
            {
                if (fallback && (coverageCells + 1) * cellArea > limits.fallbackAreaBudget) break;
                coverage[entry.index] = true; coverageCells++;
            }
            for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int next = Neighbor(entry.index, ox, oy);
                if (next < 0 || land[next] || topology.Delta(nodePosition, Position(next)).sqrMagnitude > radiusCap * radiusCap) continue;
                float distance = entry.distance + Mathf.Sqrt(ox * ox * dx * dx + oy * oy * dy * dy);
                if (distance > limits.offshoreBuffer || distance >= distanceToCoast[next]) continue;
                distanceToCoast[next] = distance; frontier.Add((distance, next));
            }
        }
        result = new LocalIslandChartResult {
            Coverage = new WorldMapCoverageMask { width = w, height = h, worldBounds = bounds, cells = coverage },
            UsedFallback = fallback, LandArea = cells * cellArea, CoverageArea = coverageCells * cellArea
        };
        return true;
    }
}
