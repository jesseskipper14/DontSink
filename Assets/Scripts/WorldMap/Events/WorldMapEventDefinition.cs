using UnityEngine;

public abstract class WorldMapEventDefinition : ScriptableObject
{
    [Header("Identity")]
    public string eventId;
    public string displayName;
    public bool isVisibleToPlayer = true;

    // Create a fresh runtime instance for a node.
    public abstract WorldMapEventInstance CreateInstance(int sourceNodeId, int seed);
}
