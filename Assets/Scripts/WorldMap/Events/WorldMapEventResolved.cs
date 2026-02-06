using System;

public readonly struct WorldMapEventResolved
{
    public readonly int sourceNodeId;
    public readonly EventOutcome outcome;
    public readonly string eventId;
    public readonly string eventName;

    public WorldMapEventResolved(int sourceNodeId, EventOutcome outcome, string eventId, string eventName)
    {
        this.sourceNodeId = sourceNodeId;
        this.outcome = outcome;
        this.eventId = eventId;
        this.eventName = eventName;
    }
}
