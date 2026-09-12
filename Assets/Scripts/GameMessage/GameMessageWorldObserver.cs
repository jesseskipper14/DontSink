using System;
using UnityEngine;

/// <summary>
/// Watches a tiny set of important persistent world-state changes that are useful
/// to the player but do not warrant system-specific HUDs.
///
/// Deliberately does NOT report high-frequency/common actions such as boarding,
/// unboarding, or module installation.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-8900)]
public sealed class GameMessageWorldObserver : MonoBehaviour
{
    private bool _initialized;
    private string _lastLockedDestinationId;

    private void Update()
    {
        GameState gs =
            GameState.I;

        WorldMapPlayerState player =
            gs != null
                ? gs.player
                : null;

        if (player == null)
            return;

        string currentLockedId =
            Normalize(
                player.lockedDestinationNodeId);

        if (!_initialized)
        {
            _lastLockedDestinationId =
                currentLockedId;

            _initialized =
                true;

            return;
        }

        if (string.Equals(
                currentLockedId,
                _lastLockedDestinationId,
                StringComparison.Ordinal))
        {
            return;
        }

        _lastLockedDestinationId =
            currentLockedId;

        if (string.IsNullOrEmpty(
                currentLockedId))
        {
            GameMessageService.PostInfo(
                "Destination unlocked.");

            return;
        }

        string place =
            GameMessageLocationResolver.ResolveDisplayName(
                currentLockedId);

        GameMessageService.PostInfo(
            $"Destination locked to {place}.");
    }

    private static string Normalize(
        string value)
    {
        return
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
    }
}
