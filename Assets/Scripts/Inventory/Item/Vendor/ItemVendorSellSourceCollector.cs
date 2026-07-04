using System;
using System.Collections.Generic;
using UnityEngine;

public static class ItemVendorSellSourceCollector
{
    private static readonly BottomBarSlotType[] EquipmentSlots =
    {
        BottomBarSlotType.Hands,
        BottomBarSlotType.Head,
        BottomBarSlotType.Feet,
        BottomBarSlotType.Toolbelt,
        BottomBarSlotType.Backpack,
        BottomBarSlotType.Body
    };

    public static void CollectPlayerSources(
        AgentServiceContext context,
        List<ItemVendorSellSource> output)
    {
        if (output == null)
            return;

        output.Clear();

        PlayerInventory inventory = FindPlayerInventory(context);
        PlayerEquipment equipment = inventory != null
            ? inventory.Equipment
            : FindPlayerEquipment(context);

        HashSet<string> visitedContainers = new HashSet<string>();

        if (equipment != null)
            CollectEquipmentSources(equipment, output, visitedContainers);

        if (inventory != null)
            CollectHotbarSources(inventory, output, visitedContainers);
    }

    public static void CollectBoatSources(
    AgentServiceContext context,
    List<ItemVendorSellSource> output)
    {
        if (output == null)
            return;

        output.Clear();

        Boat boat = FindCurrentBoat(context);
        if (boat == null)
        {
            Debug.LogWarning(
                "[ItemVendorSellSourceCollector] Boat inventory: no current boat resolved. " +
                "Player may not be boarded, or CurrentBoatRoot is missing.");

            return;
        }

        Debug.Log(
            $"[ItemVendorSellSourceCollector] Boat inventory: resolved boat '{boat.name}' " +
            $"id='{boat.BoatInstanceId}'.");

        BoatItemRegistry registry = boat.GetComponent<BoatItemRegistry>();
        if (registry == null)
        {
            Debug.LogWarning(
                $"[ItemVendorSellSourceCollector] Boat inventory: boat '{boat.name}' has no BoatItemRegistry.",
                boat);

            return;
        }

        List<BoatOwnedItem> registryItems = registry.SnapshotItems();
        int registryCount = registryItems != null ? registryItems.Count : 0;

        Debug.Log(
            $"[ItemVendorSellSourceCollector] Boat inventory: registry has {registryCount} owned item(s).",
            boat);

        HashSet<string> visitedContainers = new HashSet<string>();

        CollectBoatWorldItems(boat, output, visitedContainers);
        CollectBoatStorageModules(boat, output, visitedContainers);

        Debug.Log(
            $"[ItemVendorSellSourceCollector] Boat inventory: collected {output.Count} sell source(s).",
            boat);
    }

    private static void CollectEquipmentSources(
        PlayerEquipment equipment,
        List<ItemVendorSellSource> output,
        HashSet<string> visitedContainers)
    {
        for (int i = 0; i < EquipmentSlots.Length; i++)
        {
            BottomBarSlotType slotType = EquipmentSlots[i];
            ItemInstance item = equipment.Get(slotType);

            if (!IsValidItem(item))
                continue;

            BottomBarSlotType localSlot = slotType;

            string sourceLabel = $"Player / {localSlot}";
            string key = $"player:equip:{localSlot}:{item.InstanceId}";

            output.Add(new ItemVendorSellSource(
                key,
                ItemVendorSellSourceKind.PlayerEquipment,
                sourceLabel,
                0,
                () => equipment.Get(localSlot),
                () =>
                {
                    ItemInstance current = equipment.Get(localSlot);
                    return current != null ? current.Quantity : 0;
                },
                () => null,
                quantity => RemoveFromEquipment(equipment, localSlot, quantity)));

            CollectContainerSources(
                item,
                sourceLabel,
                ItemVendorSellSourceKind.PlayerContainer,
                output,
                visitedContainers,
                () => equipment.NotifyChanged(),
                depth: 1);
        }
    }

