using System;
using UnityEngine;

/// <summary>
/// Resolves a stable world-map node ID to a player-facing place name.
/// Prefers the live runtime registry, then falls back to the persisted graph.
/// </summary>
public static class GameMessageLocationResolver
{
    public static string ResolveDisplayName(
        string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return "Unknown destination";

        WorldMapRuntimeBinder binder =
            UnityEngine.Object.FindAnyObjectByType<WorldMapRuntimeBinder>(
                FindObjectsInactive.Include);

        if (binder != null &&
            binder.Registry != null &&
            binder.Registry.TryGetByStableId(
                stableId,
                out MapNodeRuntime runtime) &&
            runtime != null &&
            !string.IsNullOrWhiteSpace(
                runtime.DisplayName))
        {
            return
                runtime.DisplayName.Trim();
        }

        GameState gs =
            GameState.I;

        if (gs != null &&
            gs.worldMapSnapshot != null &&
            gs.worldMapSnapshot.graph != null &&
            gs.worldMapSnapshot.graph.nodes != null)
        {
            var nodes =
                gs.worldMapSnapshot.graph.nodes;

            for (int i = 0;
                 i < nodes.Count;
                 i++)
            {
                WorldMapGraphNodeSaveSnapshot node =
                    nodes[i];

                if (node == null ||
                    !string.Equals(
                        node.stableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(
                        node.displayName))
                {
                    return
                        node.displayName.Trim();
                }

                break;
            }
        }

        return stableId;
    }
}
