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
    [Header("Geographic Land Detection (map units)")]
    [Min(0f)] public float landDetectionBaseRange = 10f;
    [Min(0f)] public float landDetectionMaximumRange = 30f;
    [Tooltip("Visibility range adds this fraction of the island's equivalent diameter, up to the maximum range.")]
    [Min(0f)] public float landDetectionSizeFactor = .35f;
    [Tooltip("Keep the current island unless another is this much closer. Never retains an out-of-range island.")]
    [Min(0f)] public float landEncounterSwitchMargin = 1f;
    [Header("Development Proof Feature (debug builds only)")]
    public bool enableDebugFeature;
    [Tooltip("Signed physical strip distance from voyage spawn; camera and geographic warps do not move it.")]
    public float debugFeatureCenter = 1024f;
    [Min(8f)] public float debugFeatureHalfWidth = 512f;
    [Tooltip("Positive adds depth (trench); negative removes depth (rise). Final floor stays underwater and slope-limited.")]
    public float debugFeatureDepthDelta = 100f;
    [Tooltip("Fraction of each half-width used for the wall; the rest is a flat bottom. Smaller means sharper walls.")]
    [Range(.05f, 1f)] public float debugFeatureEdgeFraction = .15f;
    [Tooltip("Explicit feature wall cap; ordinary geographic depth retains its separate slope limits.")]
    [Range(1f, 80f)] public float debugFeatureMaximumSlopeDegrees = 75f;
}
