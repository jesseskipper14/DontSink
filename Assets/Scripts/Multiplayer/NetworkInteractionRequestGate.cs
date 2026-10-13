using System;

public enum NetworkInteractionRequestDisposition { New, Replay, Stale, RateLimited }

// One ordered reliable request stream per connected participant and world incarnation.
// Repeating the last request returns its result; older requests never run again.
public sealed class NetworkInteractionRequestGate
{
    private uint lastId;
    private double lastTime;
    private double tokens = 8;
    public bool LastSucceeded { get; private set; }
    public string LastReason { get; private set; }

    public NetworkInteractionRequestDisposition Begin(uint id, double now)
    {
        if (id == 0) return NetworkInteractionRequestDisposition.Stale;
        if (lastId != 0 && id == lastId) return NetworkInteractionRequestDisposition.Replay;
        if (lastId != 0 && unchecked((int)(id - lastId)) <= 0) return NetworkInteractionRequestDisposition.Stale;
        tokens = Math.Min(8, tokens + Math.Max(0, now - lastTime) * 10);
        lastTime = now;
        lastId = id;
        LastSucceeded = false;
        LastReason = "Too many requests.";
        if (tokens < 1) return NetworkInteractionRequestDisposition.RateLimited;
        tokens--;
        return NetworkInteractionRequestDisposition.New;
    }

    public void Complete(bool succeeded, string reason)
    {
        LastSucceeded = succeeded;
        LastReason = reason;
    }
}
