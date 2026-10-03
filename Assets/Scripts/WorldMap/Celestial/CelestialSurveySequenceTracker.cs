using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared survey-sequence facade. Phase 5 persists normal game progression in GameState.
/// A tiny in-memory fallback remains only for isolated debug/test scenes without GameState.
/// </summary>
public static class CelestialSurveySequenceTracker
{
    private static readonly Dictionary<string, int> DebugFallbackSequenceByRegion = new();

    public static int GetCurrentSequence(CelestialField field, Vector2 worldPosition, float regionSizeWorld)
    {
        string key = BuildRegionKey(field, worldPosition, regionSizeWorld);

        if (GameState.I != null)
        {
            GameState.I.EnsureCelestialChartDefaults();
            return GameState.I.celestialCharts.GetSurveySequence(key);
        }

        return DebugFallbackSequenceByRegion.TryGetValue(key, out int sequence)
            ? Mathf.Max(0, sequence)
            : 0;
    }

    /// <summary>
    /// Compatibility/debug path. Normal player-chart commits should advance progression through
    /// CelestialChartingAuthority so paper consumption + fragment creation + sequence advancement
    /// remain one authoritative transaction.
    /// </summary>
    public static void MarkSuccessful(CelestialField field, Vector2 worldPosition, float regionSizeWorld, int completedSequence)
    {
        string key = BuildRegionKey(field, worldPosition, regionSizeWorld);

        if (GameState.I != null)
        {
            if (!GameplayAuthority.IsAuthoritative)
                return;

            GameState.I.EnsureCelestialChartDefaults();
            GameState.I.celestialCharts.TryAdvanceSurveySequence(key, completedSequence);
            return;
        }

        int next = Mathf.Max(0, completedSequence) + 1;
        if (!DebugFallbackSequenceByRegion.TryGetValue(key, out int current) || next > current)
            DebugFallbackSequenceByRegion[key] = next;
    }

    public static void ClearRuntimeProgress()
    {
        DebugFallbackSequenceByRegion.Clear();
    }

    public static string BuildRegionKey(CelestialField field, Vector2 worldPosition, float regionSizeWorld)
    {
        float size = Mathf.Max(0.5f, regionSizeWorld);
        if (field != null) worldPosition = new WorldTopology(field.WorldBounds).Normalize(worldPosition);
        int x = Mathf.FloorToInt(worldPosition.x / size);
        int y = Mathf.FloorToInt(worldPosition.y / size);
        int seed = field != null ? field.WorldSeed : 0;
        int version = field != null && field.Identity != null ? field.Identity.generatorVersion : 0;
        string hash = field != null && field.Identity != null ? field.Identity.configHash : "none";
        return $"{seed}:{version}:{hash}:{x}:{y}";
    }
}