    private static void CollectHotbarSources(
        PlayerInventory inventory,
        List<ItemVendorSellSource> output,
        HashSet<string> visitedContainers)
    {
        int count = inventory.HotbarSlotCount;

        for (int i = 0; i < count; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (slot == null || slot.IsEmpty || !IsValidItem(slot.Instance))
                continue;

            int localIndex = i;
            InventorySlot localSlot = slot;
            ItemInstance item = localSlot.Instance;

            string sourceLabel = $"Player / Hotbar {localIndex + 1}";
            string key = $"player:hotbar:{localIndex}:{item.InstanceId}";

            output.Add(new ItemVendorSellSource(
                key,
                ItemVendorSellSourceKind.PlayerHotbar,
                sourceLabel,
                0,
                () => localSlot.Instance,
                () => localSlot.Instance != null ? localSlot.Instance.Quantity : 0,
                () => null,
                quantity => RemoveFromSlot(
                    localSlot,
                    () => inventory.NotifyChanged(),
                    quantity)));

            CollectContainerSources(
                item,
                sourceLabel,
                ItemVendorSellSourceKind.PlayerContainer,
                output,
                visitedContainers,
                () => inventory.NotifyChanged(),
                depth: 1);
        }
    }

    private static void CollectBoatWorldItems(
        Boat boat,
        List<ItemVendorSellSource> output,
        HashSet<string> visitedContainers)
    {
        BoatItemRegistry registry = boat.GetComponent<BoatItemRegistry>();
        if (registry == null)
            return;

        List<BoatOwnedItem> snapshot = registry.SnapshotItems();
        if (snapshot == null)
            return;

        for (int i = 0; i < snapshot.Count; i++)
        {
            BoatOwnedItem owned = snapshot[i];
            if (owned == null || !owned.IsOwnedByBoat)
                continue;

            WorldItem worldItem = owned.GetComponent<WorldItem>();
            if (worldItem == null || !IsValidItem(worldItem.Instance))
                continue;

            BoatSecuredItem secured = worldItem.GetComponent<BoatSecuredItem>();

            WorldItem localWorldItem = worldItem;
            BoatSecuredItem localSecured = secured;

            ItemInstance item = localWorldItem.Instance;

            string sourceLabel = $"Boat / World / {CleanName(localWorldItem.name)}";
            string key = $"boat:world:{item.InstanceId}";

            output.Add(new ItemVendorSellSource(
                key,
                ItemVendorSellSourceKind.BoatWorldItem,
                sourceLabel,
                0,
                () => localWorldItem != null ? localWorldItem.Instance : null,
                () =>
                {
                    ItemInstance current = localWorldItem != null ? localWorldItem.Instance : null;
                    return current != null ? current.Quantity : 0;
                },
                () =>
                {
                    if (localSecured != null && localSecured.IsSecured)
                        return "Unsecure this item before selling.";

                    return null;
                },
                quantity => RemoveFromWorldItem(localWorldItem, quantity)));

            CollectContainerSources(
                item,
                sourceLabel,
                ItemVendorSellSourceKind.BoatWorldContainer,
                output,
                visitedContainers,
                changed: null,
                depth: 1);
        }
    }

    private static void CollectBoatStorageModules(
        Boat boat,
        List<ItemVendorSellSource> output,
        HashSet<string> visitedContainers)
    {
        StorageModule[] modules = boat.GetComponentsInChildren<StorageModule>(true);
        if (modules == null)
            return;

        for (int m = 0; m < modules.Length; m++)
        {
            StorageModule module = modules[m];
            if (module == null)
                continue;

            module.EnsureContainer();

            ItemContainerState container = module.ContainerState;
            if (container == null || container.Slots == null)
                continue;

            for (int i = 0; i < container.Slots.Count; i++)
            {
                InventorySlot slot = container.Slots[i];
                if (slot == null || slot.IsEmpty || !IsValidItem(slot.Instance))
                    continue;

                int localSlotIndex = i;
                InventorySlot localSlot = slot;
                StorageModule localModule = module;
                ItemInstance item = localSlot.Instance;

                string moduleName = CleanName(localModule.name);
                string sourceLabel = $"Boat / {moduleName} / Slot {localSlotIndex + 1}";
                string key = $"boat:storage:{moduleName}:{localSlotIndex}:{item.InstanceId}";

                output.Add(new ItemVendorSellSource(
                    key,
                    ItemVendorSellSourceKind.BoatStorageModule,
                    sourceLabel,
                    0,
                    () => localSlot.Instance,
                    () => localSlot.Instance != null ? localSlot.Instance.Quantity : 0,
                    () => null,
                    quantity => RemoveFromSlot(
                        localSlot,
                        () =>
                        {
                            if (localModule.ContainerState != null)
                                localModule.ContainerState.NotifyChanged();
                        },
                        quantity)));

                CollectContainerSources(
                    item,
                    sourceLabel,
                    ItemVendorSellSourceKind.BoatWorldContainer,
                    output,
                    visitedContainers,
                    () =>
                    {
                        if (localModule.ContainerState != null)
                            localModule.ContainerState.NotifyChanged();
                    },
                    depth: 1);
            }
        }
    }

