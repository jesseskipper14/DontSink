using System;
using UnityEngine;

namespace WorldMap.Player.StarMap
{
    /// <summary>
    /// Internal tracking for a single route's knowledge for a player.
    /// Numbers may exist internally, but UI should primarily use State.
    /// </summary>
    [Serializable]
    public sealed class RouteKnowledgeRecord
    {
        [SerializeField] private RouteKnowledgeState _state = RouteKnowledgeState.Unknown;

        // Internal progress. Do not show raw numbers to players.
        // This supports gradual advancement (rumors, observations, exploration).
        [SerializeField, Range(0f, 1f)] private float _chartingProgress01 = 0f;

        // Optional: lightweight provenance for future features (rumor sources, etc.)
        [SerializeField] private int _rumorCount = 0;

        public RouteKnowledgeState State
        {
            get => _state;
            set => _state = value;
        }

        public float ChartingProgress01
        {
            get => _chartingProgress01;
            set => _chartingProgress01 = Mathf.Clamp01(value);
        }

        public int RumorCount
        {
            get => _rumorCount;
            set => _rumorCount = Mathf.Max(0, value);
        }
    }
}
