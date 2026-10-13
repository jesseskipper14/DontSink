using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct NetworkStorageRecord : INetworkSerializable, IEquatable<NetworkStorageRecord>
{
    public FixedString64Bytes Hardpoint;
    public uint Generation;
    public NetworkItemData Items;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    { s.SerializeValue(ref Hardpoint); s.SerializeValue(ref Generation); Items.NetworkSerialize(s); }
    public bool Equals(NetworkStorageRecord other) => Hardpoint.Equals(other.Hardpoint) && Generation == other.Generation && Items.Equals(other.Items);
}
public struct NetworkStorageSnapshot : INetworkSerializable, IEquatable<NetworkStorageSnapshot>
{
    public NetworkStorageRecord[] Records;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        int count = Records?.Length ?? 0; s.SerializeValue(ref count);
        if (count < 0 || count > 64) throw new InvalidOperationException("Storage capacity exceeded.");
        if (s.IsReader) Records = new NetworkStorageRecord[count];
        int bytes = 0;
        for (int i = 0; i < count; i++)
        { Records[i].NetworkSerialize(s); bytes += Records[i].Items.Bytes?.Length ?? 0;
          if (bytes > NetworkItemData.MaxBytes) throw new InvalidOperationException("Storage byte budget exceeded."); }
    }
    public bool Equals(NetworkStorageSnapshot other)
    {
        int count = Records?.Length ?? 0;
        if (count != (other.Records?.Length ?? 0)) return false;
        for (int i = 0; i < count; i++) if (!Records[i].Equals(other.Records[i])) return false;
        return true;
    }
}
public sealed partial class NetworkSceneBoat
{
    private readonly NetworkVariable<NetworkStorageSnapshot> storageData = new();
    private StorageModule[] storageTargets = Array.Empty<StorageModule>();
    private readonly Dictionary<StorageModule, NetworkItemData> appliedStorage = new();
    private double nextStorage;
    public static bool IsPlainStorage(StorageModule storage) => storage != null && storage.GetComponent<TetherDeploymentModule>() == null;
    public static bool SupportsStorage(HardpointInteractable target) => IsPlainStorage(target.TargetHardpoint?.InstalledModule?.GetComponent<StorageModule>());
    private void BindStorage()
    {
        storageTargets = boat.GetComponentsInChildren<StorageModule>(true).Where(s => IsPlainStorage(s) &&
            s.InstalledModule?.OwnerHardpoint?.InstalledModule == s.InstalledModule).ToArray();
        if (storageTargets.Length > 64) throw new InvalidOperationException("Storage capacity exceeded.");
        appliedStorage.Clear();
        if (IsServer) TickStorage(true);
    }
    public uint StorageGeneration(StorageModule storage) => storage?.InstalledModule?.OwnerHardpoint != null ? HardpointGeneration(storage.InstalledModule.OwnerHardpoint) : 0;
    public NetworkInventoryAddress StorageAddress(StorageModule storage, int slot) => new(NetworkInventoryKind.Storage, slot,
        storage?.InstalledModule?.OwnerHardpoint?.HardpointId ?? "", StorageGeneration(storage));
    public bool CanAccessStorage(NetworkPlayerActor actor, StorageModule storage)
    {
        if (actor == null || storage == null || !storageTargets.Contains(storage)) return false;
        var target = storage.InstalledModule.OwnerHardpoint.GetComponent<HardpointInteractable>();
        int index = Array.IndexOf(interactionTargets, target);
        return index >= 0 && TryInteractionContext(actor, index, out _);
    }
    public StorageModule ResolveStorage(NetworkPlayerActor actor, NetworkInventoryAddress address) => storageTargets.FirstOrDefault(s =>
        s.InstalledModule.OwnerHardpoint.HardpointId == address.Parent.ToString() && StorageGeneration(s) == address.Generation && CanAccessStorage(actor, s));
    public ItemInstance FindStorageItem(string id, NetworkPlayerActor actor = null)
    {
        foreach (var storage in storageTargets)
            if (actor == null || CanAccessStorage(actor, storage))
                foreach (var slot in storage.ContainerState.Slots)
                { var item = NetworkInventoryModel.FindItem(slot.Instance, id); if (item != null) return item; }
        return null;
    }
    internal StorageModule FindContainingStorage(NetworkPlayerActor actor, string id) => storageTargets.FirstOrDefault(s =>
        CanAccessStorage(actor, s) && s.ContainerState.Slots.Any(slot => NetworkInventoryModel.FindItem(slot.Instance, id) != null));
    internal bool StorageBudget(Dictionary<StorageModule, ItemContainerSnapshot> replacements)
    {
        int bytes = 0;
        foreach (var storage in storageTargets)
        {
            bytes += NetworkItemCodec.CaptureContainer(replacements.TryGetValue(storage, out var snapshot) ? snapshot : storage.CaptureContainerSnapshot()).Bytes.Length;
            if (bytes > NetworkItemData.MaxBytes) return false;
        }
        return true;
    }
    private void TickStorage(bool force = false)
    {
        if (!IsServer || !force && Time.realtimeSinceStartupAsDouble < nextStorage) return;
        nextStorage = Time.realtimeSinceStartupAsDouble + .25;
        if (!StorageBudget(new())) throw new InvalidOperationException("Shared storage byte budget exceeded.");
        storageData.Value = new NetworkStorageSnapshot { Records = storageTargets.Select(s => new NetworkStorageRecord {
            Hardpoint = new FixedString64Bytes(s.InstalledModule.OwnerHardpoint.HardpointId), Generation = StorageGeneration(s),
            Items = NetworkItemCodec.CaptureContainer(s.CaptureContainerSnapshot()) }).ToArray() };
    }
    private void ApplyStorage()
    {
        var records = storageData.Value.Records;
        if (records == null || records.Length != storageTargets.Length) return;
        var prepared = new Dictionary<StorageModule, ItemContainerState>();
        foreach (var record in records)
        {
            var storage = storageTargets.FirstOrDefault(s => s.InstalledModule.OwnerHardpoint.HardpointId == record.Hardpoint.ToString() && StorageGeneration(s) == record.Generation);
            if (storage == null) return;
            if (appliedStorage.TryGetValue(storage, out var old) && old.Equals(record.Items)) continue;
            var state = storage.ContainerState;
            prepared.Add(storage, ItemContainerState.FromSnapshot(NetworkItemCodec.ReadContainer(record.Items, state.SlotCount, state.ColumnCount,
                DontSinkNetworkSession.Instance.GameplayScene), DontSinkNetworkSession.Instance.GameplayScene));
        }
        foreach (var pair in prepared) pair.Key.SetContainerForTransaction(pair.Value);
        foreach (var record in records)
        {
            var storage = storageTargets.First(s => s.InstalledModule.OwnerHardpoint.HardpointId == record.Hardpoint.ToString());
            appliedStorage[storage] = record.Items;
            if (prepared.ContainsKey(storage)) storage.PublishTransactionChange();
        }
    }
    public bool RequestStorageTransfer(StorageModule storage, bool deposit)
    {
        var session = DontSinkNetworkSession.Instance;
        if (InventoryRequestPending || !session.TryGetParticipantActor(NetworkManager.LocalClientId, out var actor) || !CanAccessStorage(actor, storage)) return false;
        var inventory = actor.GetComponentInChildren<PlayerInventory>(true);
        for (int h = 0; h < inventory.HotbarSlotCount; h++)
            for (int s = 0; s < storage.ContainerState.SlotCount; s++)
            {
                var personal = inventory.GetSlot(h).Instance; var shared = storage.ContainerState.GetSlot(s).Instance;
                var item = deposit ? personal : shared; var target = deposit ? shared : personal;
                if (item == null || target != null && (!target.CanStackWith(item) || target.RemainingStackSpace <= 0)) continue;
                if (deposit ? !storage.CanAcceptItemInSlot(item, s) : !item.Definition.StowableInInventory || !item.Definition.IsAllowedInParentSlot(PlayerInventory.HotbarIndexToSlotType(h))) continue;
                var own = new NetworkInventoryAddress(NetworkInventoryKind.Hotbar, h); var external = StorageAddress(storage, s);
                return RequestInventoryMove(deposit ? own : external, deposit ? external : own, item.Quantity,
                    NetworkInventoryModel.Fingerprint(item), NetworkInventoryModel.Fingerprint(target));
            }
        return false;
    }
}
