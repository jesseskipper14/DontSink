namespace MiniGames
{
    public enum MiniGameOutcome
    {
        None = 0,        // still running
        Completed = 1,   // succeeded (may have quality)
        Partial = 2,     // exited/interrupted with some progress
        Cancelled = 3,   // exited with no meaningful progress
        Failed = 4       // explicit failure (rare)
    }
}
