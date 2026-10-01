using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/Celestial/Celestial Sky Projection Settings",
    fileName = "CelestialSkyProjectionSettings")]
public sealed class CelestialSkyProjectionSettings : ScriptableObject
{
    [Header("Celestial View Window")]
    [Tooltip("Fraction of the full celestial/world-map width visible across the scene sky. Smaller values make stars move farther for the same world travel distance.")]
    [Range(0.001f, 1f)]
    public float horizontalWorldFraction = 0.10f;

    [Tooltip("Fraction of the full celestial/world-map height queried around the observer. This now controls sky/survey coverage only; display geometry uses one shared X/Y scale so patterns are not stretched.")]
    [Range(0.001f, 1f)]
    public float verticalWorldFraction = 0.10f;

    [Tooltip("Extra world-space area queried around the visible window. This prevents objects popping in while the observer moves between query refreshes.")]
    [Range(0f, 1f)]
    public float queryPaddingFraction = 0.30f;

    [Tooltip("Refresh the queried celestial objects after the observer moves this fraction of the smaller visible world dimension.")]
    [Range(0.005f, 0.5f)]
    public float queryRefreshDistanceFraction = 0.08f;

    [Tooltip("Maximum seconds between query refreshes while the sky is visible, even if the observer barely moves.")]
    [Min(0.05f)]
    public float maximumQueryRefreshSeconds = 1.5f;

    [Header("Projection")]
    [Tooltip("Legacy-named lower/upper sky-band endpoint retained for serialized assets. After the north-up cleanup, the min/max of this and South Zenith define the scene sky presentation band; north is no longer forced to the lower value.")]
    [Range(-0.25f, 1.25f)]
    public float northHorizonViewportY = 0.22f;

    [Tooltip("Legacy-named lower/upper sky-band endpoint retained for serialized assets. After the north-up cleanup, the min/max of this and North Horizon define the scene sky presentation band. Values above 1 still permit natural offscreen continuation.")]
    [Range(-0.25f, 1.50f)]
    public float southZenithViewportY = 1.04f;

    [Tooltip("Legacy serialized field retained for compatibility. Shape-preserving projection is now linear; this curve is intentionally ignored.")]
    [HideInInspector]
    public AnimationCurve northToSouthProjection = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    public float SkyViewportMinY => Mathf.Min(northHorizonViewportY, southZenithViewportY);
    public float SkyViewportMaxY => Mathf.Max(northHorizonViewportY, southZenithViewportY);
    public float SkyViewportCenterY => (northHorizonViewportY + southZenithViewportY) * 0.5f;

    [Tooltip("How far beyond the left/right camera edges objects may remain active before being culled.")]
    [Range(0f, 0.25f)]
    public float horizontalCullMarginViewport = 0.04f;

    [Tooltip("How far beyond the vertical projection band objects may remain active before being culled.")]
    [Range(0f, 0.25f)]
    public float verticalCullMarginViewport = 0.05f;

    [Header("Visible Kinds")]
    public bool showAmbientStars = true;
    public bool showLandmarkStars = true;
    public bool showNebulae = true;
    public bool showDeepSkyObjects = true;

    [Header("Ambient Stars - screen pixels")]
    [Min(0.25f)] public float ambientSizePixelsMin = 1.0f;
    [Min(0.25f)] public float ambientSizePixelsMax = 2.6f;
    [Range(0f, 1f)] public float ambientAlpha = 0.86f;
    [Range(0f, 1f)] public float ambientColorSaturation = 0.32f;

    [Header("Landmark Stars - screen pixels")]
    [Min(1f)] public float landmarkSizePixelsMin = 5f;
    [Min(1f)] public float landmarkSizePixelsMax = 14f;
    [Range(0f, 1f)] public float landmarkAlpha = 1f;
    [Range(0f, 1f)] public float landmarkColorSaturation = 0.94f;

    [Header("Star Luminance / Twinkle")]
    [Tooltip("Brightness multiplier for ambient stars using the unlit additive star material.")]
    [Min(0.1f)] public float ambientGlowStrength = 1.35f;

    [Tooltip("Brightness multiplier for landmark stars using the unlit additive star material.")]
    [Min(0.1f)] public float landmarkGlowStrength = 1.85f;

    [Tooltip("Fractional brightness variation for ambient-star twinkle.")]
    [Range(0f, 0.75f)] public float ambientTwinkleStrength = 0.16f;

    [Tooltip("Fractional brightness variation for landmark-star twinkle.")]
    [Range(0f, 0.75f)] public float landmarkTwinkleStrength = 0.28f;

    [Tooltip("Slowest deterministic twinkle speed assigned to stars.")]
    [Min(0.01f)] public float twinkleSpeedMin = 0.65f;

    [Tooltip("Fastest deterministic twinkle speed assigned to stars.")]
    [Min(0.01f)] public float twinkleSpeedMax = 2.10f;

