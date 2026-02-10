using System;

public enum WorldMapChangeKind
{
    StatChanged,
    BuildingChanged,
    FlagAdded,
    FlagRemoved
}

public readonly struct WorldMapChange
{
    public readonly WorldMapChangeKind kind;
    public readonly int nodeId;
    public readonly string nodeName;
    public readonly string key;
    public readonly float oldValue;
    public readonly float newValue;

    public WorldMapChange(WorldMapChangeKind kind, int nodeId, string nodeName, string key, float oldValue, float newValue)
    {
        this.kind = kind;
        this.nodeId = nodeId;
        this.nodeName = string.IsNullOrWhiteSpace(nodeName) ? $"Node {nodeId}" : nodeName;
        this.key = key;
        this.oldValue = oldValue;
        this.newValue = newValue;
    }

    public override string ToString()
        => $"[{kind}] #{nodeId} {nodeName} | {key}: {oldValue:0.00} → {newValue:0.00}";
}

public static class WorldMapMessageBus
{
    public static event Action<WorldMapChange> OnChange;

    public static void Publish(WorldMapChange change)
    {
        OnChange?.Invoke(change);
    }
}
