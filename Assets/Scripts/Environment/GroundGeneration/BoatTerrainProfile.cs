using UnityEngine;

[CreateAssetMenu(menuName = "World/Boat Streamed Terrain Profile", fileName = "BoatTerrainProfile")]
public sealed class BoatTerrainProfile : ScriptableObject
{
    [Min(8f)] public float chunkWidth = 128f;
    [Min(.1f)] public float sampleSpacing = 1f;
    public float waterLevelY;
    [Min(1f)] public float baseDepth = 120f;
    [Min(1f)] public float maximumDepth = 500f;
    [Header("Geographic Depth")]
    public bool useGeographicDepth = true;
    [Tooltip("Normalized water depth: 0 at coastline, 1 at lowest topography. Curve output is physical depth.")]
    public AnimationCurve geographicDepth = new AnimationCurve(
        new Keyframe(0f, 15f), new Keyframe(.25f, 60f),
        new Keyframe(.6f, 220f), new Keyframe(1f, 500f));
    [Min(0f)] public float rollingAmplitude = 4f;
    [Min(1f)] public float rollingWavelength = 80f;
    [Range(1f, 45f)] public float maximumSlopeDegrees = 20f;
    [Tooltip("Steeper safety limit when geographic target differs substantially from committed depth.")]
    [Range(1f, 45f)] public float largeDepthChangeSlopeDegrees = 40f;
    [Tooltip("Depth mismatch at which the steeper limit is fully available.")]
    [Min(1f)] public float largeDepthChangeThreshold = 100f;
    [Min(1f)] public float fillDepth = 25f;
    [Min(32f)] public float interestRadius = 256f;
    [Min(8f)] public float unloadBuffer = 128f;
}
