using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/POIs/POI Generation Settings",
    fileName = "WorldMapPOIGenerationSettings")]
public sealed class WorldMapPOIGenerationSettings : ScriptableObject
{
    [Header("Candidate Scan")]
    [Range(16, 512)] public int candidateGridWidth = 180;
    [Range(16, 512)] public int candidateGridHeight = 112;

    [Tooltip("Reject candidates this close to the map edge, in UV units.")]
    [Range(0f, 0.35f)] public float candidateEdgeInset01 = 0.035f;

    [Tooltip("World-space radius used to inspect local terrain around a POI candidate.")]
    [Min(0.1f)] public float sampleRadiusWorld = 10f;

    [Tooltip("Local sample grid per candidate. Odd values like 5 or 7 work best.")]
    [Range(3, 11)] public int sampleGrid = 5;

    [Tooltip("Small deterministic score noise to break ties.")]
    [Range(0f, 1f)] public float candidateNoise = 0.08f;

    [Header("Spacing")]
    [Tooltip("Minimum distance between all POIs, regardless of definition.")]
    [Min(0.1f)] public float minGlobalSpacing = 14f;

    [Header("Debug")]
    public bool logGenerationSummary = true;

    private void OnValidate()
    {
        candidateGridWidth = Mathf.Max(1, candidateGridWidth);
        candidateGridHeight = Mathf.Max(1, candidateGridHeight);
        sampleRadiusWorld = Mathf.Max(0.1f, sampleRadiusWorld);
        sampleGrid = Mathf.Max(1, sampleGrid);
        minGlobalSpacing = Mathf.Max(0.1f, minGlobalSpacing);
    }
}
