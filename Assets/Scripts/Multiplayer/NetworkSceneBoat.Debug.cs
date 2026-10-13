using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Latest state per changed hardpoint. Reinstallations replace records instead
// of growing an operation log; late joiners receive the current incarnation.
public struct NetworkDebugModule : INetworkSerializable, IEquatable<NetworkDebugModule>
{
    public FixedString64Bytes Hardpoint, Item;
    public uint Revision;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    { s.SerializeValue(ref Hardpoint); s.SerializeValue(ref Item); s.SerializeValue(ref Revision); }
    public bool Equals(NetworkDebugModule other) => Hardpoint.Equals(other.Hardpoint) && Item.Equals(other.Item) && Revision == other.Revision;
}

public sealed partial class NetworkSceneBoat
{
    private NetworkList<NetworkDebugModule> debugModules;
    private readonly Dictionary<string, uint> appliedLayouts = new();
    private readonly Dictionary<string, NetworkItemData> installedItems = new();
    private uint debugSequence;
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> debugRequests = new();
    private void InitializeDebugSpawning() => debugModules = new NetworkList<NetworkDebugModule>();
    public void RequestDebugSpawn(string itemId, int quantity, int charges, bool autoInstall = true)
    {
        var session = DontSinkNetworkSession.Instance;
        if (!IsSpawned || session?.GameplayScene?.LocalSceneReady != true || string.IsNullOrEmpty(itemId) ||
            System.Text.Encoding.UTF8.GetByteCount(itemId) > 61) return;
        DebugSpawnRpc(++debugSequence, new FixedString64Bytes(itemId), quantity, charges, autoInstall);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DebugSpawnRpc(uint sequence, FixedString64Bytes itemId, int quantity, int charges, bool autoInstall, RpcParams rpc = default)
    {
        var session = DontSinkNetworkSession.Instance;
        ulong sender = rpc.Receive.SenderClientId;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) ||
            !session.TryGetParticipantActor(sender, out var actor) || actor.OwnerClientId != sender ||
            participant.CharacterIdentity != actor.CharacterIdentity || !actor.DebugSpawningAllowed ||
            !session.GameplayScene.IsParticipantReady(sender)) return;
        if (!debugRequests.TryGetValue(sender, out var gate)) debugRequests.Add(sender, gate = new NetworkInteractionRequestGate());
        var disposition = gate.Begin(sequence, Time.realtimeSinceStartupAsDouble);
        string reason = disposition == NetworkInteractionRequestDisposition.Replay ? gate.LastReason : "Debug spawn rate limited.";
        if (disposition == NetworkInteractionRequestDisposition.New)
        {
            var definition = session.GameplayScene.Resolve(itemId.ToString());
            reason = "Unknown item or invalid quantity/charges.";
            if (definition != null && quantity >= 1 && quantity <= definition.MaxStack && charges >= -1 &&
                (charges < 0 || definition.HasCharges && charges <= definition.MaxCharges))
            {
                if (definition.IsInstallableModule && autoInstall)
                {
                    var prefab = definition.ModuleDefinition.InstalledPrefab;
                    bool supported = prefab != null && (prefab.GetComponent<EngineModule>() != null ||
                        prefab.GetComponent<GeneratorModule>() != null || prefab.GetComponent<PumpModule>() != null ||
                        prefab.GetComponent<HelmModule>() != null || prefab.GetComponent<RudderModule>() != null || IsPlainStorage(prefab.GetComponent<StorageModule>()));
                    reason = "Debug install supports engines, generators, pumps, helms and rudders.";
                    if (supported)
                    {
                        reason = "Board the boat and stand within 6m of a compatible empty hardpoint.";
                        var hardpoint = actor.GetComponent<PlayerBoardingState>()?.CurrentBoatRoot == boat.transform
                            ? boat.GetComponentsInChildren<Hardpoint>(true).Where(h => h.gameObject.activeInHierarchy &&
                                h.GetComponent<HardpointInteractable>() != null && h.CanInstallAuthoritatively(definition.ModuleDefinition, out _))
                                .OrderBy(h => ((Vector2)h.MountPoint.position - actor.AuthoritativePosition).sqrMagnitude).FirstOrDefault()
                            : null;
                        if (hardpoint != null && LayoutHasCapacity(hardpoint) && ModuleCount < 64 && hardpoint != null &&
                            Vector2.Distance(hardpoint.MountPoint.position, actor.AuthoritativePosition) <= 6 &&
                            hardpoint.TryInstall(definition.ModuleDefinition, out _))
                        {
                            PublishLayout(hardpoint, definition, NetworkItemCodec.Capture(ItemInstance.Create(definition, 1)));
                            RefreshDebugModuleBindings();
                            reason = "Installed " + definition.DisplayName + " at " + hardpoint.name + ".";
                        }
                    }
                }
                else
                {
                    var item = ItemInstance.Create(definition, quantity);
                    if (charges >= 0) item.SetCharges(charges);
                    var resolver = actor.GetComponentInChildren<ItemAcquisitionResolver>(true);
                    reason = resolver != null && resolver.TryAcquire(item)
                        ? "Added " + definition.DisplayName + " to your inventory."
                        : "No inventory space; debug item was not spawned.";
                }
            }
            gate.Complete(reason.StartsWith("Installed ") || reason.StartsWith("Added "), reason);
        }
        DebugSpawnResultRpc(new FixedString128Bytes(reason.Length > 120 ? reason.Substring(0, 120) : reason),
            RpcTarget.Single(sender, RpcTargetUse.Temp));
    }
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void DebugSpawnResultRpc(FixedString128Bytes reason, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId == NetworkManager.ServerClientId)
        {
            foreach (var spawner in UnityEngine.Object.FindObjectsByType<InventoryDebugSpawner>(FindObjectsSortMode.None))
                if (spawner.GetComponentInParent<NetworkPlayerActor>()?.IsOwner == true) spawner.SetNetworkFeedback(reason.ToString());
            Debug.Log("[MP Debug] " + reason, this);
        }
    }
    public uint HardpointGeneration(Hardpoint hardpoint)
    {
        foreach (var record in debugModules) if (record.Hardpoint.ToString() == hardpoint.HardpointId) return record.Revision;
        return 1;
    }
    private void PublishLayout(Hardpoint hardpoint, ItemDefinition item, NetworkItemData source)
    {
        var record = new NetworkDebugModule { Hardpoint = new FixedString64Bytes(hardpoint.HardpointId),
            Item = new FixedString64Bytes(item?.ItemId ?? ""), Revision = HardpointGeneration(hardpoint) + 1 };
        if (item == null) installedItems.Remove(hardpoint.HardpointId); else installedItems[hardpoint.HardpointId] = source;
        for (int i = 0; i < debugModules.Count; i++)
            if (debugModules[i].Hardpoint.Equals(record.Hardpoint)) { debugModules[i] = record; return; }
        if (debugModules.Count >= 64) throw new InvalidOperationException("Changed hardpoint capacity exceeded.");
        debugModules.Add(record);
    }
    private bool LayoutHasCapacity(Hardpoint hardpoint)
    {
        if (debugModules.Count < 64) return true;
        foreach (var record in debugModules) if (record.Hardpoint.ToString() == hardpoint.HardpointId) return true;
        return false;
    }
    private void ApplyDebugModules()
    {
        bool changed = false;
        foreach (var record in debugModules)
        {
            string id = record.Hardpoint.ToString();
            if (appliedLayouts.TryGetValue(id, out var revision) && revision == record.Revision) continue;
            var hardpoint = boat.GetComponentsInChildren<Hardpoint>(true).SingleOrDefault(h => h.HardpointId == id);
            if (hardpoint == null) throw new InvalidOperationException("Module manifest references an unknown hardpoint.");
            if (hardpoint.HasInstalledModule && !hardpoint.TryRemoveReplica()) throw new InvalidOperationException("Replica module could not be removed.");
            if (record.Item.Length > 0)
            {
                var definition = DontSinkNetworkSession.Instance.GameplayScene.Resolve(record.Item.ToString())?.ModuleDefinition;
                if (definition == null || !hardpoint.TryInstallReplica(definition, out var module)) throw new InvalidOperationException("Module manifest cannot be installed.");
                ConfigureReplica(module.gameObject);
            }
            appliedLayouts[id] = record.Revision; changed = true;
        }
        if (changed) RefreshDebugModuleBindings();
    }
    private void RefreshDebugModuleBindings()
    {
        engines = boat.GetComponentsInChildren<EngineModule>(true).Where(e => e.GetComponent<InstalledModule>() is not InstalledModule m || m.OwnerHardpoint == null || m.OwnerHardpoint.InstalledModule == m).ToArray();
        if (engines.Length > 64) throw new InvalidOperationException("Shared engine capacity exceeded.");
        BindModules(); BindStorage(); BindInteractions();
        var visual = boat.GetComponent<BoatVisualStateController>();
        if (visual != null)
        {
            visual.CacheRenderers(); visual.ApplyMode(visual.CurrentMode);
            foreach (var actor in DontSinkNetworkSession.Instance.SpawnedPlayers)
                visual.ForceRefreshForPlayer(actor.GetComponent<PlayerBoardingState>());
        }
        if (IsServer) interactionRevision.Value++;
    }
}
