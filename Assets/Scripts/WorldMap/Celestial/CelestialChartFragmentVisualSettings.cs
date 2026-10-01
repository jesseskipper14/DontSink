using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/Celestial/Chart Fragment Visual Settings",
    fileName = "CelestialChartFragmentVisualSettings")]
public sealed class CelestialChartFragmentVisualSettings : ScriptableObject
{
    [Header("Scale")]
    [Tooltip("Shared physical scale for all player-charted scraps. Changing this changes every generated scrap equally.")]
    [Min(0.25f)] public float pixelsPerCelestialWorldUnit = 4f;

    [Tooltip("Blank paper around the outermost recorded celestial evidence, in celestial world units.")]
    [Min(0f)] public float paperMarginWorldUnits = 3.5f;

    [Tooltip("Minimum physical scrap width. Smaller observations get extra blank paper; their star scale is not changed.")]
    [Min(1f)] public float minimumPaperWidthWorld = 22f;

    [Tooltip("Minimum physical scrap height. Smaller observations get extra blank paper; their star scale is not changed.")]
    [Min(1f)] public float minimumPaperHeightWorld = 16f;

    [Header("Torn Paper")]
    [Range(0f, 18f)] public float tornEdgeDepthPixels = 7f;
    [Range(2f, 32f)] public float tornEdgeFeatureSizePixels = 11f;
    [Range(0f, 0.20f)] public float paperGrainStrength = 0.055f;

    [Header("Presentation")]
    public Color paperColor = new Color(0.78f, 0.69f, 0.50f, 1f);
    public Color inkColor = new Color(0.10f, 0.105f, 0.10f, 0.92f);

    private void OnValidate()
    {
        pixelsPerCelestialWorldUnit = Mathf.Max(0.25f, pixelsPerCelestialWorldUnit);
        paperMarginWorldUnits = Mathf.Max(0f, paperMarginWorldUnits);
        minimumPaperWidthWorld = Mathf.Max(1f, minimumPaperWidthWorld);
        minimumPaperHeightWorld = Mathf.Max(1f, minimumPaperHeightWorld);
        tornEdgeDepthPixels = Mathf.Clamp(tornEdgeDepthPixels, 0f, 18f);
        tornEdgeFeatureSizePixels = Mathf.Clamp(tornEdgeFeatureSizePixels, 2f, 32f);
        paperGrainStrength = Mathf.Clamp(paperGrainStrength, 0f, 0.20f);
    }
}
