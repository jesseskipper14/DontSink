using UnityEngine;

namespace WorldMap.Player.StarMap
{
    /// <summary>
    /// Read-only convenience helpers for UI/visualization.
    /// Keeps UI from re-implementing policy and record lookups.
    /// </summary>
    public static class StarMapVisualQuery
    {
        /// <summary>
        /// Returns the knowledge state for a route for visualization.
        /// Note: Intra-cluster routes are treated as Known by default.
        /// </summary>
        public static RouteKnowledgeState GetVisualState(
            WorldMapPlayerState player,
            string fromStableId, int fromClusterId,
            string toStableId, int toClusterId)
        {
            if (player == null)
                return RouteKnowledgeState.Unknown;

            // Intra-cluster is known by default (your explicit rule).
            if (fromClusterId == toClusterId)
                return RouteKnowledgeState.Known;

            var key = RouteKey.Make(fromStableId, toStableId);

            // If starMap doesn't exist yet, everything cross-cluster is Unknown.
            var sm = player.starMap;
            if (sm == null)
                return RouteKnowledgeState.Unknown;

            var svc = new StarMapService(sm);
            return svc.GetKnowledgeState(key);
        }
    }
}
