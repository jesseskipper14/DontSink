using System;

namespace MiniGames
{
    [Serializable]
    public sealed class MiniGameResult
    {
        public MiniGameOutcome outcome;

        /// <summary>0..1 overall quality of the attempt. Meaning depends on cartridge.</summary>
        public float quality01;

        /// <summary>Optional: human-readable debug/tooltip text.</summary>
        public string note;

        /// <summary>Whether the minigame applied any meaningful progress.</summary>
        public bool hasMeaningfulProgress;
    }
}
