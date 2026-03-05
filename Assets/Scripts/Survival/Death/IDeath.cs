namespace Survival.Death
{
    public readonly struct DeathInfo
    {
        public readonly string causeId;     // stable string ID, not enum
        public readonly string message;     // human readable (optional)
        public readonly float timeOfDeath;  // Time.time
        public DeathInfo(string causeId, string message, float timeOfDeath)
        {
            this.causeId = causeId;
            this.message = message;
            this.timeOfDeath = timeOfDeath;
        }
    }

    public interface IDeathCause
    {
        string CauseId { get; }

        /// <summary>
        /// Return true when this cause says the player should die now.
        /// Fill out info with cause details.
        /// </summary>
        bool Evaluate(float dt, out DeathInfo info);
    }

    public interface IDeathHandler
    {
        int Priority { get; } // lower runs first
        void OnDeath(in DeathInfo info);
        void OnRespawn(); // future-safe
    }
}