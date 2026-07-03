using System.Collections.Generic;
using UnityEngine;

public static class AgentRegistry
{
    private static readonly Dictionary<string, AgentController> AgentsByStableId = new Dictionary<string, AgentController>();

    public static IReadOnlyDictionary<string, AgentController> All => AgentsByStableId;

    public static void Register(AgentController agent)
    {
        if (agent == null)
            return;

        string stableId = agent.StableId;

        if (string.IsNullOrWhiteSpace(stableId))
            return;

        if (AgentsByStableId.TryGetValue(stableId, out AgentController existing) && existing != null && existing != agent)
        {
            Debug.LogWarning($"Duplicate agent stableId '{stableId}'. Replacing registry entry '{existing.name}' with '{agent.name}'.", agent);
        }

        AgentsByStableId[stableId] = agent;
    }

    public static void Unregister(AgentController agent)
    {
        if (agent == null)
            return;

        string stableId = agent.StableId;

        if (string.IsNullOrWhiteSpace(stableId))
            return;

        if (AgentsByStableId.TryGetValue(stableId, out AgentController existing) && existing == agent)
            AgentsByStableId.Remove(stableId);
    }

    public static bool TryGet(string stableId, out AgentController agent)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            agent = null;
            return false;
        }

        return AgentsByStableId.TryGetValue(stableId, out agent) && agent != null;
    }

    public static void Clear()
    {
        AgentsByStableId.Clear();
    }
}