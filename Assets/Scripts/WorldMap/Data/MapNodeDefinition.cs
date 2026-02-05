using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Map Node Definition")]
public class MapNodeDefinition : ScriptableObject
{
    public string NodeId; // stable GUID-like string, not instanceID
    public string DisplayName;

    public BiomeId Biome;

    public ResourceId PrimaryResource;
    public ResourceId SecondaryResource;

    public FactionId PrimaryFaction;
    public FactionId SecondaryFaction;

    [TextArea] public string Notes; // placeholder dump
}
