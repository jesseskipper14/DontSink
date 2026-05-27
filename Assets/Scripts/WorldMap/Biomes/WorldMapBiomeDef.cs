using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/Biomes/Biome Def",
    fileName = "Biome_")]
public sealed class WorldMapBiomeDef : ScriptableObject
{
    [Header("Identity")]
    public string biomeId = "coral_shelf";
    public string displayName = "Coral Shelf";

    [Header("Debug Visual")]
    public Color debugColor = new Color(0.15f, 0.85f, 0.8f, 1f);

    [Header("Base Score")]
    public float baseScore = 0f;

    [Header("Classification Weights")]
    public float deepOceanWeight;
    public float openOceanWeight;
    public float shelfWaterWeight;
    public float shallowWaterWeight;
    public float beachWeight;
    public float lowlandWeight;
    public float highlandWeight;
    public float mountainWeight;

    [Header("Regional Preferences")]
    [Range(0f, 1f)] public float preferredLand01 = 0.1f;
    [Range(0.001f, 1f)] public float landTolerance01 = 0.2f;
    public float landPreferenceWeight = 0f;

    [Range(0f, 1f)] public float preferredShallowShelf01 = 0.5f;
    [Range(0.001f, 1f)] public float shallowShelfTolerance01 = 0.25f;
    public float shallowShelfPreferenceWeight = 0f;

    [Range(0f, 1f)] public float preferredDeepOpen01 = 0.75f;
    [Range(0.001f, 1f)] public float deepOpenTolerance01 = 0.25f;
    public float deepOpenPreferenceWeight = 0f;

    [Range(0f, 1f)] public float preferredCoastPresence01 = 0.5f;
    [Range(0.001f, 1f)] public float coastTolerance01 = 0.3f;
    public float coastPreferenceWeight = 0f;

    [Range(0f, 1f)] public float preferredRuggedness01 = 0.35f;
    [Range(0.001f, 1f)] public float ruggednessTolerance01 = 0.3f;
    public float ruggednessPreferenceWeight = 0f;

    public float Score(WorldMapBiomeMetrics m, float deterministicNoise)
    {
        float score = baseScore;

        score += m.DeepOcean01 * deepOceanWeight;
        score += m.OpenOcean01 * openOceanWeight;
        score += m.ShelfWater01 * shelfWaterWeight;
        score += m.ShallowWater01 * shallowWaterWeight;
        score += m.Beach01 * beachWeight;
        score += m.Lowland01 * lowlandWeight;
        score += m.Highland01 * highlandWeight;
        score += m.Mountain01 * mountainWeight;

        score += PreferenceScore(m.Land01, preferredLand01, landTolerance01, landPreferenceWeight);
        score += PreferenceScore(m.ShallowShelf01, preferredShallowShelf01, shallowShelfTolerance01, shallowShelfPreferenceWeight);
        score += PreferenceScore(m.DeepOpen01, preferredDeepOpen01, deepOpenTolerance01, deepOpenPreferenceWeight);
        score += PreferenceScore(m.CoastPresence01, preferredCoastPresence01, coastTolerance01, coastPreferenceWeight);
        score += PreferenceScore(m.Ruggedness01, preferredRuggedness01, ruggednessTolerance01, ruggednessPreferenceWeight);

        score += deterministicNoise;

        return score;
    }

    private static float PreferenceScore(float value, float preferred, float tolerance, float weight)
    {
        if (Mathf.Abs(weight) <= 0.0001f)
            return 0f;

        tolerance = Mathf.Max(0.0001f, tolerance);

        float d = Mathf.Abs(value - preferred);
        float t = 1f - Mathf.Clamp01(d / tolerance);

        return t * weight;
    }
}