using System.Collections.Generic;
using UnityEngine;

public static partial class CelestialFieldGenerator
{
    private const int VoidAmbientChannel = 1701;
    private const int VoidThinningChannel = 1711;

    /// <summary>Presentation-only ambient continuation. Known-world generation/identity is untouched.</summary>
    public static void AppendVoidAmbientStars(CelestialField field, Rect query, float fadeDistance,
        List<CelestialObject> results)
    {
        if (field == null || !field.IsValid || results == null || fadeDistance <= 0f ||
            field.Config.ambientStarsPer1000WorldArea <= 0f) return;
        Rect bounds = field.WorldBounds;
        float left = query.xMin;
        float right = query.xMax;
        float bottom = Mathf.Max(query.yMin, bounds.yMin - fadeDistance);
        float top = Mathf.Min(query.yMax, bounds.yMax + fadeDistance);
        if (left >= right || bottom >= top) return;
        float size = field.CellSizeWorld;
        int minX = Mathf.FloorToInt((left - bounds.xMin) / size);
        int maxX = Mathf.FloorToInt((right - bounds.xMin) / size);
        int minY = Mathf.FloorToInt((bottom - bounds.yMin) / size);
        int maxY = Mathf.FloorToInt((top - bounds.yMin) / size);
        var candidates = new List<CelestialObject>();
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            Rect cell = new Rect(bounds.xMin + x * size, bounds.yMin + y * size, size, size);
            if (cell.xMin >= bounds.xMin && cell.xMax <= bounds.xMax &&
                cell.yMin >= bounds.yMin && cell.yMax <= bounds.yMax) continue;
            candidates.Clear();
            GenerateKind(field, x, y, cell, size * size, CelestialObjectKind.AmbientStar,
                VoidAmbientChannel, field.Config.ambientStarsPer1000WorldArea,
                field.Config.ambientBrightnessMin, field.Config.ambientBrightnessMax,
                field.Config.ambientProminenceMin, field.Config.ambientProminenceMax,
                field.Config.ambientVisualVariantCount, 0f, 0f, candidates);
            foreach (CelestialObject candidate in candidates)
            {
                if (candidate.WorldPosition.y >= bounds.yMin && candidate.WorldPosition.y <= bounds.yMax || !query.Contains(candidate.WorldPosition)) continue;
                float weight = GetVoidAmbientWeight(bounds, candidate.WorldPosition, fadeDistance);
                if (weight <= 0f) continue;
                var rng = CreateObjectRng(field, x, y, VoidThinningChannel, candidate.LocalIndex);
                if (rng.Next01() >= weight) continue;
                // Separate namespace: never a navigational/knowledge subject, never overlaps real star IDs.
                string id = $"voidAmbient:1:{unchecked((uint)field.WorldSeed):X8}:{x}:{y}:{candidate.LocalIndex}";
                results.Add(new CelestialObject(id, CelestialObjectKind.AmbientStar,
                    candidate.WorldPosition, candidate.Brightness01, candidate.Prominence01,
                    candidate.ColorClass, candidate.VisualVariant, candidate.RotationDegrees,
                    0f, x, y, candidate.LocalIndex));
            }
        }
    }

    public static float GetVoidAmbientWeight(Rect bounds, Vector2 position, float fadeDistance)
    {
        if (fadeDistance <= 0f) return 0f;
        Vector2 nearest = new Vector2(position.x,
            Mathf.Clamp(position.y, bounds.yMin, bounds.yMax));
        float distance = Vector2.Distance(position, nearest);
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / fadeDistance));
    }
}
