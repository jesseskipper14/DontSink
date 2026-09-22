using System;

/// <summary>
/// Player/control-layer requests for a winch.
/// These are requests, not authoritative runtime state.
/// </summary>
public enum WinchControlIntent
{
    Stop = 0,
    Lower = 1,
    Raise = 2,
    QuickRelease = 3,
    CutLine = 4
}

/// <summary>
/// Small versioned envelope used when a cartridge emits a winch control request.
/// A future network transport can carry the same intent to host authority.
/// </summary>
[Serializable]
public sealed class WinchControlIntentPayload
{
    public const int CurrentVersion = 1;
    public const string EffectSystem = "WinchControl";

    public int version = CurrentVersion;
    public WinchControlIntent intent = WinchControlIntent.Stop;
}
