using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/Celestial/Celestial Generation Settings",
    fileName = "CelestialGenerationSettings")]
public sealed class CelestialGenerationSettings : ScriptableObject
{
    [Header("Generation Identity")]
    [Tooltip("Extra deterministic salt combined with the world seed. Change this only when intentionally changing the sky for newly generated worlds.")]
    public int generationSalt = 1640531527;

    [Header("Spatial Cells")]
    [Tooltip("Implementation cell size in world-map units. Cells are deterministic query buckets, not visible star-map regions.")]
    [Min(1f)]
    public float cellSizeWorld = 16f;

    [Header("Ambient Stars")]
    [Tooltip("Average ambient-star density per 1000 square world-map units.")]
    [Min(0f)]
    public float ambientStarsPer1000WorldArea = 65f;

    [Range(0f, 1f)] public float ambientBrightnessMin = 0.18f;
    [Range(0f, 1f)] public float ambientBrightnessMax = 0.72f;
    [Range(0f, 1f)] public float ambientProminenceMin = 0.08f;
    [Range(0f, 1f)] public float ambientProminenceMax = 0.34f;
    [Min(1)] public int ambientVisualVariantCount = 3;

    [Header("Landmark Stars")]
    [Tooltip("Average distinctive navigational-star density per 1000 square world-map units.")]
    [Min(0f)]
    public float landmarkStarsPer1000WorldArea = 2.2f;

    [Range(0f, 1f)] public float landmarkBrightnessMin = 0.72f;
    [Range(0f, 1f)] public float landmarkBrightnessMax = 1f;
    [Range(0f, 1f)] public float landmarkProminenceMin = 0.62f;
    [Range(0f, 1f)] public float landmarkProminenceMax = 1f;
    [Min(1)] public int landmarkVisualVariantCount = 6;

    [Header("Nebulae")]
    [Tooltip("Average nebula density per 1000 square world-map units.")]
    [Min(0f)]
    public float nebulaePer1000WorldArea = 0.18f;

    [Range(0f, 1f)] public float nebulaBrightnessMin = 0.28f;
    [Range(0f, 1f)] public float nebulaBrightnessMax = 0.78f;
    [Range(0f, 1f)] public float nebulaProminenceMin = 0.55f;
    [Range(0f, 1f)] public float nebulaProminenceMax = 1f;
    [Min(0.1f)] public float nebulaRadiusWorldMin = 5f;
    [Min(0.1f)] public float nebulaRadiusWorldMax = 15f;
    [Min(1)] public int nebulaVisualVariantCount = 4;

    [Header("Deep-Sky Objects")]
    [Tooltip("Average non-nebula deep-sky landmark density per 1000 square world-map units.")]
    [Min(0f)]
    public float deepSkyObjectsPer1000WorldArea = 0.10f;

    [Range(0f, 1f)] public float deepSkyBrightnessMin = 0.40f;
    [Range(0f, 1f)] public float deepSkyBrightnessMax = 0.92f;
    [Range(0f, 1f)] public float deepSkyProminenceMin = 0.58f;
    [Range(0f, 1f)] public float deepSkyProminenceMax = 1f;
    [Min(0.1f)] public float deepSkyRadiusWorldMin = 2f;
    [Min(0.1f)] public float deepSkyRadiusWorldMax = 8f;
    [Min(1)] public int deepSkyVisualVariantCount = 4;

    public CelestialGenerationConfig CreateConfigSnapshot()
    {
        return CelestialGenerationConfig.FromSettings(this);
    }

    private void OnValidate()
    {
        cellSizeWorld = Mathf.Max(1f, cellSizeWorld);

        ambientStarsPer1000WorldArea = Mathf.Max(0f, ambientStarsPer1000WorldArea);
        landmarkStarsPer1000WorldArea = Mathf.Max(0f, landmarkStarsPer1000WorldArea);
        nebulaePer1000WorldArea = Mathf.Max(0f, nebulaePer1000WorldArea);
        deepSkyObjectsPer1000WorldArea = Mathf.Max(0f, deepSkyObjectsPer1000WorldArea);

        NormalizeRange(ref ambientBrightnessMin, ref ambientBrightnessMax);
        NormalizeRange(ref ambientProminenceMin, ref ambientProminenceMax);
        NormalizeRange(ref landmarkBrightnessMin, ref landmarkBrightnessMax);
        NormalizeRange(ref landmarkProminenceMin, ref landmarkProminenceMax);
        NormalizeRange(ref nebulaBrightnessMin, ref nebulaBrightnessMax);
        NormalizeRange(ref nebulaProminenceMin, ref nebulaProminenceMax);
        NormalizeRange(ref deepSkyBrightnessMin, ref deepSkyBrightnessMax);
        NormalizeRange(ref deepSkyProminenceMin, ref deepSkyProminenceMax);

        ambientVisualVariantCount = Mathf.Max(1, ambientVisualVariantCount);
        landmarkVisualVariantCount = Mathf.Max(1, landmarkVisualVariantCount);
        nebulaVisualVariantCount = Mathf.Max(1, nebulaVisualVariantCount);
        deepSkyVisualVariantCount = Mathf.Max(1, deepSkyVisualVariantCount);

        nebulaRadiusWorldMin = Mathf.Max(0.1f, nebulaRadiusWorldMin);
        nebulaRadiusWorldMax = Mathf.Max(nebulaRadiusWorldMin, nebulaRadiusWorldMax);
        deepSkyRadiusWorldMin = Mathf.Max(0.1f, deepSkyRadiusWorldMin);
        deepSkyRadiusWorldMax = Mathf.Max(deepSkyRadiusWorldMin, deepSkyRadiusWorldMax);
    }

    private static void NormalizeRange(ref float min, ref float max)
    {
        min = Mathf.Clamp01(min);
        max = Mathf.Clamp01(max);

        if (max < min)
            max = min;
    }
}
