using System.Collections.Generic;
using UnityEngine;

public interface INodeCivicQuestProvider
{
    IReadOnlyList<CivicQuestEntry> GetAvailable(string nodeId);
    bool TryAccept(string nodeId, string questId, out string reason);
    bool TryTurnIn(string nodeId, string questId, out string reason);
}

/// <summary>Temporary node-owned generic provider. Future generators can replace the provider, not the leader.</summary>
public sealed class NodeCivicQuestProvider : INodeCivicQuestProvider
{
    public static readonly NodeCivicQuestProvider Instance = new();
    public static MapNodeState FindState(string nodeId)
    {
        var store = GameState.I?.worldMap?.byNodeStableId;
        return store != null && nodeId != null && store.TryGetValue(nodeId, out var state) && state?.NodeId == nodeId ? state : null;
    }

    public IReadOnlyList<CivicQuestEntry> GetAvailable(string nodeId)
    {
        var state = FindState(nodeId);
        if (state == null) return new List<CivicQuestEntry>();
        if (GameplayAuthority.IsAuthoritative) EnsureAvailable(state);
        var result = new List<CivicQuestEntry>();
        if (state.civic?.quests != null)
            foreach (var quest in state.civic.quests)
                if (IsPending(quest, nodeId)) result.Add(quest.Copy());
        return result;
    }

    private static void EnsureAvailable(MapNodeState state)
    {
        state.civic ??= new NodeCivicState();
        state.civic.EnsureDefaults();
        foreach (var quest in state.civic.quests)
            if (IsPending(quest, state.NodeId)) return;
        string id;
        do { id = state.NodeId + "/civic/check_in/" + (state.civic.nextQuestSequence++).ToString(System.Globalization.CultureInfo.InvariantCulture); }
        while (state.civic.quests.Exists(q => q != null && q.id == id));
        state.civic.quests.Add(new CivicQuestEntry {
            id = id, nodeId = state.NodeId, title = "Settlement check-in",
            description = "Introduce yourself to the settlement's leader. Accept this work, then confirm your check-in here. This temporary civic task has no payment or item reward." });
    }

    public bool TryAccept(string nodeId, string questId, out string reason) => Transition(nodeId, questId, false, out reason);
    public bool TryTurnIn(string nodeId, string questId, out string reason) => Transition(nodeId, questId, true, out reason);
    private static bool Transition(string nodeId, string questId, bool complete, out string reason)
    {
        reason = "Civic work requires the authoritative host.";
        if (!GameplayAuthority.IsAuthoritative) return false;
        var state = FindState(nodeId);
        if (state?.civic?.quests == null) { reason = "This settlement's work is unavailable."; return false; }
        var quest = state.civic.quests.Find(q => q != null && q.id == questId && q.nodeId == nodeId);
        if (quest == null || quest.providerId != "temporary_civic_check_in" || !quest.temporary ||
            quest.status != (complete ? CivicQuestStatus.Accepted : CivicQuestStatus.Offered))
        { reason = "That task is no longer available for this action."; return false; }
        quest.status = complete ? CivicQuestStatus.Completed : CivicQuestStatus.Accepted;
        reason = complete ? "Check-in recorded. No payment or item reward." : "Check-in accepted. Confirm it with this leader to finish.";
        if (complete) EnsureAvailable(state);
        return true;
    }

    private static bool IsPending(CivicQuestEntry quest, string nodeId) => quest != null &&
        !string.IsNullOrWhiteSpace(quest.id) && quest.nodeId == nodeId && quest.temporary &&
        quest.providerId == "temporary_civic_check_in" &&
        (quest.status == CivicQuestStatus.Offered || quest.status == CivicQuestStatus.Accepted);
}
