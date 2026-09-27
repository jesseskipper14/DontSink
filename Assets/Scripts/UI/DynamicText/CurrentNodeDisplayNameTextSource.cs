using System;
using UnityEngine;

/// <summary>
/// Supplies the player-facing sign text for the node currently occupied by the
/// local game state. The node name uses GameMessageLocationResolver so signs use
/// the same display names as the rest of the game. Population comes from the
/// live MapNodeState for that same stable node ID.
///
/// Kept under the existing component/class name so authored sign prefabs do not
/// need to be rebuilt just because the sign gained a second line.
/// </summary>
[DisallowMultipleComponent]
public sealed class CurrentNodeDisplayNameTextSource : DynamicTextSourceBehaviour
{
    [Header("Population Line")]
    [Tooltip("string.Format template for the population line. {0} is the rounded current population.")]
    [SerializeField] private string populationFormat = "Population: {0:N0} sad cows";

    [Tooltip("If the current node's live population cannot be resolved, still show the node name by itself.")]
    [SerializeField] private bool allowNameWithoutPopulation = true;

    public override bool TryGetText(out string text)
    {
        text = null;

        GameState gs = GameState.I;
        if (gs == null ||
            gs.player == null ||
            string.IsNullOrWhiteSpace(gs.player.currentNodeId))
        {
            return false;
        }

        string nodeId = gs.player.currentNodeId;
        string displayName =
            GameMessageLocationResolver.ResolveDisplayName(nodeId);

        if (string.IsNullOrWhiteSpace(displayName))
            return false;

        if (!TryGetCurrentPopulation(gs, nodeId, out int population))
        {
            if (!allowNameWithoutPopulation)
                return false;

            text = displayName;
            return true;
        }

        text =
            displayName +
            "\n" +
            FormatPopulation(population);

        return true;
    }

    private static bool TryGetCurrentPopulation(
        GameState gs,
        string nodeId,
        out int population)
    {
        population = 0;

        if (gs == null ||
            gs.worldMap == null ||
            gs.worldMap.byNodeStableId == null ||
            string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        if (!gs.worldMap.byNodeStableId.TryGetValue(
                nodeId,
                out MapNodeState nodeState) ||
            nodeState == null)
        {
            return false;
        }

        population = Mathf.Max(
            0,
            Mathf.RoundToInt(nodeState.population));

        return true;
    }

    private string FormatPopulation(int population)
    {
        if (string.IsNullOrWhiteSpace(populationFormat))
            return $"Population: {population:N0}";

        try
        {
            return string.Format(
                populationFormat,
                population);
        }
        catch (FormatException)
        {
            Debug.LogWarning(
                $"[{nameof(CurrentNodeDisplayNameTextSource)}:{name}] " +
                $"Invalid population format '{populationFormat}'. Using fallback.",
                this);

            return $"Population: {population:N0}";
        }
    }
}
