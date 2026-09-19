using System;

/// <summary>
/// Player requests for the handheld sounding line.
/// These are requests, not authoritative runtime state.
/// </summary>
public enum HandheldSoundingLineIntent
{
    Deploy = 0,
    Retrieve = 1,
    Release = 2
}

/// <summary>
/// Small versioned request envelope so the same intent can later travel
/// client -> host without changing the sounding-line authority itself.
/// </summary>
[Serializable]
public struct HandheldSoundingLineIntentRequest
{
    public const int CurrentVersion = 1;

    public int version;
    public HandheldSoundingLineIntent intent;

    public static HandheldSoundingLineIntentRequest Create(
        HandheldSoundingLineIntent intent)
    {
        return new HandheldSoundingLineIntentRequest
        {
            version = CurrentVersion,
            intent = intent
        };
    }
}
