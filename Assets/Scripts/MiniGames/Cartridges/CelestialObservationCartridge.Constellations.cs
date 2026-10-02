using System;
using UnityEngine;

namespace MiniGames
{
    public sealed partial class CelestialObservationCartridge
    {
        private readonly Func<bool> _debugShowAllConstellations;
        private bool _showKnownConstellations = true;

        private void DrawKnownConstellations(Rect lens, float visibility)
        {
            bool debug = _debugShowAllConstellations != null && _debugShowAllConstellations();
            if ((!_showKnownConstellations && !debug) || visibility <= 0.001f || _field == null) return;
            var state = GameState.I != null ? GameState.I.celestialCharts : null;
            float radius = lens.width * Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
            foreach (var constellation in _field.Constellations.All)
            {
                if (!debug && !CelestialKnowledgeQueries.IsConstellationKnown(state, constellation.StableId)) continue;
                foreach (var edge in constellation.Edges)
                {
                    var a = FindProjected(edge.fromStarStableId);
                    var b = FindProjected(edge.toStarStableId);
                    if (a == null || b == null) continue;
                    DrawLineClippedToCircle(a.screenPoint, b.screenPoint, lens.center, radius,
                        new Color(0.42f, 0.78f, 0.95f, visibility * 0.6f), 1.5f);
                }
            }
        }
    }
}
