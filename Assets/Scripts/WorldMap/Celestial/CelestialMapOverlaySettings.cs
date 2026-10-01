using UnityEngine;

/// <summary>
/// Presentation-only settings for the Phase 2 map overlay.
/// None of these values affect celestial world truth or stable IDs.
/// </summary>
[CreateAssetMenu(
    menuName = "WorldMap/Celestial/Celestial Map Overlay Settings",
    fileName = "CelestialMapOverlaySettings")]
public sealed class CelestialMapOverlaySettings : ScriptableObject
{
    [Header("Texture Resolution")]
    [Tooltip("Requested raster resolution in pixels per world-map unit before maxTextureDimension clamps it.")]
    [Min(0.25f)] public float pixelsPerWorldUnit = 2f;

    [Tooltip("Hard safety cap for either texture dimension.")]
    [Range(256, 8192)] public int maxTextureDimension = 4096;

    public FilterMode filterMode = FilterMode.Bilinear;

    [Header("Ambient Stars")]
    [Range(0f, 1f)] public float ambientAlpha = 0.72f;
    [Min(0.25f)] public float ambientRadiusPixelsMin = 0.55f;
    [Min(0.25f)] public float ambientRadiusPixelsMax = 1.35f;

    [Header("Landmark Stars")]
    [Range(0f, 1f)] public float landmarkAlpha = 0.96f;
    [Min(0.5f)] public float landmarkCoreRadiusPixelsMin = 1.6f;
    [Min(0.5f)] public float landmarkCoreRadiusPixelsMax = 3.2f;
    [Min(0.5f)] public float landmarkRayPixelsMin = 2.5f;
    [Min(0.5f)] public float landmarkRayPixelsMax = 6.5f;

    [Header("Nebulae")]
    [Range(0f, 1f)] public float nebulaAlpha = 0.28f;
    [Range(0.2f, 1f)] public float nebulaMinimumAspect = 0.48f;

    [Header("Deep-Sky Objects")]
    [Range(0f, 1f)] public float deepSkyAlpha = 0.88f;
    [Min(0.5f)] public float deepSkyMinimumRadiusPixels = 2.5f;
    [Min(0.5f)] public float deepSkyRingThicknessPixels = 1.25f;

    [Header("Celestial Color Classes")]
    public Color white = new Color(1f, 1f, 1f, 1f);
    public Color blueWhite = new Color(0.66f, 0.82f, 1f, 1f);
    public Color gold = new Color(1f, 0.84f, 0.42f, 1f);
    public Color orange = new Color(1f, 0.56f, 0.28f, 1f);
    public Color red = new Color(1f, 0.34f, 0.34f, 1f);
    public Color cyan = new Color(0.38f, 0.95f, 1f, 1f);
    public Color violet = new Color(0.76f, 0.50f, 1f, 1f);

    public Color GetColor(CelestialColorClass colorClass)
    {
        switch (colorClass)
        {
            case CelestialColorClass.BlueWhite: return blueWhite;
            case CelestialColorClass.Gold: return gold;
            case CelestialColorClass.Orange: return orange;
            case CelestialColorClass.Red: return red;
            case CelestialColorClass.Cyan: return cyan;
            case CelestialColorClass.Violet: return violet;
            default: return white;
        }
    }

    private void OnValidate()
    {
        pixelsPerWorldUnit = Mathf.Max(0.25f, pixelsPerWorldUnit);
        maxTextureDimension = Mathf.Clamp(maxTextureDimension, 256, 8192);

        ambientRadiusPixelsMin = Mathf.Max(0.25f, ambientRadiusPixelsMin);
        ambientRadiusPixelsMax = Mathf.Max(ambientRadiusPixelsMin, ambientRadiusPixelsMax);

        landmarkCoreRadiusPixelsMin = Mathf.Max(0.5f, landmarkCoreRadiusPixelsMin);
        landmarkCoreRadiusPixelsMax = Mathf.Max(landmarkCoreRadiusPixelsMin, landmarkCoreRadiusPixelsMax);
        landmarkRayPixelsMin = Mathf.Max(0.5f, landmarkRayPixelsMin);
        landmarkRayPixelsMax = Mathf.Max(landmarkRayPixelsMin, landmarkRayPixelsMax);

        deepSkyMinimumRadiusPixels = Mathf.Max(0.5f, deepSkyMinimumRadiusPixels);
        deepSkyRingThicknessPixels = Mathf.Max(0.5f, deepSkyRingThicknessPixels);
    }
}
