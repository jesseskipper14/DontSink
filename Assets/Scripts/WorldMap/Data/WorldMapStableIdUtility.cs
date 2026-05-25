using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldMapStableIdUtility
{
    public static string BuildNodeStableId(int worldSeed, MapNode node)
    {
        if (node == null)
            return string.Empty;

        string local = node.localStableId;

        if (string.IsNullOrWhiteSpace(local))
        {
            Debug.LogWarning(
                $"MapNode #{node.id} has empty localStableId. Falling back to legacy id. " +
                "This should be fixed before world map persistence is trusted."
            );

            local = $"legacy_node_{node.id:0000}";
        }

        return $"{worldSeed}:{local}";
    }

    public static string BuildNodeStableId(int worldSeed, string localStableId)
    {
        if (string.IsNullOrWhiteSpace(localStableId))
            return string.Empty;

        return $"{worldSeed}:{localStableId}";
    }
}