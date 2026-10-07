using System;
using UnityEngine;

/// <summary>A frozen physical bottom reading. No shared map grants until processing and integration.</summary>
[Serializable]
public sealed class SoundingChartEvidence
{
    public int version = 1;
    public Vector2 position;
    public float measuredDepthMeters;
    public SoundingChartEvidence Copy() => (SoundingChartEvidence)MemberwiseClone();
    public bool IsValid(Rect bounds) => version == 1 && new WorldTopology(bounds).IsValid &&
        WorldTopology.IsFinite(position.x) && WorldTopology.IsFinite(position.y) &&
        new WorldTopology(bounds).ContainsY(position.y) && WorldTopology.IsFinite(measuredDepthMeters) && measuredDepthMeters > 0;
}

public static class SoundingChartBuilder
{
    public const float DefaultRevealRadius = 28f;
    public static bool TryProcess(CartographicChartState evidence, string physicalItemId,
        WorldMapTopographyField field, WorldMapKnowledgeState knowledge, out CartographicChartState result,
        float revealRadius = DefaultRevealRadius)
    {
        result = null;
        if (evidence == null || evidence.version != 1 || evidence.kind != CartographicChartKind.SoundingEvidence ||
            evidence.sounding?.IsValid(evidence.worldBounds) != true || string.IsNullOrWhiteSpace(physicalItemId) ||
            field == null || !field.IsValid || knowledge?.IsValid != true || evidence.worldSeed != field.Seed ||
            evidence.topographyVersion != field.GenerationVersion || evidence.worldBounds != field.WorldBounds ||
            knowledge.WorldBounds != field.WorldBounds || !WorldTopology.IsFinite(revealRadius) || revealRadius <= 0) return false;
        // At least the measurement's grid cell is covered even on a coarse shared knowledge grid.
        float radius = Mathf.Max(revealRadius, new Vector2(field.WorldBounds.width / knowledge.Width,
            field.WorldBounds.height / knowledge.Height).magnitude * .51f);
        result = evidence.Copy();
        result.kind = CartographicChartKind.Georeferenced;
        result.title = "Processed sounding — seafloor chart";
        result.referenceText = $"Bottom reading: {evidence.sounding.measuredDepthMeters:0.0} m. Local seafloor coverage only; no surface geography or POI records.";
        result.payload = new WorldMapCartographicPayload { sourceId = "sounding:v1:" + physicalItemId,
            bathymetry = WorldMapCoverageMask.Circle(knowledge.Width, knowledge.Height, field.WorldBounds, evidence.sounding.position, radius) };
        return true;
    }

    // Quest generators may use this narrow predicate; arbitrary player readings never consult it.
    public static bool IsQuestCandidate(WorldMapKnowledgeState knowledge, WorldMapTopographyField field,
        float sea, Vector2 position) => knowledge?.IsValid == true && field?.IsValid == true &&
        knowledge.WorldBounds == field.WorldBounds && WorldTopology.IsFinite(sea) &&
        knowledge.IsRevealed(WorldMapKnowledgeLayer.Surface, position) &&
        !knowledge.IsRevealed(WorldMapKnowledgeLayer.UnderwaterSurvey, position) && field.Sample01World(position) < sea;
}
