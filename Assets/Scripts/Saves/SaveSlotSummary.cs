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

    public bool isValid;
    public string invalidReason;
}