using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NodeCivicState
{
    public int nextQuestSequence = 1;
    public List<CivicQuestEntry> quests = new();
    public List<AdjacentNodeIntelSnapshot> adjacentIntel = new();
    public List<string> receivedIntelContactIds = new();
    public NodeCivicState Copy() => JsonUtility.FromJson<NodeCivicState>(JsonUtility.ToJson(this));
    public void EnsureDefaults()
    {
        quests ??= new(); adjacentIntel ??= new(); receivedIntelContactIds ??= new();
        nextQuestSequence = Mathf.Max(1, nextQuestSequence);
        foreach (var report in adjacentIntel)
            if (report != null && !string.IsNullOrWhiteSpace(report.contactId) && !receivedIntelContactIds.Contains(report.contactId))
                receivedIntelContactIds.Add(report.contactId);
    }
}

public enum CivicQuestStatus { Offered, Accepted, Completed }

[Serializable]
public sealed class CivicQuestEntry
{
    public string id, nodeId, title, description;
    public string providerId = "temporary_civic_check_in";
    public CivicQuestStatus status;
    public bool temporary = true;
    public CivicQuestEntry Copy() => JsonUtility.FromJson<CivicQuestEntry>(JsonUtility.ToJson(this));
}

[Serializable]
public sealed class AdjacentNodeIntelSnapshot
{
    public string sourceNodeId, observedNodeId, observedName;
    public double observedAtWorldHours, receivedAtWorldHours;
    public string provenance, report;
    public string contactId;
    public float population;
    public List<WorldMapNodeStatSaveSnapshot> stats = new();
    public List<string> flags = new(), majorEvents = new(), majorBuffs = new();
    public List<WorldMapNodeResourcePressureSaveSnapshot> resourcePressures = new();
    public AdjacentNodeIntelSnapshot Copy() => JsonUtility.FromJson<AdjacentNodeIntelSnapshot>(JsonUtility.ToJson(this));
}
