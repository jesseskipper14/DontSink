using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-11000)]
[DisallowMultipleComponent]
public sealed class DontSinkNetworkSession : MonoBehaviour
{
    private const string Protocol = "DontSink/node-loose-items-throw/14";
    private const string WelcomeMessage = "DontSink/Participant/1";
    private const string PlaytestMessage = "DontSink/MovementPlaytest/2";
    public static DontSinkNetworkSession Instance { get; private set; }
    public static bool IsSessionActive => Instance != null && Instance.roleBound;

    [SerializeField, Min(1)] private int concurrentParticipantLimit = 8;
    [SerializeField] private bool showDevelopmentPanel = true;
    private NetworkManager manager;
    private UnityTransport transport;
    private bool roleBound, leaving;
    private GameObject playerPrefab;
    private GameObject interactionPrefab;
    private GameObject boatPlaytestPrefab;
    private GameObject sceneBoatPrefab;
    private MultiplayerMovementPlaytest playtest;
    private readonly Dictionary<ulong, NetworkPlayerActor> actors = new();
    public int SpawnedPlayerCount => actors.Count;
    public IEnumerable<NetworkPlayerActor> SpawnedPlayers => actors.Values;
    public bool MovementPlaytestActive => playtest != null;
    public NetworkSharedInteractionWorld InteractionWorld { get; private set; }
    public NetworkBoatPlaytest BoatWorld { get; private set; }
    public NetworkWorldBootstrap WorldBootstrap { get; private set; }
    public NetworkGameplaySceneBootstrap GameplayScene { get; private set; }
    private string address = "127.0.0.1", portText = "7777";
    private bool panelOpen;
    private string status = "Offline";
    public string Status => status;
    public SessionParticipantRegistry Registry { get; private set; }
    public string LocalPersistenceIdentity { get; private set; }
    public string LocalCharacterIdentity { get; private set; }
    public string SessionId { get; private set; }
    public bool IsHost => manager != null && manager.IsHost;
    public bool HasLocalParticipant => !string.IsNullOrEmpty(LocalPersistenceIdentity);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallMenuSession()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu" && Instance == null)
            new GameObject("_MultiplayerSession").AddComponent<DontSinkNetworkSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // Do not silently take over an unrelated authored NetworkManager.
        if (NetworkManager.Singleton != null)
        {
            status = "Another NetworkManager exists. Remove the duplicate session setup.";
            return;
        }
        transport = gameObject.AddComponent<UnityTransport>();
        manager = gameObject.AddComponent<NetworkManager>();
        manager.NetworkConfig ??= new NetworkConfig();
        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Protocol);
        // MP.2 tests real spawned actors without migrating generated game scenes yet.
        manager.NetworkConfig.EnableSceneManagement = false;
        playerPrefab = Resources.Load<GameObject>("Prefabs/Agents/CharacterPlayer");
        if (playerPrefab != null && playerPrefab.GetComponent<NetworkPlayerActor>() != null &&
            playerPrefab.GetComponent<NetworkObject>() != null) manager.AddNetworkPrefab(playerPrefab);
        else playerPrefab = null;
        interactionPrefab = Resources.Load<GameObject>("Prefabs/Multiplayer/SharedInteractionPlaytest");
        if (interactionPrefab != null) manager.AddNetworkPrefab(interactionPrefab);
        boatPlaytestPrefab = Resources.Load<GameObject>("Prefabs/Multiplayer/SharedBoatPlaytest");
        if (boatPlaytestPrefab != null) manager.AddNetworkPrefab(boatPlaytestPrefab);
        sceneBoatPrefab = Resources.Load<GameObject>("Prefabs/Multiplayer/SharedSceneBoat");
        if (sceneBoatPrefab != null) manager.AddNetworkPrefab(sceneBoatPrefab);
        manager.ConnectionApprovalCallback = Approve;
        manager.OnClientConnectedCallback += Connected;
        manager.OnClientDisconnectCallback += Disconnected;
        manager.OnTransportFailure += TransportFailed;
    }

    public bool StartHost(ushort port = 7777)
    {
        if (!CanStart() || port == 0) return false;
        Registry = new SessionParticipantRegistry();
        SessionId = Registry.SessionId;
        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
        return StartSession(true);
    }

    public bool Join(string targetAddress, ushort port = 7777)
    {
        if (!CanStart() || string.IsNullOrWhiteSpace(targetAddress) || port == 0) return false;
        Registry = null;
        transport.SetConnectionData(targetAddress.Trim(), port);
        return StartSession(false);
    }

    private bool CanStart() => manager != null && !roleBound && !manager.IsListening && !manager.ShutdownInProgress;

    private bool StartSession(bool host)
    {
        if (!GameplayAuthority.TryBindSessionRole(this, host))
        {
            status = "Another session owns gameplay authority.";
            return false;
        }
        roleBound = true;
        status = host ? "Starting host..." : "Connecting...";
        try
        {
            if (host ? manager.StartHost() : manager.StartClient())
            {
                manager.CustomMessagingManager.RegisterNamedMessageHandler(WelcomeMessage, ReceiveWelcome);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(PlaytestMessage, ReceivePlaytest);
                WorldBootstrap = new NetworkWorldBootstrap(this, manager);
                GameplayScene = new NetworkGameplaySceneBootstrap(this, manager);
                return true;
            }
            status = "Transport could not start.";
        }
        catch (Exception ex)
        {
            status = "Transport failed to start: " + ex.Message;
            Debug.LogException(ex);
        }
        Leave();
        return false;
    }

    private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false;
        response.Pending = false;
        bool validProtocol = request.Payload != null &&
            request.Payload.Length <= 128 && Encoding.UTF8.GetString(request.Payload) == Protocol;
        response.Approved = validProtocol && Registry != null &&
            Registry.TryApprove(request.ClientNetworkId, concurrentParticipantLimit, out _);
        response.Reason = response.Approved ? "" : "Incompatible session protocol or session full.";
    }

    private void Connected(ulong clientId)
    {
        if (manager.IsServer && Registry != null && Registry.MarkConnected(clientId, out var participant))
        {
            if (clientId == manager.LocalClientId)
                ApplyWelcome(Registry.SessionId, participant.PersistenceIdentity, participant.CharacterIdentity);
            else
            {
                using var writer = new FastBufferWriter(1024, Allocator.Temp);
                writer.WriteValueSafe(Registry.SessionId);
                writer.WriteValueSafe(participant.PersistenceIdentity);
                writer.WriteValueSafe(participant.CharacterIdentity);
                manager.CustomMessagingManager.SendNamedMessage(WelcomeMessage, clientId, writer);
            }
        }
        if (!manager.IsServer && clientId == manager.LocalClientId)
            status = "Connected; awaiting host participant assignment.";
        if (manager.IsServer && playtest != null)
        {
            SendPlaytest(clientId);
            SpawnParticipant(clientId);
        }
        if (manager.IsServer) WorldBootstrap?.Connected(clientId);
    }

    private void Update() { WorldBootstrap?.Tick(); GameplayScene?.Tick(); }
    public void ReportSessionFailure(string reason) { status = reason; Debug.LogError(reason, this); Leave(); }
    public bool CanStartHostedGame(string sceneName, out string reason)
    {
        reason = "Shared scene boat/player prefabs are unavailable.";
        return !leaving && sceneBoatPrefab != null && playerPrefab != null && GameplayScene != null && GameplayScene.CanStart(sceneName, out reason);
    }
    public void StartHostedGame(string sceneName) => GameplayScene.Start(sceneName);
    public void SpawnSceneBoat()
    {
        if (!IsHost || GameplayScene.SceneBoat == null || GameplayScene.Replication != null) return;
        Instantiate(sceneBoatPrefab).GetComponent<NetworkObject>().Spawn();
    }
    public void SpawnGameplayParticipant(ulong clientId) => SpawnParticipant(clientId);

    public bool PrepareCurrentWorld(string sceneName = "NodeScene")
    {
        if (!IsHost || leaving || WorldBootstrap == null) return false;
        if (GameState.I == null || !WorldMapSaveRestorer.HasUsableSnapshot())
        { status = "Generate a host world before staging it for connected scenes."; return false; }
        bool result = WorldBootstrap.Prepare(GameState.I.worldMapSnapshot, sceneName, out string reason);
        status = result ? WorldBootstrap.Status : reason;
        return result;
    }

    public bool StartMovementPlaytest()
    {
        if (!IsHost || leaving || GameplayScene?.IsActive == true || playtest != null || playerPrefab == null) return false;
        EnsurePlaytest();
        foreach (var participant in Registry.Participants)
        {
            if (!participant.IsConnected) continue;
            SendPlaytest(participant.ConnectionId);
            SpawnParticipant(participant.ConnectionId);
        }
        return true;
    }

    private void SendPlaytest(ulong clientId)
    {
        if (clientId == manager.LocalClientId) return;
        using var writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe(concurrentParticipantLimit);
        manager.CustomMessagingManager.SendNamedMessage(PlaytestMessage, clientId, writer);
    }

    private void ReceivePlaytest(ulong sender, FastBufferReader reader)
    {
        if (manager.IsServer || sender != NetworkManager.ServerClientId || leaving) return;
        reader.ReadValueSafe(out int capacity);
        EnsurePlaytest(Mathf.Clamp(capacity, 1, 1024));
    }

    private void EnsurePlaytest(int capacity = 0)
    {
        if (playtest != null) return;
        playtest = new MultiplayerMovementPlaytest(capacity > 0 ? capacity : concurrentParticipantLimit);
        if (BoatWorld != null) playtest.UseBoatDeck();
        foreach (var actor in actors.Values) if (actor != null) playtest.Present(actor);
    }

    private void SpawnParticipant(ulong clientId)
    {
        if (!manager.IsServer || actors.ContainsKey(clientId) ||
            !Registry.TryResolveConnectedSender(clientId, out var participant)) return;
        // Use an unoccupied spawn, not a client-provided coordinate or hierarchy identity.
        int slot = 0;
        Vector2 spawn;
        do
        {
            float x = slot == 0 ? 0 : ((slot + 1) / 2) * 3 * (slot % 2 == 1 ? 1 : -1);
            spawn = MultiplayerMovementPlaytest.Origin + new Vector2(x, 1.5f);
        if (BoatWorld != null) spawn = BoatWorld.SpawnPoint(slot);
            if (GameplayScene?.IsActive == true) spawn = GameplayScene.SpawnPoint(slot);
            slot++;
        } while (Physics2D.OverlapBox(spawn, new Vector2(1.5f, 2), 0, 1 << LayerMask.NameToLayer("Player")) != null && slot < concurrentParticipantLimit);
        var instance = Instantiate(playerPrefab, spawn, Quaternion.identity);
        var actor = instance.GetComponent<NetworkPlayerActor>();
        actor.Initialize(participant);
        actors.Add(clientId, actor);
        instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    public bool IsParticipantActor(ulong sender, NetworkPlayerActor actor)
        => actors.TryGetValue(sender, out var registered) && registered == actor;

    public bool TryGetParticipantActor(ulong sender, out NetworkPlayerActor actor)
        => actors.TryGetValue(sender, out actor) && actor != null && actor.IsSpawned;

    public bool StartInteractionPlaytest()
    {
        if (GameplayScene?.IsActive == true) return false;
        if (!IsHost || leaving || InteractionWorld != null || interactionPrefab == null) return false;
        if (playtest == null && !StartMovementPlaytest()) return false;
        var instance = Instantiate(interactionPrefab, MultiplayerMovementPlaytest.Origin, Quaternion.identity);
        instance.GetComponent<NetworkObject>().Spawn();
        return true;
    }

    public bool StartBoatPlaytest()
    {
        if (GameplayScene?.IsActive == true) return false;
        if (!IsHost || leaving || BoatWorld != null || InteractionWorld != null || boatPlaytestPrefab == null || interactionPrefab == null)
            return false;
        if (PhysicsManager.Instance == null || PhysicsManager.Instance.globals == null)
        { status = "The sailing test requires this scene's PhysicsManager and PhysicsGlobals asset."; return false; }
        if (playtest == null && !StartMovementPlaytest()) return false;
        var instance = Instantiate(boatPlaytestPrefab, MultiplayerMovementPlaytest.Origin, Quaternion.identity);
        instance.GetComponent<NetworkObject>().Spawn();
        int slot = 0;
        foreach (var actor in actors.Values) BoatWorld.Present(actor, true, slot++);
        return StartInteractionPlaytest();
    }

    public void BindBoatWorld(NetworkBoatPlaytest world)
    {
        BoatWorld = world;
        if (world == null) return;
        playtest?.UseBoatDeck();
        foreach (var actor in actors.Values) world.Present(actor);
    }

    public void BindInteractionWorld(NetworkSharedInteractionWorld world) => InteractionWorld = world;

    public void PlayerSpawned(NetworkPlayerActor actor)
    {
        actors[actor.OwnerClientId] = actor;
        // During late-join NGO may synchronize existing actors before the fixture
        // message arrives. Bind them when the host's fixture configuration arrives.
        playtest?.Present(actor);
        InteractionWorld?.Present(actor);
        BoatWorld?.Present(actor);
        GameplayScene?.Present(actor);
    }

    public void PlayerDespawned(NetworkPlayerActor actor)
    {
        if (manager != null && manager.IsServer)
        {
            GameplayScene?.Replication?.ReleaseParticipant(actor.OwnerClientId);
            InteractionWorld?.ReleaseParticipant(actor.OwnerClientId);
            BoatWorld?.ReleaseParticipant(actor.OwnerClientId);
        }
        if (IsParticipantActor(actor.OwnerClientId, actor)) actors.Remove(actor.OwnerClientId);
        playtest?.PlayerRemoved(actor);
    }

    private void ReceiveWelcome(ulong sender, FastBufferReader reader)
    {
        if (manager.IsServer || sender != NetworkManager.ServerClientId) return;
        try
        {
            reader.ReadValueSafe(out string session);
            reader.ReadValueSafe(out string persistence);
            reader.ReadValueSafe(out string character);
            if (session.Length != 32 || persistence.Length > 80 || character.Length > 80 ||
                !persistence.StartsWith("participant_", StringComparison.Ordinal) ||
                !character.StartsWith("character_", StringComparison.Ordinal))
                throw new InvalidOperationException("Malformed participant assignment.");
            ApplyWelcome(session, persistence, character);
        }
        catch (Exception ex)
        {
            status = "Invalid host participant assignment: " + ex.Message;
            Leave();
        }
    }

    private void ApplyWelcome(string session, string persistence, string character)
    {
        SessionId = session;
        LocalPersistenceIdentity = persistence;
        LocalCharacterIdentity = character;
        status = manager.IsHost ? "Hosting — participant assigned" : "Connected — participant assigned";
    }

    private void Disconnected(ulong clientId)
    {
        GameplayScene?.Replication?.ReleaseParticipant(clientId);
        WorldBootstrap?.Disconnected(clientId);
        GameplayScene?.Disconnected(clientId);
        if (manager.IsServer) InteractionWorld?.ReleaseParticipant(clientId);
        if (manager.IsServer) BoatWorld?.ReleaseParticipant(clientId);
        if (manager.IsServer) Registry?.MarkDisconnected(clientId);
        if (clientId != manager.LocalClientId || leaving) return;
        status = string.IsNullOrEmpty(manager.DisconnectReason) ? "Disconnected" : manager.DisconnectReason;
        Leave();
    }

    private void TransportFailed()
    {
        status = "Transport failure";
        Leave();
    }

    public void Leave()
    {
        if (!roleBound || leaving) return;
        leaving = true;
        if (manager != null) manager.Shutdown();
        StartCoroutine(FinishLeave());
    }

    private IEnumerator FinishLeave()
    {
        while (manager != null && (manager.IsListening || manager.ShutdownInProgress)) yield return null;
        bool returnToMenu = GameplayScene?.IsActive == true;
        GameplayScene?.Dispose(); GameplayScene = null;
        WorldBootstrap?.Dispose(); WorldBootstrap = null;
        GameplayAuthority.ReleaseSessionRole(this);
        roleBound = leaving = false;
        playtest?.Dispose();
        playtest = null;
        InteractionWorld = null;
        BoatWorld = null;
        actors.Clear();
        Registry = null;
        SessionId = LocalPersistenceIdentity = LocalCharacterIdentity = null;
        status = "Offline — " + status;
        if (returnToMenu)
        {
            GameState.I?.SetLocalPlayerPersistenceKey(GameState.DefaultPlayerPersistenceKey, "Left multiplayer");
            SceneManager.LoadScene("MainMenu");
        }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (manager != null)
        {
            manager.OnClientConnectedCallback -= Connected;
            manager.OnClientDisconnectCallback -= Disconnected;
            manager.OnTransportFailure -= TransportFailed;
            manager.Shutdown();
        }
        GameplayAuthority.ReleaseSessionRole(this);
        playtest?.Dispose();
        WorldBootstrap?.Dispose(); WorldBootstrap = null;
        GameplayScene?.Dispose(); GameplayScene = null;
        Instance = null;
    }

    private void OnGUI()
    {
        if (!showDevelopmentPanel) return;
        float width = Mathf.Min(410f, Screen.width - 20f);
        if (GUI.Button(new Rect(Screen.width - width - 10f, 10f, width, 28f), "Multiplayer — Development")) panelOpen = !panelOpen;
        if (!panelOpen) return;
        GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 44f, width, 510f), GUI.skin.box);
        GUILayout.Label("MP.2 / MP.3 / MP.4: connected development tests.");
        GUILayout.Label(status);
        if (GameplayScene?.IsActive == true) GUILayout.Label(GameplayScene.Status);
        if (WorldBootstrap?.Prepared != null) GUILayout.Label(WorldBootstrap.Status);
        GUILayout.Label("LAN / local testing. No Steam authentication yet.");
        GUILayout.Label("Address"); address = GUILayout.TextField(address);
        GUILayout.Label("Port"); portText = GUILayout.TextField(portText);
        bool validPort = ushort.TryParse(portText, out ushort port) && port != 0;
        bool previous = GUI.enabled;
        GUI.enabled = previous && CanStart() && validPort;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Host")) StartHost(port);
        if (GUILayout.Button("Join")) Join(address, port);
        GUILayout.EndHorizontal();
        GUI.enabled = previous && roleBound && !leaving;
        if (GUILayout.Button("Leave")) { status = "Leaving session"; Leave(); }
        GUI.enabled = previous;
        if (HasLocalParticipant) GUILayout.Label("Player: " + LocalPersistenceIdentity);
        if (Registry != null) GUILayout.Label("Reserved participants: " + Registry.Participants.Count);
        GUILayout.Label("Spawned players: " + SpawnedPlayerCount);
        GUI.enabled = previous && IsHost && !leaving && GameplayScene?.IsActive != true && playtest == null && playerPrefab != null;
        if (GUILayout.Button("Start MP.2 movement playtest")) StartMovementPlaytest();
        GUI.enabled = previous && IsHost && !leaving && GameplayScene?.IsActive != true && InteractionWorld == null && interactionPrefab != null;
        if (GUILayout.Button("Start MP.3 interaction playtest")) StartInteractionPlaytest();
        GUI.enabled = previous && IsHost && !leaving && GameplayScene?.IsActive != true && BoatWorld == null && InteractionWorld == null && boatPlaytestPrefab != null;
        if (GUILayout.Button("Start MP.4 sailing boat playtest")) StartBoatPlaytest();
        GUI.enabled = previous;
        if (playerPrefab == null) GUILayout.Label("CharacterPlayer needs NetworkObject and NetworkPlayerActor.");
        if (playtest != null) GUILayout.Label("A/D or arrows: move. Space: jump. Shift: sprint. Leave restores menu.");
        GUILayout.Label("Fresh host: use New Game to enter the shared NodeScene. Load remains single-player only.");
        GUILayout.EndArea();
    }
}
