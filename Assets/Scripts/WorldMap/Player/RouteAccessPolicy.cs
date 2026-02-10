using UnityEngine;

/// <summary>
/// Centralized, read-only travel policy helpers.
///
/// Keep rules here so gameplay gating (restrictions) and presentation (overlay/hover)
/// stay consistent without duplicated logic.
/// </summary>
public static class RouteAccessPolicy
{
    /// <summary>
    /// Returns true if the route between the two nodes is considered known/travelable
    /// by the player.
    ///
    /// Current rules:
    /// - Intra-cluster travel is known by default.
    /// - Cross-cluster travel is known only if the player has explicitly unlocked the route.
    /// </summary>
    public static bool IsRouteKnown(
        WorldMapPlayerState player,
        string fromStableId,
        string toStableId,
        int fromClusterId,
        int toClusterId)
    {
        if (fromClusterId == toClusterId)
            return true;

        if (player == null)
            return false;

        if (string.IsNullOrEmpty(fromStableId) || string.IsNullOrEmpty(toStableId))
            return false;

        var key = RouteKey.Make(fromStableId, toStableId);
        return player.unlockedRoutes != null && player.unlockedRoutes.Contains(key);
    }

    /// <summary>
    /// Convenience helper for UI/hover: compute whether a route is blocked and (if so) why.
    ///
    /// This mirrors the current restriction stack (max length, then known gate) so that
    /// tooltip/overlay messaging stays consistent with actual travel rules.
    /// </summary>
    public static bool TryGetBlockReason(
        WorldMapPlayerState player,
        string fromStableId,
        string toStableId,
        int fromClusterId,
        int toClusterId,
        float routeLength,
        float maxRouteLength,
        out string reason)
    {
        // Max length gate first (matches restriction ordering)
        if (!float.IsNaN(maxRouteLength) && routeLength > maxRouteLength)
        {
            reason = $"Route too long (max {maxRouteLength:0.00}, got {routeLength:0.00})";
            return true;
        }

        if (!IsRouteKnown(player, fromStableId, toStableId, fromClusterId, toClusterId))
        {
            reason = "Route locked. Requires star map.";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}
