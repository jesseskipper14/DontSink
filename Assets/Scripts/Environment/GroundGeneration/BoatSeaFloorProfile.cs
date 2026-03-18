using UnityEngine;

[CreateAssetMenu(
    fileName = "BoatSeaFloorProfile",
    menuName = "World/Boat Sea Floor Profile")]
public sealed class BoatSeaFloorProfile : ScriptableObject
{
    [Header("Dock / Plateau Layout")]
    [Min(0f)] public float leftPlateauLength = 18f;
    [Min(0f)] public float rightPlateauLength = 18f;
    [Min(0.1f)] public float leftSlopeLength = 20f;
    [Min(0.1f)] public float rightSlopeLength = 20f;

    [Header("Base Depth")]
    [Tooltip("Typical seafloor depth below landY in the travel section.")]
    [Min(0f)] public float baseSeaFloorDepth = 18f;

    [Tooltip("Additional broad depth drift across the trip.")]
    [Min(0f)] public float depthDriftAmplitude = 6f;

    [Min(0.001f)] public float depthDriftScale = 0.01f;

    [Header("Macro Shape")]
    [Min(0f)] public float macroAmplitude = 4f;
    [Min(0.001f)] public float macroScale = 0.03f;
    [Range(1, 6)] public int macroOctaves = 2;

    [Header("Micro Shape")]
    [Min(0f)] public float microAmplitude = 1.5f;
    [Min(0.001f)] public float microScale = 0.10f;
    [Range(1, 6)] public int microOctaves = 3;

    [Header("Fractal Tuning")]
    [Range(0.1f, 0.9f)] public float octavePersistence = 0.5f;
    [Range(1.2f, 3.5f)] public float octaveLacunarity = 2.0f;

    [Header("Slope Deformation")]
    [Min(0f)] public float slopeDeformationAmplitude = 0.8f;
    [Min(0.001f)] public float slopeDeformationScale = 0.08f;
    [Range(1, 6)] public int slopeDeformationOctaves = 2;
    [Min(0f)] public float slopeDeformationEdgeFade = 0.15f;

    [Header("Ravines / Trenches")]
    [Min(0)] public int ravineCountMin = 0;
    [Min(0)] public int ravineCountMax = 2;
    [Min(0.1f)] public float ravineWidthMin = 8f;
    [Min(0.1f)] public float ravineWidthMax = 24f;
    [Min(0f)] public float ravineDepthMin = 10f;
    [Min(0f)] public float ravineDepthMax = 40f;
    public bool ravinesUseFlatFloor = true;
    [Tooltip("0 = pointed ravine, higher = wider flat bottom.")]
    [Range(0f, 0.95f)] public float ravineFloorFlatness = 0.35f;

    [Tooltip("How soft the ravine edges are. Higher = wider shoulder.")]
    [Min(0.1f)] public float ravineEdgeSoftness = 2f;

    [Header("Cliffs")]
    [Min(0)] public int cliffCountMin = 0;
    [Min(0)] public int cliffCountMax = 2;

    [Min(0.1f)] public float cliffTopLengthMin = 6f;
    [Min(0.1f)] public float cliffTopLengthMax = 18f;

    [Min(0.1f)] public float cliffSlopeLengthMin = 2f;
    [Min(0.1f)] public float cliffSlopeLengthMax = 10f;

    [Min(0.1f)] public float cliffBottomLengthMin = 6f;
    [Min(0.1f)] public float cliffBottomLengthMax = 18f;

    [Min(0f)] public float cliffHeightMin = 6f;
    [Min(0f)] public float cliffHeightMax = 20f;

    [Min(0f)] public float cliffNoiseAmplitude = 0.75f;
    [Min(0.001f)] public float cliffNoiseScale = 0.08f;

    [Header("Safety / Clamps")]
    [Min(0f)] public float minDockClearDepth = 2f;
    [Min(0f)] public float maxAbsoluteDepth = 120f;
}