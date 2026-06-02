using System;

[Serializable]
public sealed class SaveSlotSummary
{
    public string slotId;
    public string profileId;
    public string displayName;
    public string path;

    public string createdUtc;
    public string updatedUtc;

    public int schemaVersion;
    public string gameVersion;

    public bool hasWorldMapSnapshot;
    public int worldMapNodeCount;
    public int worldMapEdgeCount;
    public int worldMapPOICount;
    public int worldMapEventCount;
    public int worldMapBuffCount;

    public bool isValid;
    public string invalidReason;
}