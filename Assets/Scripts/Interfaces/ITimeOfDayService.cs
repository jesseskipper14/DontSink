using System;

public enum DayPhase
{
    Night,
    Dawn,
    Day,
    Dusk
}

public interface ITimeOfDayService
{
    /// <summary>
    /// Current time in hours [0, 24)
    /// </summary>
    float CurrentTime { get; }

    /// <summary>
    /// Normalized time [0, 1)
    /// </summary>
    float NormalizedTime { get; }

    /// <summary>
    /// Length of a full day in real-time seconds
    /// </summary>
    float DayLength { get; }

    /// <summary>
    /// Current day phase (night/dawn/day/dusk)
    /// </summary>
    DayPhase CurrentPhase { get; }

    /// <summary>
    /// Fired whenever time changes (can be throttled)
    /// </summary>
    event Action<float> OnTimeChanged;

    /// <summary>
    /// Fired when phase changes (night → dawn → day → dusk)
    /// </summary>
    event Action<DayPhase> OnDayPhaseChanged;

    /// <summary>
    /// Advance time by deltaTime (authoritative tick)
    /// </summary>
    void Tick(float deltaTime);

    /// <summary>
    /// Set time explicitly (network sync, save/load, console commands)
    /// </summary>
    void SetTime(float hour);
}
