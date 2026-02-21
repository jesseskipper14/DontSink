using System;
using UnityEngine;

namespace WorldMap.Player.StarMap
{
    /// <summary>
    /// Small, explicit API over PlayerStarMapState.
    /// Not a MonoBehaviour. No Unity scene dependencies.
    /// This is the single place to mutate star-map knowledge.
    /// </summary>
    public sealed class StarMapService
    {
        private readonly PlayerStarMapState _state;

        public StarMapService(PlayerStarMapState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Get the current explicit knowledge state for a route.
        /// If no record exists, returns Unknown.
        /// </summary>
        public RouteKnowledgeState GetKnowledgeState(string routeKey)
        {
            if (string.IsNullOrWhiteSpace(routeKey))
                return RouteKnowledgeState.Unknown;

            if (!_state.TryGet(routeKey, out var rec) || rec == null)
                return RouteKnowledgeState.Unknown;

            // Keep record.State consistent with fields in case older saves wrote mismatched values.
            var computed = StarMapKnowledgeRules.ComputeState(rec);
            if (rec.State != computed)
                rec.State = computed;

            return rec.State;
        }

        /// <summary>
        /// True only when the route is fully known.
        /// (Matches your current phase: travel requires Known.)
        /// </summary>
        public bool IsRouteKnown(string routeKey) => GetKnowledgeState(routeKey) == RouteKnowledgeState.Known;

        /// <summary>
        /// Apply a rumor to a route. Returns resulting explicit state.
        /// </summary>
        public RouteKnowledgeState AddRumor(string routeKey, int amount = 1)
        {
            var rec = _state.GetOrCreate(routeKey);
            return StarMapKnowledgeRules.ApplyRumor(rec, amount);
        }

        /// <summary>
        /// Apply charting progress to a route. Returns resulting explicit state.
        /// </summary>
        public RouteKnowledgeState AddProgress(string routeKey, float delta01)
        {
            var rec = _state.GetOrCreate(routeKey);
            return StarMapKnowledgeRules.ApplyProgress(rec, delta01);
        }

        /// <summary>
        /// Debug/migration operation: force an explicit state.
        /// </summary>
        public void ForceState(string routeKey, RouteKnowledgeState state)
        {
            var rec = _state.GetOrCreate(routeKey);
            StarMapKnowledgeRules.ForceState(rec, state);
        }

        /// <summary>
        /// Remove all knowledge of a route.
        /// </summary>
        public void ForgetRoute(string routeKey)
        {
            _state.Remove(routeKey);
        }

        /// <summary>
        /// Optional: for debugging/inspecting saves.
        /// </summary>
        public PlayerStarMapState RawState => _state;
    }
}
