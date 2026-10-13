using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Initial hosted NodeScene entry. Travel/save/reconnect policy remains a separate
// protocol: do not route ordinary unilateral scene transitions through this path.
public sealed class NetworkGameplaySceneBootstrap : IDisposable, IItemDefinitionResolver
{
    private const string EnterMessage = "DontSink/Scene/Enter/1", LoadedMessage = "DontSink/Scene/Loaded/1";
    private readonly DontSinkNetworkSession session;
    private readonly NetworkManager manager;
    private readonly HashSet<ulong> entered = new(), loaded = new();
    private ItemDefinitionCatalog catalog;
    private Vector2 spawn;
    private string sceneName;
    private uint sceneEpoch;
    private bool captured, sentLoaded, preparedScene;
    private double deadline;
    public bool IsActive { get; private set; }
    public Boat SceneBoat { get; private set; }
    public string Status { get; private set; } = "Gameplay scene not started.";
    public NetworkSceneBoat Replication { get; internal set; }
    public bool LocalSceneReady => preparedScene && SceneBoat != null;

    public NetworkGameplaySceneBootstrap(DontSinkNetworkSession owner, NetworkManager networkManager)
    {
        session = owner; manager = networkManager;
        manager.CustomMessagingManager.RegisterNamedMessageHandler(EnterMessage, Enter);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(LoadedMessage, Loaded);
        SceneManager.sceneLoaded += SceneLoaded;
    }
    public bool CanStart(string scene, out string reason)
    {
        reason = "Only a fresh host session may start a hosted NodeScene.";
        if (!manager.IsHost || IsActive || session.MovementPlaytestActive || scene != "NodeScene") return false;
        if (!Application.CanStreamedLevelBeLoaded(scene)) { reason = "NodeScene must be included in the build."; return false; }
        reason = null; return true;
    }
    public void Start(string scene)
    {
        if (!CanStart(scene, out string reason)) throw new InvalidOperationException(reason);
        IsActive = true; sceneName = scene; deadline = Time.realtimeSinceStartupAsDouble + 180;
        Status = "Host is initializing the authored node/world.";
        SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsActive || scene.name != sceneName) return;
        // Called after Awake, before Start/physics/input. Keep the authored actor
        // as an inactive authoring reference; its restore loops never run.
        foreach (var player in UnityEngine.Object.FindObjectsByType<CharacterPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (player.gameObject.scene != scene || player.GetComponent<NetworkPlayerActor>()?.IsSpawned == true) continue;
            spawn = player.transform.position;
            catalog ??= player.GetComponentInChildren<PlayerLoadoutPersistence>(true)?.ItemCatalog;
            player.gameObject.SetActive(false);
        }
        preparedScene = true;
        foreach (var actor in session.SpawnedPlayers) Present(actor);
    }
    public void BindBoat(Boat boat)
    {
        if (!IsActive || boat == null || boat.gameObject.scene.name != sceneName) return;
        if (SceneBoat != null && SceneBoat != boat) throw new InvalidOperationException("Hosted scene created more than one player boat.");
        SceneBoat = boat;
        catalog ??= boat.GetComponent<BoatModuleStatePersistence>()?.ItemCatalog;
        if (manager.IsServer && Replication == null) session.SpawnSceneBoat();
    }
    public ItemDefinition Resolve(string id) => catalog != null ? catalog.Resolve(id) :
        id == "item_cartographic_chart" ? Resources.Load<ItemDefinition>("Cartography/item_cartographic_chart") : null;
    public bool IsParticipantReady(ulong clientId) => IsActive && loaded.Contains(clientId);
    public Vector2 SpawnPoint(int slot) => spawn + Vector2.right * slot * 1.5f + Vector2.up * .5f;
    public void Present(NetworkPlayerActor actor)
    {
        if (!IsActive || !preparedScene || actor == null) return;
        foreach (var visual in actor.GetComponentsInChildren<PlayerHeldItemVisual>(true)) visual.enabled = true;
        if (!actor.IsOwner) return;
        foreach (var ui in UnityEngine.Object.FindObjectsByType<InteractPromptUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (ui.gameObject.scene.name == sceneName) actor.GetComponent<InteractPromptDriver>()?.BindPromptUI(ui);
        foreach (var camera in UnityEngine.Object.FindObjectsByType<CameraManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (camera.gameObject.scene.name == sceneName) camera.BindPlayer(actor.transform, true);
        foreach (var ui in UnityEngine.Object.FindObjectsByType<PlayerInventoryUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (ui.gameObject.scene.name == sceneName) ui.BindParticipant(actor.GetComponentInChildren<PlayerInventory>(true), actor.GetComponentInChildren<PlayerEquipment>(true));
        foreach (var overlay in UnityEngine.Object.FindObjectsByType<ExternalContainerOverlayUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (overlay.gameObject.scene.name == sceneName) overlay.BindParticipant(actor.transform);
        foreach (var drag in UnityEngine.Object.FindObjectsByType<InventoryDragController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (drag.gameObject.scene.name == sceneName) { drag.BindParticipant(actor.GetComponentInChildren<PlayerInventory>(true)); drag.enabled = true; }
        SceneBoat?.GetComponent<BoatVisualStateController>()?.SetLocalViewingPlayer(actor.GetComponent<PlayerBoardingState>());
    }
    private bool SceneBuilt()
    {
        if (!preparedScene || SceneBoat == null) return false;
        var binder = UnityEngine.Object.FindAnyObjectByType<WorldMapRuntimeBinder>();
        var topology = UnityEngine.Object.FindAnyObjectByType<WorldMapTopographyDebugSource>();
        return binder != null && binder.IsBuilt && topology?.Field?.IsValid == true;
    }
    public void Tick()
    {
        if (!IsActive || !manager.IsListening) return;
        if (Time.realtimeSinceStartupAsDouble > deadline && (!captured || !sentLoaded))
        { Fail("Gameplay scene initialization timed out."); return; }
        if (manager.IsServer)
        {
            if (!captured)
            {
                if (!SceneBuilt()) return;
                if (!WorldMapSaveBuilder.CaptureCurrentWorldMapIntoGameState("Multiplayer initial node")) { Fail("Host world capture failed."); return; }
                SceneTransitionController.I?.SaveCurrentBoatState("Multiplayer initial node");
                var gs = GameState.I;
                gs.SetLocalPlayerPersistenceKey(session.LocalPersistenceIdentity, "Multiplayer host scene owner");
                var gameplay = new NetworkGameplayStartState { player = gs.player, boat = gs.boat, time = gs.timeOfDay,
                    celestial = gs.celestialCharts, navigation = gs.worldNavigation };
                if (!session.WorldBootstrap.Prepare(gs.worldMapSnapshot, sceneName, out string reason, gameplay)) { Fail(reason); return; }
                captured = sentLoaded = true; sceneEpoch = session.WorldBootstrap.Epoch;
                loaded.Add(manager.LocalClientId); entered.Add(manager.LocalClientId);
            }
            foreach (var participant in session.Registry.Participants)
            {
                if (!participant.IsConnected) continue;
                if (!entered.Contains(participant.ConnectionId) && session.WorldBootstrap.IsReady(participant.ConnectionId))
                {
                    using var writer = new FastBufferWriter(16, Allocator.Temp);
                    writer.WriteValueSafe(sceneEpoch);
                    manager.CustomMessagingManager.SendNamedMessage(EnterMessage, participant.ConnectionId, writer, NetworkDelivery.ReliableFragmentedSequenced);
                    entered.Add(participant.ConnectionId);
                    deadline = Time.realtimeSinceStartupAsDouble + 180;
                }
                if (loaded.Contains(participant.ConnectionId)) session.SpawnGameplayParticipant(participant.ConnectionId);
            }
            Status = "Hosted NodeScene — " + session.SpawnedPlayerCount + " participants ready.";
            foreach (var participant in session.Registry.Participants)
                if (participant.IsConnected && entered.Contains(participant.ConnectionId) &&
                    !loaded.Contains(participant.ConnectionId) && Time.realtimeSinceStartupAsDouble > deadline)
                { Fail("Participant did not finish loading NodeScene."); return; }
        }
        else if (!sentLoaded && SceneBuilt())
        {
            sentLoaded = true;
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(sceneEpoch);
            manager.CustomMessagingManager.SendNamedMessage(LoadedMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            Status = "NodeScene restored from host world; awaiting participant spawn.";
        }
    }
    private void Enter(ulong sender, FastBufferReader reader)
    {
        if (manager.IsServer || sender != NetworkManager.ServerClientId || IsActive) return;
        try
        {
            reader.ReadValueSafe(out uint epoch);
            var snapshot = session.WorldBootstrap.Prepared;
            if (snapshot == null || epoch != session.WorldBootstrap.Epoch || snapshot.sceneName != "NodeScene" ||
                snapshot.gameplay?.boat == null || snapshot.gameplay.player == null) throw new InvalidOperationException("No matching prepared gameplay state.");
            IsActive = captured = true; sceneEpoch = epoch; sceneName = snapshot.sceneName;
            deadline = Time.realtimeSinceStartupAsDouble + 180;
            if (GameState.I == null) new GameObject("GameState").AddComponent<GameState>();
            var gs = GameState.I;
            gs.player = snapshot.gameplay.player; gs.boat = snapshot.gameplay.boat;
            gs.SetWorldMapSnapshot(snapshot.world, "Multiplayer initial node");
            gs.timeOfDay = snapshot.gameplay.time; gs.celestialCharts = snapshot.gameplay.celestial;
            gs.worldNavigation = snapshot.gameplay.navigation; gs.activeTravel = null;
            gs.SetLocalPlayerPersistenceKey(session.LocalPersistenceIdentity, "Multiplayer scene owner");
            WorldMapSaveRestorer.RestoreNodeRuntimeStateToGameState(gs);
            Status = "Loading host-selected NodeScene.";
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (Exception ex) { Fail("Gameplay entry rejected: " + ex.Message); }
    }
    private void Loaded(ulong sender, FastBufferReader reader)
    {
        if (!manager.IsServer || !IsActive || !session.Registry.TryResolveConnectedSender(sender, out _)) return;
        try
        {
            reader.ReadValueSafe(out uint epoch);
            if (epoch == sceneEpoch && entered.Contains(sender) && session.WorldBootstrap.IsReady(sender)) loaded.Add(sender);
        }
        catch (Exception) { }
    }
    public void Disconnected(ulong clientId) { entered.Remove(clientId); loaded.Remove(clientId); }
    private void Fail(string reason) { Status = reason; session.ReportSessionFailure(reason); }
    public void Dispose()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (manager != null && manager.CustomMessagingManager != null)
        {
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(EnterMessage);
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(LoadedMessage);
        }
        entered.Clear(); loaded.Clear(); SceneBoat = null; Replication = null;
    }
}
