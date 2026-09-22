using System;

public enum WinchLineTransferOperation
{
    LoadOneFromPlayer = 0,
    UnloadToPlayer = 1
}

public enum WinchLinePlayerSourceKind
{
    None = 0,
    Hotbar = 1,
    Hands = 2
}

/// <summary>
/// Versioned request envelope for moving one tether-line ItemInstance between a
/// specific player and a specific winch line slot.
///
/// The request carries only stable/validatable facts. The authoritative side
/// resolves the live ItemInstance again from the authenticated requester and
/// refuses the transfer if the expected instance no longer occupies that source.
/// </summary>
[Serializable]
public sealed class WinchLineTransferIntentPayload
{
    public const int CurrentVersion = 1;
    public const string EffectSystem = "WinchLineTransfer";

    public int version = CurrentVersion;
    public WinchLineTransferOperation operation = WinchLineTransferOperation.LoadOneFromPlayer;
    public int winchSlotIndex = -1;

    // Used by LoadOneFromPlayer. Unload ignores these source fields.
    public WinchLinePlayerSourceKind playerSourceKind = WinchLinePlayerSourceKind.None;
    public int playerHotbarIndex = -1;

    // Load: expected ItemInstance currently in the requested player source.
    // Unload: expected ItemInstance currently in the requested winch slot.
    public string expectedInstanceId;
}
