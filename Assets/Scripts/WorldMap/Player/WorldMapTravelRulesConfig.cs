using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Travel Rules Config", fileName = "WorldMapTravelRulesConfig")]
public sealed class WorldMapTravelRulesConfig : ScriptableObject
{
    [Header("Travel Rules")]
    [Min(0f)]
    public float maxRouteLength = 999f;
}
