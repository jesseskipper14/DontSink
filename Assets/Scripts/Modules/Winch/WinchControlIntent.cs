using System;

public enum WinchControlIntent
{
    Stop = 0,
    Lower = 1,
    Raise = 2,
    QuickRelease = 3,
    CutLine = 4
}

/// <summary>
/// Describes how a physical/UI control is being operated.
/// Press is a one-shot command used by powered automatic controls and mechanical
/// emergency actions. HoldBegin / HoldEnd bracket an unpowered manual-control
/// session.
/// </summary>
public enum WinchControlInputPhase
{
    Press = 0,
    HoldBegin = 1,
    HoldEnd = 2
}

/// <summary>
/// Versioned control request envelope. This remains intent, not authoritative
/// winch state. A future transport can carry the same payload from client to host.
/// </summary>
[Serializable]
public sealed class WinchControlIntentPayload
{
    public const int CurrentVersion = 2;
    public const string EffectSystem = "WinchControl";

    public int version = CurrentVersion;
    public WinchControlIntent intent = WinchControlIntent.Stop;
    public WinchControlInputPhase phase = WinchControlInputPhase.Press;
}
