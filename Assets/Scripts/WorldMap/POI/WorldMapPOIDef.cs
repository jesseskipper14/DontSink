using System;
using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/POIs/POI Definition",
    fileName = "POI_")]
public sealed class WorldMapPOIDef : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable content ID. Use lowercase snake_case. This is what generated POI instances and saves reference.")]
    public string poiId = "underwater_ruins";

    public string displayName = "Underwater Ruins";

    [Tooltip("Optional deterministic placeholder names for generated instances of this POI type.")]
    public string[] generatedNames =
    {
        "Drowned Bell",
        "Old Spire",
        "Sunken Court",
        "Salt Chapel",
        "Broken Plaza"
    };

    [Header("Map Visual")]
    public Color mapColor = new Color(0.75f, 0.55f, 1f, 0.95f);

    [Tooltip("Reserved for later UI. Current map view draws diamonds, not text glyphs.")]
    public string mapGlyph = "◆";

    [Header("Generation Count and Thresholds")]
    [Min(0)] public int targetCount = 6;

    [Tooltip("Reject candidates of this POI type below this score.")]
    public float minScore = 0.25f;

    [Tooltip("Minimum world-space distance between POIs using this same definition.")]
    [Min(0.1f)] public float minSameTypeSpacing = 24f;

    [Header("Hard Rules")]
    [Tooltip("Currently all generated POIs are underwater, but keep this here for explicit content rules/future validation.")]
    public bool mustBeUnderwater = true;

    [Tooltip("Minimum depth below sea level, normalized height units.")]
    [Range(0f, 1f)] public float minDepth01 = 0.005f;

    [Tooltip("Maximum depth below sea level, normalized height units.")]
    [Range(0f, 1f)] public float maxDepth01 = 1f;

    [Header("Base Score")]
    public float baseScore = 0f;

    [Header("Classification Weights")]
    public float deepOceanWeight;
    public float openOceanWeight;
    public float shelfWaterWeight;
    public float shallowWaterWeight;

    [Tooltip("Nearby beach samples. Since POI centers are underwater, this means adjacent/nearby land, not center terrain.")]
    public float beachNearbyWeight;

    public float lowlandNearbyWeight;
    public float highlandNearbyWeight;
    public float mountainNearbyWeight;

    [Header("Aggregate Terrain Weights")]
    public float waterWeight;
    public float landNearbyWeight;
    public float shallowShelfWeight;
    public float deepOpenWeight;
    public float coastPresenceWeight;
    public float ruggednessWeight;

    [Tooltip("Positive values prefer the candidate center to be a local low within the sampled area.")]
    public float localLowWeight;

    [Tooltip("Positive values prefer the candidate center to be a local high within the sampled underwater area.")]
    public float localHighWeight;

    [Tooltip("Positive values prefer low land presence nearby, useful for isolated deep relics.")]
    public float farFromLandWeight;

    [Tooltip("Positive values prefer low coast presence nearby, useful for isolated/open-ocean POIs.")]
    public float isolationWeight;

    [Header("Depth Preference")]
    [Range(0f, 1f)] public float preferredDepth01 = 0.15f;

    [Range(0.001f, 1f)] public float depthTolerance01 = 0.20f;

    public float depthPreferenceWeight = 0.75f;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(poiId))
            poiId = name;

        poiId = MakeSafeId(poiId);

        targetCount = Mathf.Max(0, targetCount);
        minSameTypeSpacing = Mathf.Max(0.1f, minSameTypeSpacing);

        maxDepth01 = Mathf.Max(minDepth01, maxDepth01);
        depthTolerance01 = Mathf.Max(0.001f, depthTolerance01);
    }

    private static string MakeSafeId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "poi";

        raw = raw.Trim().ToLowerInvariant();

        var chars = raw.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool ok =
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '_';

            chars[i] = ok ? c : '_';
        }

        return new string(chars);
    }
}
