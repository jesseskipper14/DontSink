using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public sealed class NetworkWorldBootstrapSnapshot
{
    public int protocolVersion = 1;
    public string sessionId;
    public string sceneName;
    public WorldMapSaveSnapshot world;
    public NetworkGameplayStartState gameplay;
}

[Serializable]
public sealed class NetworkGameplayStartState
{
    public WorldMapPlayerState player;
    public BoatSaveState boat;
    public TimeOfDaySnapshot time;
    public CelestialChartStateSnapshot celestial;
    public WorldNavigationState navigation;
}

// Staging only: receiving a world never loads a scene, regenerates terrain,
// applies a disk save, or changes the active GameState. Scene/entity bootstrap
// must explicitly consume this validated snapshot in its next checkpoint.
public sealed class NetworkWorldBootstrap : IDisposable
{
    private const string BeginMessage = "DontSink/World/Begin/1", ChunkMessage = "DontSink/World/Chunk/1",
        EndMessage = "DontSink/World/End/1", ReadyMessage = "DontSink/World/Ready/1";
    public const int ChunkBytes = 16384, MaxCompressedBytes = 16 * 1024 * 1024, MaxRawBytes = 64 * 1024 * 1024;
    private const double TimeoutSeconds = 90;
    private readonly DontSinkNetworkSession session;
    private readonly NetworkManager manager;
    private readonly Dictionary<ulong, int> outgoing = new();
    private readonly HashSet<ulong> ready = new();
    private readonly Dictionary<ulong, double> waiting = new();
    private byte[] encoded, incoming;
    private uint epoch;
    private int received, expectedRaw;
    private string digest, incomingSession, incomingScene;
    private bool ended;
    private double deadline;
    public NetworkWorldBootstrapSnapshot Prepared { get; private set; }
    public uint Epoch => epoch;
    public string Status { get; private set; } = "No world prepared.";
    public bool IsReady(ulong clientId) => ready.Contains(clientId);
    public bool Failed { get; private set; }
    public bool AllConnectedReady
    {
        get
        {
            if (!manager.IsServer || Failed || Prepared == null || session.Registry == null) return false;
            foreach (var participant in session.Registry.Participants)
                if (participant.IsConnected && !ready.Contains(participant.ConnectionId)) return false;
            return true;
        }
    }