    private static void CollectContainerSources(
        ItemInstance containerItem,
        string parentLabel,
        ItemVendorSellSourceKind sourceKind,
        List<ItemVendorSellSource> output,
        HashSet<string> visitedContainers,
        Action changed,
        int depth)
    {
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
            return;

        string containerId = containerItem.InstanceId;
        if (!string.IsNullOrWhiteSpace(containerId) && !visitedContainers.Add(containerId))
            return;

        ItemContainerState container = containerItem.ContainerState;
        if (container.Slots == null)
            return;

        for (int i = 0; i < container.Slots.Count; i++)
        {
            InventorySlot slot = container.Slots[i];
            if (slot == null || slot.IsEmpty || !IsValidItem(slot.Instance))
                continue;

            int localSlotIndex = i;
            InventorySlot localSlot = slot;
            ItemInstance item = localSlot.Instance;

            string sourceLabel = $"{parentLabel} / Slot {localSlotIndex + 1}";
            string key = $"container:{containerId}:{localSlotIndex}:{item.InstanceId}";

            output.Add(new ItemVendorSellSource(
                key,
                sourceKind,
                sourceLabel,
                depth,
                () => localSlot.Instance,
                () => localSlot.Instance != null ? localSlot.Instance.Quantity : 0,
                () => null,
                quantity => RemoveFromSlot(
                    localSlot,
                    () =>
                    {
                        container.NotifyChanged();
                        changed?.Invoke();
                    },
                    quantity)));

            CollectContainerSources(
                item,
                sourceLabel,
                sourceKind,
                output,
                visitedContainers,
                () =>
                {
                    container.NotifyChanged();
                    changed?.Invoke();
                },
                depth + 1);
        }
    }

    private static ItemVendorSellRemovalResult RemoveFromEquipment(
        PlayerEquipment equipment,
        BottomBarSlotType slotType,
        int quantity)
    {
        ItemInstance current = equipment.Get(slotType);
        if (!IsValidItem(current))
            return ItemVendorSellRemovalResult.Fail("Equipment slot is empty.");

        quantity = Mathf.Clamp(quantity, 1, current.Quantity);

        if (current.IsStackable && quantity < current.Quantity)
        {
            ItemInstance split = current.SplitOff(quantity);
            if (split == null)
                return ItemVendorSellRemovalResult.Fail("Failed to split stack.");

            equipment.NotifyChanged();
            return ItemVendorSellRemovalResult.Success(split, "Sold from equipment.");
        }

        ItemInstance removed = equipment.Remove(slotType);
        if (removed == null)
            return ItemVendorSellRemovalResult.Fail("Failed to remove equipment item.");

        return ItemVendorSellRemovalResult.Success(removed, "Sold from equipment.");
    }

    private static ItemVendorSellRemovalResult RemoveFromSlot(
        InventorySlot slot,
        Action changed,
        int quantity)
    {
        if (slot == null || slot.IsEmpty || !IsValidItem(slot.Instance))
            return ItemVendorSellRemovalResult.Fail("Slot is empty.");

        ItemInstance current = slot.Instance;
        quantity = Mathf.Clamp(quantity, 1, current.Quantity);

        ItemInstance removed;

        if (current.IsStackable && quantity < current.Quantity)
        {
            removed = current.SplitOff(quantity);
            if (removed == null)
                return ItemVendorSellRemovalResult.Fail("Failed to split stack.");
        }
        else
        {
            removed = current;
            slot.Clear();
        }

        changed?.Invoke();

        return ItemVendorSellRemovalResult.Success(removed, "Sold from slot.");
    }

