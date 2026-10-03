using UnityEngine;

[CreateAssetMenu(menuName = "World/Boat Streamed Terrain Profile", fileName = "BoatTerrainProfile")]
public sealed class BoatTerrainProfile : ScriptableObject
{
    [Min(8f)] public float chunkWidth = 128f;
    [Min(.1f)] public float sampleSpacing = 1f;
    public float waterLevelY;
    [Min(1f)] public float baseDepth = 120f;
    [Min(1f)] public float maximumDepth = 500f;
    [Min(0f)] public float rollingAmplitude = 4f;
    [Min(1f)] public float rollingWavelength = 80f;
    [Range(1f, 45f)] public float maximumSlopeDegrees = 20f;
    [Min(1f)] public float fillDepth = 25f;
    [Min(32f)] public float interestRadius = 256f;
    [Min(8f)] public float unloadBuffer = 128f;
}