    [Header("Nebulae")]
    [Tooltip("Multiplier applied after projecting a nebula's deterministic world-space footprint into screen size.")]
    [Min(0.01f)] public float nebulaSizeMultiplier = 1f;
    [Min(2f)] public float nebulaMinimumSizePixels = 18f;
    [Min(2f)] public float nebulaMaximumSizePixels = 260f;
    [Range(0f, 1f)] public float nebulaAlpha = 0.32f;
    [Range(0f, 1f)] public float nebulaColorSaturation = 0.68f;

    [Header("Deep Sky Objects")]
    [Tooltip("Multiplier applied after projecting a deep-sky object's deterministic world-space footprint into screen size.")]
    [Min(0.01f)] public float deepSkySizeMultiplier = 1f;
    [Min(2f)] public float deepSkyMinimumSizePixels = 8f;
    [Min(2f)] public float deepSkyMaximumSizePixels = 96f;
    [Range(0f, 1f)] public float deepSkyAlpha = 0.88f;
    [Range(0f, 1f)] public float deepSkyColorSaturation = 0.86f;

    [Header("Color Classes")]
    public Color white = new Color(0.94f, 0.97f, 1f, 1f);
    public Color blueWhite = new Color(0.60f, 0.84f, 1f, 1f);
    public Color gold = new Color(1f, 0.82f, 0.30f, 1f);
    public Color orange = new Color(1f, 0.47f, 0.16f, 1f);
    public Color red = new Color(1f, 0.26f, 0.30f, 1f);
    public Color cyan = new Color(0.20f, 0.95f, 1f, 1f);
    public Color violet = new Color(0.70f, 0.40f, 1f, 1f);

    [Header("Legacy Sky Takeover")]
    [Tooltip("If enabled, the renderer disables SceneContext.starsRenderer after the procedural celestial sky is ready. It restores it when this component disables/destroys.")]
    public bool disableLegacyStarsRenderer = true;

    [Header("Fallback Sorting")]
    [Tooltip("Used only when no legacy stars SpriteRenderer is available to copy sorting from.")]
    public string fallbackSortingLayerName = "Default";
    public int fallbackSortingOrder = -100;

    private void OnValidate()
    {
        horizontalWorldFraction = Mathf.Clamp(horizontalWorldFraction, 0.001f, 1f);
        verticalWorldFraction = Mathf.Clamp(verticalWorldFraction, 0.001f, 1f);
        queryPaddingFraction = Mathf.Clamp01(queryPaddingFraction);
        queryRefreshDistanceFraction = Mathf.Clamp(queryRefreshDistanceFraction, 0.005f, 0.5f);
        maximumQueryRefreshSeconds = Mathf.Max(0.05f, maximumQueryRefreshSeconds);

        ambientSizePixelsMin = Mathf.Max(0.25f, ambientSizePixelsMin);
        ambientSizePixelsMax = Mathf.Max(ambientSizePixelsMin, ambientSizePixelsMax);
        landmarkSizePixelsMin = Mathf.Max(1f, landmarkSizePixelsMin);
        landmarkSizePixelsMax = Mathf.Max(landmarkSizePixelsMin, landmarkSizePixelsMax);

        ambientGlowStrength = Mathf.Max(0.1f, ambientGlowStrength);
        landmarkGlowStrength = Mathf.Max(0.1f, landmarkGlowStrength);
        ambientTwinkleStrength = Mathf.Clamp(ambientTwinkleStrength, 0f, 0.75f);
        landmarkTwinkleStrength = Mathf.Clamp(landmarkTwinkleStrength, 0f, 0.75f);
        twinkleSpeedMin = Mathf.Max(0.01f, twinkleSpeedMin);
        twinkleSpeedMax = Mathf.Max(twinkleSpeedMin, twinkleSpeedMax);

        nebulaSizeMultiplier = Mathf.Max(0.01f, nebulaSizeMultiplier);
        nebulaMinimumSizePixels = Mathf.Max(2f, nebulaMinimumSizePixels);
        nebulaMaximumSizePixels = Mathf.Max(nebulaMinimumSizePixels, nebulaMaximumSizePixels);

        deepSkySizeMultiplier = Mathf.Max(0.01f, deepSkySizeMultiplier);
        deepSkyMinimumSizePixels = Mathf.Max(2f, deepSkyMinimumSizePixels);
        deepSkyMaximumSizePixels = Mathf.Max(deepSkyMinimumSizePixels, deepSkyMaximumSizePixels);

        if (northToSouthProjection == null || northToSouthProjection.length < 2)
            northToSouthProjection = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    public Color ResolveColor(CelestialColorClass colorClass, float saturation01)
    {
        Color target;

        switch (colorClass)
        {
            case CelestialColorClass.BlueWhite: target = blueWhite; break;
            case CelestialColorClass.Gold: target = gold; break;
            case CelestialColorClass.Orange: target = orange; break;
            case CelestialColorClass.Red: target = red; break;
            case CelestialColorClass.Cyan: target = cyan; break;
            case CelestialColorClass.Violet: target = violet; break;
            default: target = white; break;
        }

        Color result = Color.Lerp(white, target, Mathf.Clamp01(saturation01));
        result.a = 1f;
        return result;
    }
}
