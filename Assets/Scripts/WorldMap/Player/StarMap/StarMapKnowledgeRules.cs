using System;
using UnityEngine;

namespace WorldMap.Player.StarMap
{
    /// <summary>
    /// Pure rules for how route knowledge progresses and which explicit state results.
    /// Centralize thresholds here so UI/travel never guesses.
    /// </summary>
    public static class StarMapKnowledgeRules
    {
        // Tune these later. Keep them as named constants so you don't scatter magic numbers.
        public const float PartialThreshold01 = 0.25f;
        public const float KnownThreshold01 = 1.0f;

        /// <summary>
        /// Compute the explicit knowledge state from the record's internal fields.
        /// Important: This does NOT special-case intra-cluster knowledge; that's a separate policy rule.
        /// </summary>
        public static RouteKnowledgeState ComputeState(RouteKnowledgeRecord record)
        {
            if (record == null)
                return RouteKnowledgeState.Unknown;

            // If you have any progress, at minimum you've "heard something" (rumor).
            if (record.ChartingProgress01 > 0f || record.RumorCount > 0)
            {
                if (record.ChartingProgress01 >= KnownThreshold01)
                    return RouteKnowledgeState.Known;

                if (record.ChartingProgress01 >= PartialThreshold01)
                    return RouteKnowledgeState.Partial;

                return RouteKnowledgeState.Rumored;
            }

            return RouteKnowledgeState.Unknown;
        }

        /// <summary>
        /// Apply a rumor. This should NEVER directly make a route Known.
        /// It just ensures the route isn't Unknown anymore.
        /// </summary>
        public static RouteKnowledgeState ApplyRumor(RouteKnowledgeRecord record, int amount = 1)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            record.RumorCount = Mathf.Max(0, record.RumorCount + Mathf.Max(1, amount));

            // Keep charting progress unchanged. Rumor alone never implies partial charting.
            var computed = ComputeState(record);
            record.State = computed;
            return computed;
        }

        /// <summary>
        /// Apply charting progress from observation/exploration.
        /// Returns the new explicit state after applying progress.
        /// </summary>
        public static RouteKnowledgeState ApplyProgress(RouteKnowledgeRecord record, float delta01)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            if (delta01 <= 0f)
            {
                // Still enforce state consistency if caller is sloppy.
                var computed0 = ComputeState(record);
                record.State = computed0;
                return computed0;
            }

            record.ChartingProgress01 = Mathf.Clamp01(record.ChartingProgress01 + delta01);

            var computed = ComputeState(record);
            record.State = computed;
            return computed;
        }

        /// <summary>
        /// Force an explicit state and clamp internal fields to match.
        /// Use for debug controls, migrations, or scripted events.
        /// </summary>
        public static void ForceState(RouteKnowledgeRecord record, RouteKnowledgeState state)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            record.State = state;

            switch (state)
            {
                case RouteKnowledgeState.Unknown:
                    record.RumorCount = 0;
                    record.ChartingProgress01 = 0f;
                    break;

                case RouteKnowledgeState.Rumored:
                    record.RumorCount = Mathf.Max(1, record.RumorCount);
                    record.ChartingProgress01 = Mathf.Min(record.ChartingProgress01, PartialThreshold01 - 0.0001f);
                    break;

                case RouteKnowledgeState.Partial:
                    record.RumorCount = Mathf.Max(1, record.RumorCount);
                    record.ChartingProgress01 = Mathf.Max(record.ChartingProgress01, PartialThreshold01);
                    record.ChartingProgress01 = Mathf.Min(record.ChartingProgress01, KnownThreshold01 - 0.0001f);
                    break;

                case RouteKnowledgeState.Known:
                    record.RumorCount = Mathf.Max(1, record.RumorCount);
                    record.ChartingProgress01 = KnownThreshold01;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown RouteKnowledgeState");
            }
        }
    }
}
