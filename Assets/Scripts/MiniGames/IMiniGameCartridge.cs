namespace MiniGames
{
    public interface IMiniGameCartridge
    {
        /// <summary>Called once when the overlay opens.</summary>
        void Begin(MiniGameContext context);

        /// <summary>Advance the minigame. Returns outcome None while still running.</summary>
        MiniGameResult Tick(float dt, MiniGameInput input);

        /// <summary>Called when player exits manually (Esc).</summary>
        MiniGameResult Cancel();

        /// <summary>Called when the world interrupts the interaction (knockback, flooding, etc.).</summary>
        MiniGameResult Interrupt(string reason);

        /// <summary>Called once when overlay closes (after result finalized).</summary>
        void End();
    }
}
