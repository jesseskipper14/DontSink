using UnityEngine;

/// <summary>
/// Central preflight rule for leaving a scene with the player boat.
/// Blocks departure while any tether deployment module still owns a deployed payload.
/// Cut-loose payloads no longer belong to their deployment module and therefore do not block.
/// </summary>
public static class BoatDepartureGate
{
    public static bool CanDepart(
        Boat boat,
        out string reason)
    {
        reason = null;

        // This gate only evaluates deployment blockers. Missing-boat handling remains
        // owned by the existing transition/persistence code.
        if (boat == null)
            return true;

        TetherDeploymentModule[] deployments =
            boat.GetComponentsInChildren<TetherDeploymentModule>(true);

        if (deployments == null || deployments.Length == 0)
            return true;

        int deployedCount = 0;
        string firstDeploymentName = null;

        for (int i = 0; i < deployments.Length; i++)
        {
            TetherDeploymentModule deployment = deployments[i];
            if (deployment == null || !deployment.HasDeployedPayload)
                continue;

            deployedCount++;

            if (string.IsNullOrWhiteSpace(firstDeploymentName))
                firstDeploymentName = deployment.name;
        }

        if (deployedCount <= 0)
            return true;

        reason =
            deployedCount == 1
                ? $"Tether payload is still deployed on '{firstDeploymentName}'. Raise/stow it or cut the line before departing."
                : $"{deployedCount} tether payloads are still deployed. Raise/stow them or cut their lines before departing.";

        return false;
    }
}
