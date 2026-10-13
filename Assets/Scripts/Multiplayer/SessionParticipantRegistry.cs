using System;
using System.Collections.Generic;

// Host-owned identities. Transport connection IDs route messages; they are not save identities.
public sealed class SessionParticipantRegistry
{
    public sealed class Participant
    {
        public ulong ConnectionId { get; }
        public string ConnectionIdentity { get; }
        public string PersistenceIdentity { get; }
        public string CharacterIdentity { get; }
        public bool IsConnected { get; internal set; }
        internal bool Pending;

        internal Participant(string sessionId, ulong connectionId)
        {
            ConnectionId = connectionId;
            ConnectionIdentity = $"lan:{sessionId}:{connectionId}";
            PersistenceIdentity = "participant_" + Guid.NewGuid().ToString("N");
            CharacterIdentity = "character_" + Guid.NewGuid().ToString("N");
            Pending = true;
        }
    }

    private readonly Dictionary<ulong, Participant> connections = new();
    private readonly List<Participant> reserved = new();
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public IReadOnlyList<Participant> Participants => reserved.AsReadOnly();

    public bool TryApprove(ulong connectionId, int concurrentLimit, out Participant participant)
    {
        participant = null;
        if (connections.ContainsKey(connectionId) || concurrentLimit < 1) return false;
        int active = 0;
        foreach (var p in reserved) if (p.IsConnected || p.Pending) active++;
        if (active >= concurrentLimit) return false;
        participant = new Participant(SessionId, connectionId);
        connections.Add(connectionId, participant);
        reserved.Add(participant);
        return true;
    }

    public bool MarkConnected(ulong connectionId, out Participant participant)
    {
        if (!connections.TryGetValue(connectionId, out participant)) return false;
        participant.Pending = false;
        participant.IsConnected = true;
        return true;
    }

    public bool TryResolveConnectedSender(ulong connectionId, out Participant participant)
    {
        return connections.TryGetValue(connectionId, out participant) && participant.IsConnected;
    }

    public void MarkDisconnected(ulong connectionId)
    {
        if (!connections.Remove(connectionId, out var participant)) return;
        participant.IsConnected = false;
        participant.Pending = false;
        // Keep the record reserved for this session. Reclaiming it will require verified
        // reconnect credentials; a new connection cannot request an arbitrary saved identity.
    }
}
