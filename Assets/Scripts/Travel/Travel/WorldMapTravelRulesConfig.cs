using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Travel Rules Config", fileName = "WorldMapTravelRulesConfig")]
public sealed class WorldMapTravelRulesConfig : ScriptableObject
{
    [Header("Travel Rules")]
    [Tooltip("Legacy value retained for asset compatibility. Route distance is no longer limited.")]
    [Min(0f)]
    public float maxRouteLength = 999f;
}
