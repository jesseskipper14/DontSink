using System;
using UnityEngine;

namespace MiniGames
{
    public enum MiniGameEffectKind
    {
        Progress,   // sticky accumulation (star charts, repairs)
        Control,    // ephemeral signal (piloting, aiming)
        Buff,        // temporary modifier request (impulse mitigation)
        Transaction   // structured payload (trade, transfers, crafting, etc.)
    }

    [Serializable]
    public struct MiniGameEffect
    {
        public MiniGameEffectKind kind;

        /// <summary>Which system should interpret this ("StarMap", "Repair", "Piloting").</summary>
        public string system;

        /// <summary>What it applies to (routeKey, pumpId, helmId, etc.).</summary>
        public string targetId;

        /// <summary>Main payload (delta for Progress, intensity for Control/Buff).</summary>
        public float value01;

        /// <summary>Quality associated with this effect slice (0..1). Optional for some kinds.</summary>
        public float quality01;

        /// <summary>Optional duration for Buff-style effects (seconds). 0 = instantaneous.</summary>
        public float durationSeconds;

        /// <summary>Optional vector payload (e.g., steering intent, aim offset).</summary>
        public Vector2 v2;

        /// <summary>
        /// Optional structured payload (JSON). Used for complex effects like trade transactions.
        /// Keep small and versionable (include a version field inside payload).
        /// </summary>
        public string payloadJson;


        public static MiniGameEffect Progress(string system, string targetId, float delta01, float quality01)
        {
            return new MiniGameEffect
            {
                kind = MiniGameEffectKind.Progress,
                system = system,
                targetId = targetId,
                value01 = delta01,
                quality01 = Mathf.Clamp01(quality01),
                durationSeconds = 0f,
                v2 = Vector2.zero,
                payloadJson = null
            };
        }

        public static MiniGameEffect Control(string system, string targetId, float intensity01, float quality01, Vector2 v2)
        {
            return new MiniGameEffect
            {
                kind = MiniGameEffectKind.Control,
                system = system,
                targetId = targetId,
                value01 = Mathf.Clamp01(intensity01),
                quality01 = Mathf.Clamp01(quality01),
                durationSeconds = 0f,
                v2 = v2,
                payloadJson = null
            };
        }

        public static MiniGameEffect Buff(string system, string targetId, float intensity01, float quality01, float durationSeconds)
        {
            return new MiniGameEffect
            {
                kind = MiniGameEffectKind.Buff,
                system = system,
                targetId = targetId,
                value01 = Mathf.Clamp01(intensity01),
                quality01 = Mathf.Clamp01(quality01),
                durationSeconds = Mathf.Max(0f, durationSeconds),
                v2 = Vector2.zero,
                payloadJson = null
            };
        }
    }
}
