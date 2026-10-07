using UnityEngine;

public sealed partial class WorldMapKnowledgeSource
{
    [Header("Surface Survey Work")]
    [SerializeField] private SurfaceSurveySettings surfaceSurveySettings = new();
    public SurfaceSurveySettings SurfaceSurveySettings => surfaceSurveySettings ??= new();

    public bool TryGetSurfaceSurveyWorld(out WorldMapTopographyField field, out float sea)
    {
        AutoWire(); EnsureInitialized();
        field = topographySource?.Field;
        sea = topographySource != null ? topographySource.EffectiveSeaLevel01 : 0;
        if (field == null || !field.IsValid)
        {
            var cache = WorldMapRuntimeCache.I;
            field = cache?.Field; sea = cache != null ? cache.EffectiveSeaLevel01 : 0;
        }
        return field != null && field.IsValid && HasState && State.WorldBounds == field.WorldBounds &&
            HarborTravelService.CurrentGraph?.seed == field.Seed;
    }
}
