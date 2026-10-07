using UnityEngine;

public sealed partial class WorldMapKnowledgeSource
{
    [Header("Sounding Charts")]
    [Tooltip("Radius in map units revealed by a newly processed sounding chart. Existing processed charts retain their saved coverage.")]
    [Min(.1f)] [SerializeField] private float soundingRevealRadius = SoundingChartBuilder.DefaultRevealRadius;
    public float SoundingRevealRadius => soundingRevealRadius;
}