    public NetworkWorldBootstrap(DontSinkNetworkSession owner, NetworkManager networkManager)
    {
        session = owner; manager = networkManager;
        manager.CustomMessagingManager.RegisterNamedMessageHandler(BeginMessage, Begin);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(ChunkMessage, Chunk);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(EndMessage, End);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(ReadyMessage, Ready);
    }
    public bool Prepare(WorldMapSaveSnapshot world, string sceneName, out string reason, NetworkGameplayStartState gameplay = null)
    {
        reason = null;
        if (!manager.IsServer || session.Registry == null) { reason = "Only the host prepares worlds."; return false; }
        try
        {
            var snapshot = new NetworkWorldBootstrapSnapshot { sessionId = session.SessionId, sceneName = sceneName, world = world, gameplay = gameplay };
            Validate(snapshot);
            byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));
            if (raw.Length > MaxRawBytes) throw new InvalidDataException("World exceeds the 64 MiB bootstrap budget.");
            using var compressed = new MemoryStream();
            using (var zipper = new DeflateStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true)) zipper.Write(raw, 0, raw.Length);
            if (compressed.Length > MaxCompressedBytes) throw new InvalidDataException("Compressed world exceeds 16 MiB.");
            // Detach from live simulation: subsequent world changes cannot alter
            // this epoch's packet or the host's prepared snapshot.
            var detached = JsonUtility.FromJson<NetworkWorldBootstrapSnapshot>(Encoding.UTF8.GetString(raw));
            byte[] nextEncoded = compressed.ToArray();
            string nextDigest = Hash(nextEncoded);
            encoded = nextEncoded; digest = nextDigest; expectedRaw = raw.Length;
            Prepared = detached; epoch++; if (epoch == 0) epoch = 1;
            outgoing.Clear(); ready.Clear(); waiting.Clear(); Failed = false; ready.Add(manager.LocalClientId);
            foreach (var participant in session.Registry.Participants)
                if (participant.IsConnected) Connected(participant.ConnectionId);
            Status = "Host world staged; awaiting participant acknowledgements.";
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is ArgumentException || ex is IOException || ex is FormatException)
        { reason = ex.Message; return false; }
    }
    public void Connected(ulong clientId)
    {
        if (!manager.IsServer || Prepared == null || clientId == manager.LocalClientId ||
            !session.Registry.TryResolveConnectedSender(clientId, out _)) return;
        ready.Remove(clientId); outgoing[clientId] = 0;
        waiting[clientId] = Time.realtimeSinceStartupAsDouble + TimeoutSeconds;
        using var writer = new FastBufferWriter(512, Allocator.Temp);
        writer.WriteValueSafe(epoch);
        writer.WriteValueSafe(new FixedString64Bytes(Prepared.sessionId));
        writer.WriteValueSafe(new FixedString64Bytes(Prepared.sceneName));
        writer.WriteValueSafe(encoded.Length); writer.WriteValueSafe(expectedRaw);
        writer.WriteValueSafe(new FixedString128Bytes(digest));
        Send(BeginMessage, clientId, writer);
    }
    public void Disconnected(ulong clientId) { outgoing.Remove(clientId); ready.Remove(clientId); waiting.Remove(clientId); }
    public void Tick()
    {
        if (!manager.IsListening) return;
        if (manager.IsServer)
        {
            if (Prepared == null) return;
            if (outgoing.Count == 0 && waiting.Count == 0)
            {
                if (AllConnectedReady) Status = "Host world acknowledged by all connected participants.";
                return;
            }
            foreach (var clientId in new List<ulong>(waiting.Keys))
                if (Time.realtimeSinceStartupAsDouble > waiting[clientId])
                {
                    waiting.Remove(clientId); outgoing.Remove(clientId); Failed = true;
                    Status = "Participant " + clientId + " did not acknowledge the staged world; scene entry remains blocked.";
                }
            // Fixed total per-frame budget across participants; no one-frame
            // broadcast of a many-megabyte world on every client connection.
            int budget = 8;
            foreach (var clientId in new List<ulong>(outgoing.Keys))
            {
                if (budget == 0) break;
                if (!session.Registry.TryResolveConnectedSender(clientId, out _)) { Disconnected(clientId); continue; }
                int offset = outgoing[clientId];
                while (offset < encoded.Length && budget-- > 0)
                {
                    int length = Math.Min(ChunkBytes, encoded.Length - offset);
                    using var writer = new FastBufferWriter(length + 16, Allocator.Temp);
                    writer.WriteValueSafe(epoch); writer.WriteValueSafe(offset); writer.WriteValueSafe(length);
                    writer.WriteBytesSafe(encoded, length, offset);
                    Send(ChunkMessage, clientId, writer); offset += length;
                }
                outgoing[clientId] = offset;
                if (offset == encoded.Length)
                {
                    using var writer = new FastBufferWriter(8, Allocator.Temp);
                    writer.WriteValueSafe(epoch); Send(EndMessage, clientId, writer); outgoing.Remove(clientId);
                }
                if (budget < 0) budget = 0;
            }
            if (AllConnectedReady) Status = "Host world acknowledged by all connected participants.";
        }
        else if (incoming != null)
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) { Fail("World transfer timed out."); return; }
            if (ended && received == incoming.Length && session.HasLocalParticipant) Complete();
        }
    }
    private void Begin(ulong sender, FastBufferReader reader)
    {
        if (manager.IsServer || sender != NetworkManager.ServerClientId) return;
        try
        {
            reader.ReadValueSafe(out uint nextEpoch);
            reader.ReadValueSafe(out FixedString64Bytes sessionId); reader.ReadValueSafe(out FixedString64Bytes scene);
            reader.ReadValueSafe(out int length); reader.ReadValueSafe(out int rawLength);
            reader.ReadValueSafe(out FixedString128Bytes hash);
            if (nextEpoch == 0 || (epoch != 0 && unchecked((int)(nextEpoch - epoch)) <= 0)) return;
            if (length < 1 || length > MaxCompressedBytes || rawLength < 1 || rawLength > MaxRawBytes ||
                !Guid.TryParseExact(sessionId.ToString(), "N", out _) || !AllowedScene(scene.ToString()) || hash.Length != 64)
                throw new InvalidDataException("Invalid world transfer header.");
            if (session.HasLocalParticipant && sessionId.ToString() != session.SessionId)
                throw new InvalidDataException("World belongs to a different session.");
            epoch = nextEpoch; Failed = false; incomingSession = sessionId.ToString(); incomingScene = scene.ToString();
            digest = hash.ToString(); expectedRaw = rawLength; received = 0; ended = false; Prepared = null;
            incoming = new byte[length]; deadline = Time.realtimeSinceStartupAsDouble + TimeoutSeconds;
            Status = "Receiving host world.";
        }
        catch (Exception ex) { Fail("Invalid world header: " + ex.Message); }
    }
    private void Chunk(ulong sender, FastBufferReader reader)
    {
        if (manager.IsServer || sender != NetworkManager.ServerClientId || incoming == null) return;
        try
        {
            reader.ReadValueSafe(out uint packetEpoch); reader.ReadValueSafe(out int offset); reader.ReadValueSafe(out int length);
            if (packetEpoch != epoch) return;
            if (ended || offset != received || length < 1 || length > ChunkBytes || length > incoming.Length - received)
                throw new InvalidDataException("Out-of-order or oversized world chunk.");
            reader.ReadBytesSafe(ref incoming, length, offset); received += length;
            Status = "Receiving host world: " + received + " / " + incoming.Length + " bytes.";
        }
        catch (Exception ex) { Fail("Invalid world chunk: " + ex.Message); }
    }
    private void End(ulong sender, FastBufferReader reader)
    {
        if (manager.IsServer || sender != NetworkManager.ServerClientId || incoming == null) return;
        try
        {
            reader.ReadValueSafe(out uint packetEpoch); if (packetEpoch != epoch) return;
            if (received != incoming.Length) throw new InvalidDataException("Incomplete world transfer.");
            ended = true;
        }
        catch (Exception ex) { Fail("Invalid world completion: " + ex.Message); }
    }
    private void Complete()
    {
        try
        {
            if (incomingSession != session.SessionId || Hash(incoming) != digest) throw new InvalidDataException("World session/checksum mismatch.");
            using var source = new MemoryStream(incoming, false);
            using var zipper = new DeflateStream(source, CompressionMode.Decompress);
            using var raw = new MemoryStream();
            byte[] block = new byte[32768]; int count;
            while ((count = zipper.Read(block, 0, block.Length)) > 0)
            {
                if (raw.Length + count > expectedRaw) throw new InvalidDataException("World expansion exceeds its declared budget.");
                raw.Write(block, 0, count);
            }
            if (raw.Length != expectedRaw) throw new InvalidDataException("World length mismatch.");
            var snapshot = JsonUtility.FromJson<NetworkWorldBootstrapSnapshot>(Encoding.UTF8.GetString(raw.ToArray()));
            Validate(snapshot);
            if (snapshot.sessionId != incomingSession || snapshot.sceneName != incomingScene)
                throw new InvalidDataException("World descriptor mismatch.");
            Prepared = snapshot; incoming = null;
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteValueSafe(epoch); writer.WriteValueSafe(new FixedString64Bytes(session.SessionId));
            writer.WriteValueSafe(new FixedString128Bytes(digest)); Send(ReadyMessage, NetworkManager.ServerClientId, writer);
            Status = "Host world validated and staged for " + snapshot.sceneName + ".";
        }
        catch (Exception ex) { Fail("Could not stage host world: " + ex.Message); }
    }
    private void Ready(ulong sender, FastBufferReader reader)
    {
        if (!manager.IsServer || Prepared == null || !session.Registry.TryResolveConnectedSender(sender, out _)) return;
        try
        {
            reader.ReadValueSafe(out uint packetEpoch); reader.ReadValueSafe(out FixedString64Bytes sessionId);
            reader.ReadValueSafe(out FixedString128Bytes hash);
            if (packetEpoch == epoch && sessionId.ToString() == session.SessionId && hash.ToString() == digest &&
                !outgoing.ContainsKey(sender) && waiting.ContainsKey(sender))
            { ready.Add(sender); waiting.Remove(sender); }
        }
        catch (Exception) { /* Invalid client acknowledgements never grant readiness. */ }
    }
    private void Send(string name, ulong clientId, FastBufferWriter writer) =>
        manager.CustomMessagingManager.SendNamedMessage(name, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
    private void Fail(string reason) { incoming = null; Prepared = null; Failed = true; Status = reason; }
    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    private static bool AllowedScene(string scene) => scene == "NodeScene" || scene == "BoatScene";
    public static void Validate(NetworkWorldBootstrapSnapshot snapshot)
    {
        var world = snapshot?.world; var topo = world?.topography;
        if (snapshot == null || snapshot.protocolVersion != 1 || !Guid.TryParseExact(snapshot.sessionId, "N", out _) ||
            !AllowedScene(snapshot.sceneName) || world?.version != 4 || world.graph?.version != 1 || !world.HasPersistedWorld)
            throw new InvalidDataException("Incompatible world/bootstrap identity or graph.");
        if (topo == null || topo.version < 1 || topo.version > 2 || topo.mode != WorldMapTopographySaveMode.RawHeightData || topo.width < 2 || topo.height < 2 ||
            topo.width > 4096 || topo.height > 4096 || !topo.HasRawHeightData ||
            !float.IsFinite(topo.worldBoundsX) || !float.IsFinite(topo.worldBoundsY) ||
            !float.IsFinite(topo.worldBoundsWidth) || !float.IsFinite(topo.worldBoundsHeight) || topo.worldBoundsWidth <= 0 || topo.worldBoundsHeight <= 0)
            throw new InvalidDataException("Bootstrap requires portable raw host topography, not a local asset or seed-only regeneration.");
        var heights = topo.CopyHeight01();
        if (heights?.Length != topo.width * topo.height) throw new InvalidDataException("Malformed host height field.");
        foreach (float height in heights)
            if (!float.IsFinite(height) || height < 0 || height > 1) throw new InvalidDataException("Invalid host height value.");
        if (world.graph.nodes.Count > 20000) throw new InvalidDataException("World node budget exceeded.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in world.graph.nodes)
            if (node == null || string.IsNullOrWhiteSpace(node.stableId) || !ids.Add(node.stableId) ||
                !float.IsFinite(node.positionX) || !float.IsFinite(node.positionY)) throw new InvalidDataException("Invalid/duplicate world node identity or position.");
    }
    public void Dispose()
    {
        if (manager != null && manager.CustomMessagingManager != null)
            foreach (var name in new[] { BeginMessage, ChunkMessage, EndMessage, ReadyMessage }) manager.CustomMessagingManager.UnregisterNamedMessageHandler(name);
        outgoing.Clear(); ready.Clear(); waiting.Clear(); incoming = encoded = null; Prepared = null;
    }
}
