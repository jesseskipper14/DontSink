using System;

// Transport identity/actor ownership are checked by the caller. This gate rejects
// invalid/replayed input and bounds its lifetime; clients never send a position.
public sealed class CharacterNetworkInputGate
{
    public const ushort AllowedButtons = 255;
    public const double Timeout = 0.3;
    private bool received;
    private uint lastSequence;
    private double lastReceived, allowanceAt;
    private double allowance = 4;

    public bool IsFresh(double now) => received && now - lastReceived <= Timeout;

    public bool TryAccept(uint sequence, float moveX, ushort buttons, double now)
    {
        if (float.IsNaN(moveX) || float.IsInfinity(moveX) || Math.Abs(moveX) > 1 ||
            (buttons & ~AllowedButtons) != 0 || double.IsNaN(now) || double.IsInfinity(now)) return false;
        if (received && unchecked((int)(sequence - lastSequence)) <= 0) return false;
        allowance = Math.Min(4, allowance + Math.Max(0, now - allowanceAt) * 60);
        allowanceAt = now;
        if (allowance < 1) return false;
        allowance -= 1;
        received = true;
        lastSequence = sequence;
        lastReceived = now;
        return true;
    }
}
