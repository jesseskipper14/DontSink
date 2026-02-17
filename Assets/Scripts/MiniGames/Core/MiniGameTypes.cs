using System;
using UnityEngine;

namespace MiniGames
{
    [Serializable]
    public sealed class MiniGameContext
    {
        /// <summary>Arbitrary identifier for what is being worked on (pump instance id, routeKey, etc.).</summary>
        public string targetId;

        /// <summary>Difficulty scalar (1 = baseline). Cartridge decides how to use it.</summary>
        public float difficulty = 1f;

        /// <summary>Optional: time pressure multiplier from the world.</summary>
        public float pressure = 0f;

        /// <summary>Optional: seed for deterministic simulations.</summary>
        public int seed = 0;

        /// <summary>
        /// Optional: effect emission hook for cartridges to report deltas/control/buffs.
        /// Host supplies this when opening the overlay. Cartridges should treat it as fire-and-forget.
        /// </summary>
        [NonSerialized] public Action<MiniGameEffect> emitEffect;
    }

    public struct MiniGameInput
    {
        public Vector2 pointer;        // normalized -1..1 or screen? your choice; start simple
        public Vector2 move;           // WASD (-1..1)
        public bool actionDown;         // click/confirm
        public bool actionHeld;
        public bool actionUp;

        public static MiniGameInput FromUnity()
        {
            var i = new MiniGameInput();

            i.move = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            // Raw mouse delta is fine for now; cartridges can interpret.
            i.pointer = Input.mousePosition;

            i.actionDown = Input.GetMouseButtonDown(0);
            i.actionHeld = Input.GetMouseButton(0);
            i.actionUp = Input.GetMouseButtonUp(0);

            return i;
        }
    }
}
