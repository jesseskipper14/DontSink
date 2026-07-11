using System;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Agents/Spawn Groups/Spawn Points",
    fileName = "AgentSpawnPointGroup")]
public sealed class AgentSpawnPointGroupDefinition : AgentSpawnGroupDefinition
{
    [Header("Filtering")]
    [SerializeField] private bool includeInactivePoints = false;

    [Tooltip("Optional substring filter for spawn point stable ids. Leave empty to spawn all.")]
    [SerializeField] private string stableIdContains;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    public override void Spawn(AgentSpawnContext context)
    {
        AgentSpawnPoint[] points = UnityEngine.Object.FindObjectsByType<AgentSpawnPoint>(
            includeInactivePoints ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (points == null || points.Length == 0)
        {
            Log("No AgentSpawnPoint objects found.");
            return;
        }

        Array.Sort(points, ComparePoints);

        int spawned = 0;

        for (int i = 0; i < points.Length; i++)
        {
            AgentSpawnPoint point = points[i];

            if (point == null)
                continue;

            if (!MatchesFilter(point))
                continue;

            AgentController agent = point.Spawn();

            if (agent != null)
                spawned++;
        }

        Log($"Spawned {spawned} agents from {points.Length} spawn points.");
    }

    private bool MatchesFilter(AgentSpawnPoint point)
    {
        if (string.IsNullOrWhiteSpace(stableIdContains))
            return true;

        return !string.IsNullOrWhiteSpace(point.StableId) &&
               point.StableId.IndexOf(stableIdContains, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int ComparePoints(AgentSpawnPoint a, AgentSpawnPoint b)
    {
        string aId = a != null ? a.StableId : "";
        string bId = b != null ? b.StableId : "";

        return string.CompareOrdinal(aId, bId);
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[AgentSpawnPointGroupDefinition] {message}", this);
    }
}