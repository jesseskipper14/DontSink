using System.Collections.Generic;
using UnityEngine;

public static class SpawnPointClaimService
{
    // Key: spawn transform instance id (runtime unique)
    // Value: playerId that last claimed it
    private static readonly Dictionary<int, string> _claimedBy = new();
    private static readonly Dictionary<int, int> _claimOrder = new(); // simple LRU
    private static int _orderCounter = 0;

    public static void ClearAll()
    {
        _claimedBy.Clear();
        _claimOrder.Clear();
        _orderCounter = 0;
    }

    public static void ReleaseClaimsForPlayer(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        // Avoid modifying during iteration
        var toRemove = new List<int>();
        foreach (var kv in _claimedBy)
        {
            if (kv.Value == playerId)
                toRemove.Add(kv.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _claimedBy.Remove(toRemove[i]);
            _claimOrder.Remove(toRemove[i]);
        }
    }

    public static Transform ChooseAndClaimSpawn(Transform boatRoot, string playerId, out bool reusedClaimedPoint)
    {
        reusedClaimedPoint = false;

        if (boatRoot == null) return null;

        var points = boatRoot.GetComponentsInChildren<PlayerSpawnPoint>(true);
        if (points == null || points.Length == 0) return null;

        // Prefer unclaimed points, with simple tie-breakers.
        PlayerSpawnPoint bestFree = null;
        int bestFreeScore = int.MinValue;

        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            if (p == null) continue;

            int id = p.transform.GetInstanceID();
            bool claimed = _claimedBy.ContainsKey(id);

            if (claimed) continue;

            // Score: weight, then prefer lower slot index (if set)
            int score = p.weight * 1000;
            if (p.slotIndex >= 0) score -= p.slotIndex;

            if (score > bestFreeScore)
            {
                bestFreeScore = score;
                bestFree = p;
            }
        }

        if (bestFree != null)
        {
            Claim(bestFree.transform, playerId);
            return bestFree.transform;
        }

        // All claimed. That's fine. Reuse the least recently claimed (LRU-ish).
        // This keeps “100 players” from hard failing.
        reusedClaimedPoint = true;

        PlayerSpawnPoint bestReuse = null;
        int bestOrder = int.MaxValue;

        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            if (p == null) continue;

            int id = p.transform.GetInstanceID();
            int order = _claimOrder.TryGetValue(id, out var o) ? o : int.MinValue;

            // We want the smallest order (oldest claim)
            if (order < bestOrder)
            {
                bestOrder = order;
                bestReuse = p;
            }
        }

        if (bestReuse == null) bestReuse = points[0];

        Claim(bestReuse.transform, playerId);
        return bestReuse.transform;
    }

    private static void Claim(Transform spawn, string playerId)
    {
        if (spawn == null) return;
        if (string.IsNullOrEmpty(playerId)) playerId = "unknown";

        int id = spawn.GetInstanceID();
        _claimedBy[id] = playerId;
        _claimOrder[id] = _orderCounter++;
    }
}