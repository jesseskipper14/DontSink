using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Plans against detached persistence DTOs. No authoritative slot is extracted
// while a UI preview is held, and validation finishes before the first write.
public static class NetworkInventoryModel
{
    public static readonly BottomBarSlotType[] EquipmentSlots = { BottomBarSlotType.Hands, BottomBarSlotType.Head,
        BottomBarSlotType.Feet, BottomBarSlotType.Toolbelt, BottomBarSlotType.Backpack, BottomBarSlotType.Body };
    public static ulong Fingerprint(ItemInstance item)
    {
        var snapshot = item?.ToSnapshot();
        NormalizeMeasurements(snapshot);
        ulong hash = 14695981039346656037UL;
        foreach (byte value in NetworkItemCodec.CaptureItemSnapshot(snapshot).Bytes) hash = unchecked((hash ^ value) * 1099511628211UL);
        return hash;
    }
    private static void NormalizeMeasurements(ItemInstanceSnapshot item)
    {
        if (item == null) return;
        // Drying and consumption do not relocate an item. Commit the latest
        // host measurements, while detecting identity/quantity/content edits.
        item.currentCharges = 0; item.retainedWaterVolume = 0;
        foreach (var child in item.container?.slots ?? new List<ItemInstanceSnapshot>()) NormalizeMeasurements(child);
    }
    public static ItemInstance FindItem(ItemInstance root, string id)
    {
        if (root == null || string.IsNullOrEmpty(id)) return null;
        if (root.InstanceId == id) return root;
        var state = root.ContainerState;
        for (int i = 0; state != null && i < state.SlotCount; i++)
        { var found = FindItem(state.GetSlot(i).Instance, id); if (found != null) return found; }
        return null;
    }
    public static ItemInstance FindOwnedItem(NetworkPlayerActor actor, string id)
    {
        if (actor == null || string.IsNullOrEmpty(id)) return null;
        var inventory = actor.GetComponentInChildren<PlayerInventory>(true);
        var equipment = actor.GetComponentInChildren<PlayerEquipment>(true);
        for (int i = 0; inventory != null && i < inventory.HotbarSlotCount; i++)
        { var found = FindItem(inventory.GetSlot(i).Instance, id); if (found != null) return found; }
        foreach (var slot in EquipmentSlots)
        { var found = FindItem(equipment?.Get(slot), id); if (found != null) return found; }
        return null;
    }
    public static ItemInstance ResolveLocalItem(string id)
    {
        var session = DontSinkNetworkSession.Instance;
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (session == null || manager == null || !session.TryGetParticipantActor(manager.LocalClientId, out var actor)) return null;
        return FindOwnedItem(actor, id) ?? session.GameplayScene.Replication?.FindStorageItem(id);
    }
    public static ItemInstanceSnapshot EquipmentItem(EquipmentSnapshot s, BottomBarSlotType slot) => slot switch
    {
        BottomBarSlotType.Hands => s.hands, BottomBarSlotType.Head => s.head, BottomBarSlotType.Feet => s.feet,
        BottomBarSlotType.Toolbelt => s.toolbelt, BottomBarSlotType.Backpack => s.backpack, BottomBarSlotType.Body => s.body, _ => null
    };
    private static void SetEquipment(EquipmentSnapshot s, BottomBarSlotType slot, ItemInstanceSnapshot value)
    {
        switch (slot) { case BottomBarSlotType.Hands: s.hands = value; break; case BottomBarSlotType.Head: s.head = value; break;
            case BottomBarSlotType.Feet: s.feet = value; break; case BottomBarSlotType.Toolbelt: s.toolbelt = value; break;
            case BottomBarSlotType.Backpack: s.backpack = value; break; case BottomBarSlotType.Body: s.body = value; break; }
    }
    private static ItemInstanceSnapshot FindSnapshot(ItemInstanceSnapshot root, string id)
    {
        if (root == null) return null;
        if (root.instanceId == id) return root;
        foreach (var child in root.container?.slots ?? new List<ItemInstanceSnapshot>())
        { var found = FindSnapshot(child, id); if (found != null) return found; }
        return null;
    }
    internal sealed class SlotView
    {
        public Func<ItemInstanceSnapshot> Read;
        public Action<ItemInstanceSnapshot> Write;
        public ItemInstance Actual, Parent;
        public StorageModule Storage;
        public NetworkInventoryAddress Address;
        public int Limit(ItemInstance item) => Address.Kind == NetworkInventoryKind.Container && Parent.Definition.ContainerMaxQuantityPerSlot > 0
            ? Mathf.Min(item.MaxStack, Parent.Definition.ContainerMaxQuantityPerSlot) : item.MaxStack;
        public bool Accept(ItemInstance item) => item != null && item.Definition != null && (Address.Kind switch
        {
            NetworkInventoryKind.Hotbar => item.Definition.StowableInInventory && item.Definition.IsAllowedInParentSlot(PlayerInventory.HotbarIndexToSlotType(Address.Slot)),
            NetworkInventoryKind.Equipment => (BottomBarSlotType)Address.Slot == BottomBarSlotType.Hands ||
                item.Definition.IsEquippable && item.Definition.EquipSlot == (BottomBarSlotType)Address.Slot,
            NetworkInventoryKind.Container => Parent.Definition.CanContainerAccept(item.Definition),
            NetworkInventoryKind.Storage => Storage.CanAcceptItemInSlot(item, Address.Slot),
            _ => false
        });
    }
    internal sealed class Plan
    {
        public NetworkPlayerActor Actor;
        public PlayerInventory Inventory;
        public PlayerEquipment Equipment;
        public PlayerLoadoutSnapshot Snapshot;
        public IItemDefinitionResolver Resolver;
        public readonly Dictionary<StorageModule, ItemContainerSnapshot> Shared = new();
        private ItemContainerSnapshot SharedSnapshot(StorageModule storage)
        {
            if (!Shared.TryGetValue(storage, out var snapshot)) Shared.Add(storage, snapshot = storage.CaptureContainerSnapshot());
            return snapshot;
        }
        public Plan(NetworkPlayerActor actor)
        {
            Actor = actor; Inventory = actor.GetComponentInChildren<PlayerInventory>(true); Equipment = actor.GetComponentInChildren<PlayerEquipment>(true);
            Snapshot = new PlayerLoadoutSnapshot { inventory = Inventory.CaptureSnapshot(), equipment = Equipment.CaptureSnapshot() };
            Resolver = DontSinkNetworkSession.Instance.GameplayScene;
        }
        public SlotView Resolve(NetworkInventoryAddress address)
        {
            if (address.Slot < 0 || address.Slot >= 64 || address.Kind != NetworkInventoryKind.Storage && address.Generation != 0) return null;
            var view = new SlotView { Address = address };
            if (address.Kind == NetworkInventoryKind.Hotbar && address.Parent.Length == 0 && address.Slot < Inventory.HotbarSlotCount)
            {
                view.Actual = Inventory.GetSlot(address.Slot).Instance;
                view.Read = () => Snapshot.inventory.hotbarSlots[address.Slot]; view.Write = value => Snapshot.inventory.hotbarSlots[address.Slot] = value;
            }
            else if (address.Kind == NetworkInventoryKind.Equipment && address.Parent.Length == 0 && Array.IndexOf(EquipmentSlots, (BottomBarSlotType)address.Slot) >= 0)
            {
                view.Actual = Equipment.Get((BottomBarSlotType)address.Slot);
                view.Read = () => EquipmentItem(Snapshot.equipment, (BottomBarSlotType)address.Slot);
                view.Write = value => SetEquipment(Snapshot.equipment, (BottomBarSlotType)address.Slot, value);
            }
            else if (address.Kind == NetworkInventoryKind.Storage)
            {
                var storage = DontSinkNetworkSession.Instance.GameplayScene.Replication.ResolveStorage(Actor, address);
                if (storage == null || address.Slot >= storage.ContainerState.SlotCount) return null;
                var snapshot = SharedSnapshot(storage); view.Storage = storage;
                view.Actual = storage.ContainerState.GetSlot(address.Slot).Instance;
                view.Read = () => snapshot.slots[address.Slot]; view.Write = value => snapshot.slots[address.Slot] = value;
            }
            else if (address.Kind == NetworkInventoryKind.Container && address.Parent.Length > 0)
            {
                string id = address.Parent.ToString(); view.Parent = FindOwnedItem(Actor, id);
                ItemInstanceSnapshot parent = null;
                foreach (var root in Snapshot.inventory.hotbarSlots.Concat(EquipmentSlots.Select(slot => EquipmentItem(Snapshot.equipment, slot))))
                { parent = FindSnapshot(root, id); if (parent != null) break; }
                if (parent == null)
                {
                    var storage = DontSinkNetworkSession.Instance.GameplayScene.Replication.FindContainingStorage(Actor, id);
                    if (storage != null)
                    {
                        view.Parent = DontSinkNetworkSession.Instance.GameplayScene.Replication.FindStorageItem(id, Actor);
                        foreach (var root in SharedSnapshot(storage).slots) { parent = FindSnapshot(root, id); if (parent != null) break; }
                    }
                }
                if (view.Parent?.ContainerState == null || parent?.container == null || address.Slot >= parent.container.slotCount) return null;
                view.Actual = view.Parent.ContainerState.GetSlot(address.Slot).Instance;
                view.Read = () => parent.container.slots[address.Slot]; view.Write = value => parent.container.slots[address.Slot] = value;
            }
            else return null;
            return view;
        }
        public bool Commit(out string reason)
        {
            if (!TryPrepare(out var commit, out reason)) return false;
            commit(); return true;
        }
        public bool TryPrepare(out Action commit, out string reason)
        {
            commit = null;
            reason = "Equipment footprint, item nesting or payload budget is invalid.";
            if (!Equipment.ValidateTransactionLayout(Snapshot.equipment, Resolver)) return false;
            var decoded = NetworkItemCodec.ReadLoadout(NetworkItemCodec.Capture(Snapshot), Resolver);
            var ids = new HashSet<string>();
            foreach (var root in decoded.inventory.hotbarSlots.Concat(EquipmentSlots.Select(slot => EquipmentItem(decoded.equipment, slot))))
                if (!Unique(root, ids)) return false;
            if (!DontSinkNetworkSession.Instance.GameplayScene.Replication.StorageBudget(Shared)) return false;
            var storageStates = new Dictionary<StorageModule, ItemContainerState>();
            foreach (var pair in Shared)
            {
                var layout = pair.Key.ContainerState;
                var checkedState = NetworkItemCodec.ReadContainer(NetworkItemCodec.CaptureContainer(pair.Value), layout.SlotCount, layout.ColumnCount, Resolver);
                foreach (var item in checkedState.slots) if (!Unique(item, ids)) return false;
                storageStates.Add(pair.Key, ItemContainerState.FromSnapshot(checkedState, Resolver));
            }
            // Construct all replacement trees before changing either model.
            var hotbar = decoded.inventory.hotbarSlots.Select(item => ItemInstance.FromSnapshot(item, Resolver)).ToArray();
            var equipped = EquipmentSlots.Select(slot => ItemInstance.FromSnapshot(EquipmentItem(decoded.equipment, slot), Resolver)).ToArray();
            commit = () => {
            for (int i = 0; i < hotbar.Length; i++) Inventory.GetSlot(i).Set(hotbar[i]);
            for (int i = 0; i < EquipmentSlots.Length; i++) Equipment.SetForTransaction(EquipmentSlots[i], equipped[i]);
            foreach (var pair in storageStates) pair.Key.SetContainerForTransaction(pair.Value);
            Inventory.PublishTransactionChange(); Equipment.PublishTransactionChange();
            foreach (var pair in storageStates) pair.Key.PublishTransactionChange(); };
            reason = "Done."; return true;
        }
    }
    private static bool Unique(ItemInstanceSnapshot item, HashSet<string> ids)
    {
        if (item == null) return true;
        if (!ids.Add(item.instanceId)) return false;
        foreach (var child in item.container?.slots ?? new List<ItemInstanceSnapshot>()) if (!Unique(child, ids)) return false;
        return true;
    }
    public static bool TryMove(NetworkPlayerActor actor, NetworkInventoryAddress source, NetworkInventoryAddress destination,
        int requested, ulong sourceFingerprint, ulong destinationFingerprint, out int moved, out string reason)
    {
        moved = 0; reason = "Item changed, inaccessible slot, or invalid transfer.";
        if (source.Equals(destination) || requested < 1) return false;
        var plan = new Plan(actor); var from = plan.Resolve(source); var to = plan.Resolve(destination);
        var item = from?.Actual; var existing = to?.Actual;
        if (item == null || to == null || requested > item.Quantity || Fingerprint(item) != sourceFingerprint ||
            Fingerprint(existing) != destinationFingerprint) return false;
        var sounder = actor.GetComponentInChildren<HandheldSoundingLineController>(true);
        if (sounder != null && (sounder.IsActiveDeployedSounder(item) || existing != null && sounder.IsActiveDeployedSounder(existing)))
        { reason = "Stow the sounding line before moving it."; return false; }
        // A container icon remains an insertion target, as in the existing UI.
        if (existing?.IsContainer == true && !item.IsContainer)
        {
            if (existing.ContainerState == null) return false;
            SlotView candidate = null;
            for (int i = 0; i < existing.ContainerState.SlotCount; i++)
            {
                var target = plan.Resolve(new NetworkInventoryAddress(NetworkInventoryKind.Container, i, existing.InstanceId));
                if (target != null && !target.Address.Equals(from.Address) && target.Accept(item) && (target.Actual == null || target.Actual.CanStackWith(item) && target.Actual.Quantity < target.Limit(item)))
                { candidate = target; break; }
            }
            if (candidate == null) { reason = "Container cannot receive this item."; return false; }
            to = candidate; existing = to.Actual;
        }
        if (to.Parent != null && FindItem(item, to.Parent.InstanceId) != null ||
            existing != null && from.Parent != null && FindItem(existing, from.Parent.InstanceId) != null) return false;
        if (!to.Accept(item)) { reason = "This slot cannot receive that item."; return false; }
        var sourceData = from.Read(); var targetData = to.Read();
        int amount = requested;
        if (existing == null) amount = Mathf.Min(amount, to.Limit(item));
        else if (existing.CanStackWith(item)) amount = Mathf.Min(amount, Mathf.Max(0, to.Limit(item) - existing.Quantity));
        else if (requested != item.Quantity || !from.Accept(existing) || existing.Quantity > from.Limit(existing) || item.Quantity > to.Limit(item))
        { reason = "The displaced item cannot return to the source slot."; return false; }
        if (amount <= 0 || amount < item.Quantity && !item.CanSplit) return false;
        if (existing != null && !existing.CanStackWith(item))
        { from.Write(targetData); to.Write(sourceData); }
        else
        {
            float water = sourceData.retainedWaterVolume * amount / sourceData.quantity;
            if (existing != null)
            { targetData.quantity += amount; targetData.retainedWaterVolume += water; sourceData.quantity -= amount; sourceData.retainedWaterVolume -= water; from.Write(sourceData.quantity > 0 ? sourceData : null); }
            else if (amount == item.Quantity) { from.Write(null); to.Write(sourceData); }
            else
            {
                var split = ItemInstance.FromSnapshot(sourceData, plan.Resolver).ToSnapshot();
                split.instanceId = Guid.NewGuid().ToString("N"); split.quantity = amount; split.retainedWaterVolume = water;
                sourceData.quantity -= amount; sourceData.retainedWaterVolume -= water; to.Write(split);
            }
        }
        if (!plan.Commit(out reason)) return false;
        moved = amount; return true;
    }
}
