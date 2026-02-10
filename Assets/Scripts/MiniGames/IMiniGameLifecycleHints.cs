namespace MiniGames
{
    /// <summary>
    /// Optional hints about how the host should treat this cartridge's lifetime.
    /// Cartridges that don't implement this are treated as session-based by default.
    /// </summary>
    public interface IMiniGameLifecycleHints
    {
        /// <summary>
        /// True if this mini-game is intended to run indefinitely and should not auto-close
        /// based on MiniGameResult outcomes (except if the host/player explicitly closes).
        /// </summary>
        bool IsOngoing { get; }
    }
}