    private static ItemVendorSellRemovalResult RemoveFromWorldItem(
        WorldItem worldItem,
        int quantity)
    {
        if (worldItem == null || !IsValidItem(worldItem.Instance))
            return ItemVendorSellRemovalResult.Fail("World item is missing.");

        BoatSecuredItem secured = worldItem.GetComponent<BoatSecuredItem>();
        if (secured != null && secured.IsSecured)
            return ItemVendorSellRemovalResult.Fail("Unsecure this item before selling.");

        ItemInstance current = worldItem.Instance;
        quantity = Mathf.Clamp(quantity, 1, current.Quantity);

        ItemInstance removed;

        if (current.IsStackable && quantity < current.Quantity)
        {
            removed = current.SplitOff(quantity);
            if (removed == null)
                return ItemVendorSellRemovalResult.Fail("Failed to split world item stack.");

            CargoWorldLabel label = worldItem.GetComponentInChildren<CargoWorldLabel>(true);
            if (label != null)
                label.Refresh();

            return ItemVendorSellRemovalResult.Success(removed, "Sold from boat.");
        }

        removed = current;

        BoatOwnedItem owned = worldItem.GetComponent<BoatOwnedItem>();
        if (owned != null)
            owned.ClearOwnership();

        UnityEngine.Object.Destroy(worldItem.gameObject);

        return ItemVendorSellRemovalResult.Success(removed, "Sold from boat.");
    }

    private static PlayerInventory FindPlayerInventory(AgentServiceContext context)
    {
        GameObject actor = context.Actor;

        if (actor != null)
        {
            PlayerInventory inventory =
                actor.GetComponentInParent<PlayerInventory>() ??
                actor.GetComponentInChildren<PlayerInventory>(true);

            if (inventory != null)
                return inventory;
        }

        return UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
    }

    private static PlayerEquipment FindPlayerEquipment(AgentServiceContext context)
    {
        GameObject actor = context.Actor;

        if (actor != null)
        {
            PlayerEquipment equipment =
                actor.GetComponentInParent<PlayerEquipment>() ??
                actor.GetComponentInChildren<PlayerEquipment>(true);

            if (equipment != null)
                return equipment;
        }

        return UnityEngine.Object.FindFirstObjectByType<PlayerEquipment>();
    }

    private static Boat FindCurrentBoat(AgentServiceContext context)
    {
        GameObject actor = context.Actor;

        // 1) Best source: the player's boarding state.
        PlayerBoardingState boarding = null;

        if (actor != null)
        {
            boarding =
                actor.GetComponentInParent<PlayerBoardingState>() ??
                actor.GetComponentInChildren<PlayerBoardingState>(true);
        }

        if (boarding != null && boarding.CurrentBoatRoot != null)
        {
            Boat boardedBoat =
                boarding.CurrentBoatRoot.GetComponent<Boat>() ??
                boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

            if (boardedBoat != null)
                return boardedBoat;
        }

        // 2) If the actor is physically under a boat hierarchy, use that.
        if (actor != null)
        {
            Boat actorBoat = actor.GetComponentInParent<Boat>();
            if (actorBoat != null)
                return actorBoat;
        }

        // 3) If the agent/vendor is under a boat hierarchy, use that.
        if (context.Agent != null)
        {
            Boat agentBoat = context.Agent.GetComponentInParent<Boat>();
            if (agentBoat != null)
                return agentBoat;
        }

        // 4) V0 fallback: if there is exactly one boat in the scene, use it.
        // Multiplayer-future-us can complain later from its yacht.
        Boat[] boats = UnityEngine.Object.FindObjectsByType<Boat>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (boats != null && boats.Length == 1)
            return boats[0];

        Debug.LogWarning(
            $"[ItemVendorSellSourceCollector] Could not resolve current boat. " +
            $"boarding={(boarding != null ? "found" : "missing")}, " +
            $"isBoarded={(boarding != null && boarding.IsBoarded)}, " +
            $"boatCount={(boats != null ? boats.Length : 0)}.");

        return null;
    }

    private static bool IsValidItem(ItemInstance item)
    {
        return item != null &&
               item.Definition != null &&
               item.Quantity > 0;
    }

    private static string CleanName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Item";

        return raw
            .Replace("(Clone)", "")
            .Replace("_", " ")
            .Trim();
    }
}