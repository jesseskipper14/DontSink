using System.Collections.Generic;
using UnityEngine;

public static class InventoryConsumableUtility
{
    private static readonly BottomBarSlotType[] EquipmentSlots =
    {
        BottomBarSlotType.Backpack,
        BottomBarSlotType.Toolbelt,
        BottomBarSlotType.Body,
        BottomBarSlotType.Head,
        BottomBarSlotType.Feet,
        BottomBarSlotType.Hands
    };

    public static int Count(PlayerInventory inventory, ItemDefinition definition)
    {
        if (inventory == null || definition == null)
            return 0;

        int total = 0;
        HashSet<ItemInstance> visited = new();

        for (int i = 0; i < inventory.HotbarSlotCount; i++)
            total += CountSlot(inventory.GetSlot(i), definition, visited);

        PlayerEquipment equipment = FindEquipment(inventory);
        if (equipment != null)
        {
            for (int i = 0; i < EquipmentSlots.Length; i++)
                total += CountInstance(equipment.Get(EquipmentSlots[i]), definition, visited);
        }

        return total;
    }

    public static bool TryConsume(PlayerInventory inventory, ItemDefinition definition, int amount)
    {
        if (inventory == null || definition == null || amount <= 0)
            return false;

        if (Count(inventory, definition) < amount)
            return false;

        int remaining = amount;
        bool changed = false;
        HashSet<ItemInstance> visited = new();

        for (int i = 0; i < inventory.HotbarSlotCount && remaining > 0; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (ConsumeFromSlot(slot, definition, visited, ref remaining))
                changed = true;
        }

        PlayerEquipment equipment = FindEquipment(inventory);
        if (equipment != null)
        {
            for (int i = 0; i < EquipmentSlots.Length && remaining > 0; i++)
            {
                if (ConsumeFromEquipmentSlot(equipment, EquipmentSlots[i], definition, visited, ref remaining))
                    changed = true;
            }
        }

        if (changed)
        {
            inventory.NotifyChanged();

            if (equipment != null)
                equipment.NotifyChanged();
        }

        return remaining <= 0;
    }

    private static int CountSlot(
        InventorySlot slot,
        ItemDefinition definition,
        HashSet<ItemInstance> visited)
    {
        if (slot == null || slot.IsEmpty)
            return 0;

        return CountInstance(slot.Instance, definition, visited);
    }

    private static int CountInstance(
        ItemInstance instance,
        ItemDefinition definition,
        HashSet<ItemInstance> visited)
    {
        if (instance == null || instance.Definition == null || instance.IsDepleted())
            return 0;

        int total = IsTarget(instance, definition) ? instance.Quantity : 0;

        if (!instance.IsContainer || instance.ContainerState == null)
            return total;

        if (!visited.Add(instance))
            return total;

        ItemContainerState state = instance.ContainerState;

        for (int i = 0; i < state.SlotCount; i++)
            total += CountSlot(state.GetSlot(i), definition, visited);

        return total;
    }

    private static bool ConsumeFromSlot(
        InventorySlot slot,
        ItemDefinition definition,
        HashSet<ItemInstance> visited,
        ref int remaining)
    {
        if (slot == null || slot.IsEmpty || slot.Instance == null || remaining <= 0)
            return false;

        ItemInstance instance = slot.Instance;
        bool changed = ConsumeFromInstance(instance, definition, visited, ref remaining);

        if (instance.IsDepleted())
            slot.Clear();

        return changed;
    }

    private static bool ConsumeFromEquipmentSlot(
        PlayerEquipment equipment,
        BottomBarSlotType slotType,
        ItemDefinition definition,
        HashSet<ItemInstance> visited,
        ref int remaining)
    {
        if (equipment == null || remaining <= 0)
            return false;

        ItemInstance instance = equipment.Get(slotType);
        if (instance == null)
            return false;

        bool changed = ConsumeFromInstance(instance, definition, visited, ref remaining);

        if (instance.IsDepleted())
            equipment.Remove(slotType);

        return changed;
    }

    private static bool ConsumeFromInstance(
        ItemInstance instance,
        ItemDefinition definition,
        HashSet<ItemInstance> visited,
        ref int remaining)
    {
        if (instance == null || instance.Definition == null || instance.IsDepleted() || remaining <= 0)
            return false;

        bool changed = false;

        if (IsTarget(instance, definition))
        {
            int removed = instance.RemoveQuantity(remaining);
            remaining -= removed;
            changed = removed > 0;

            if (remaining <= 0)
                return changed;
        }

        if (!instance.IsContainer || instance.ContainerState == null)
            return changed;

        if (!visited.Add(instance))
            return changed;

        ItemContainerState state = instance.ContainerState;
        bool nestedChanged = false;

        for (int i = 0; i < state.SlotCount && remaining > 0; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (ConsumeFromSlot(slot, definition, visited, ref remaining))
                nestedChanged = true;
        }

        if (nestedChanged)
            state.NotifyChanged();

        return changed || nestedChanged;
    }

    private static bool IsTarget(ItemInstance instance, ItemDefinition definition)
    {
        if (instance == null || definition == null || instance.Definition == null)
            return false;

        if (ReferenceEquals(instance.Definition, definition))
            return true;

        return !string.IsNullOrWhiteSpace(definition.ItemId) &&
               string.Equals(instance.Definition.ItemId, definition.ItemId, System.StringComparison.Ordinal);
    }

    private static PlayerEquipment FindEquipment(PlayerInventory inventory)
    {
        if (inventory == null)
            return null;

        return inventory.GetComponentInParent<PlayerEquipment>(true) ??
               inventory.GetComponentInChildren<PlayerEquipment>(true);
    }
}