using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Transaction helper for consuming one charting-paper item from the exact requester's
/// carried inventory, including nested portable containers. It never searches for "the player".
/// </summary>
public static class CelestialChartPaperConsumption
{
    public sealed class Receipt
    {
        internal PlayerInventory inventory;
        internal PlayerEquipment equipment;
        internal BottomBarSlotType equipmentSlot;
        internal InventorySlot slot;
        internal ItemContainerState parentContainer;
        internal ItemInstance item;
        internal bool removedWholeItem;
        internal bool fromEquipment;
        internal bool quantityDecremented;
        internal bool committed;

        public void Commit()
        {
            committed = true;
        }

        public void Rollback()
        {
            if (committed || item == null)
                return;

            if (quantityDecremented)
            {
                item.AddQuantity(1);
            }
            else if (removedWholeItem)
            {
                if (fromEquipment)
                {
                    if (equipment != null)
                        equipment.TryPlace(equipmentSlot, item, out _);
                }
                else if (slot != null && slot.IsEmpty)
                {
                    slot.Set(item);
                    parentContainer?.NotifyChanged();
                }
            }

            inventory?.NotifyChanged();
            committed = true;
        }
    }

    public static bool TryConsumeOne(
        GameObject requester,
        ItemDefinition paperDefinition,
        out Receipt receipt,
        out string error)
    {
        receipt = null;
        error = null;

        if (requester == null)
        {
            error = "No charting requester was supplied.";
            return false;
        }

        if (paperDefinition == null || string.IsNullOrWhiteSpace(paperDefinition.ItemId))
        {
            error = "Charting Paper is not configured.";
            return false;
        }

        PlayerInventory inventory = ResolveInventory(requester);
        if (inventory == null)
        {
            error = "The charting player has no PlayerInventory.";
            return false;
        }

        var visited = new HashSet<ItemInstance>();

        PlayerEquipment equipment = inventory.Equipment;
        if (equipment != null)
        {
            if (TryConsumeFromEquipmentSlot(inventory, equipment, BottomBarSlotType.Hands, paperDefinition, visited, out receipt))
                return true;
        }

        for (int i = 0; i < inventory.HotbarSlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (TryConsumeFromSlot(inventory, slot, null, paperDefinition, visited, out receipt))
                return true;
        }

        if (equipment != null)
        {
            BottomBarSlotType[] slots =
            {
                BottomBarSlotType.Backpack,
                BottomBarSlotType.Toolbelt,
                BottomBarSlotType.Body,
                BottomBarSlotType.Head,
                BottomBarSlotType.Feet
            };

            for (int i = 0; i < slots.Length; i++)
            {
                if (TryConsumeFromEquipmentSlot(inventory, equipment, slots[i], paperDefinition, visited, out receipt))
                    return true;
            }
        }

        error = $"You need 1 {paperDefinition.DisplayName} to record this chart.";
        return false;
    }

    public static PlayerInventory ResolveInventory(GameObject requester)
    {
        if (requester == null)
            return null;

        return requester.GetComponent<PlayerInventory>() ??
               requester.GetComponentInParent<PlayerInventory>(true) ??
               requester.GetComponentInChildren<PlayerInventory>(true);
    }

    /// <summary>Only the instrument's direct filtered storage; never falls back to pockets.</summary>
    public static bool TryConsumeFromInstrument(ItemInstance instrument, ItemDefinition paperDefinition,
        out Receipt receipt, out string error)
    {
        receipt = null;
        error = "Load 1 Charting Paper into the charting instrument.";
        if (instrument == null || instrument.IsDepleted() || instrument.ChartingInstrument?.invalidated == true ||
            paperDefinition == null || !instrument.IsContainer || instrument.ContainerState == null)
            return false;
        var state = instrument.ContainerState;
        for (int i = 0; i < state.SlotCount; i++)
        {
            var slot = state.GetSlot(i);
            if (slot?.Instance == null || !Matches(slot.Instance, paperDefinition)) continue;
            receipt = ConsumeSlotItem(null, slot, state, slot.Instance);
            if (receipt != null) { error = null; return true; }
        }
        return false;
    }

    private static bool TryConsumeFromEquipmentSlot(
        PlayerInventory inventory,
        PlayerEquipment equipment,
        BottomBarSlotType slotType,
        ItemDefinition paperDefinition,
        HashSet<ItemInstance> visited,
        out Receipt receipt)
    {
        receipt = null;
        ItemInstance item = equipment.Get(slotType);
        if (item == null)
            return false;

        if (!visited.Add(item))
            return false;

        if (Matches(item, paperDefinition))
        {
            receipt = ConsumeEquipmentItem(inventory, equipment, slotType, item);
            return receipt != null;
        }

        return TryConsumeFromContainedItems(inventory, item, paperDefinition, visited, out receipt);
    }

    private static bool TryConsumeFromSlot(
        PlayerInventory inventory,
        InventorySlot slot,
        ItemContainerState parentContainer,
        ItemDefinition paperDefinition,
        HashSet<ItemInstance> visited,
        out Receipt receipt)
    {
        receipt = null;
        if (slot == null || slot.IsEmpty || slot.Instance == null)
            return false;

        ItemInstance item = slot.Instance;
        if (!visited.Add(item))
            return false;

        if (Matches(item, paperDefinition))
        {
            receipt = ConsumeSlotItem(inventory, slot, parentContainer, item);
            return receipt != null;
        }

        return TryConsumeFromContainedItems(inventory, item, paperDefinition, visited, out receipt);
    }

    private static bool TryConsumeFromContainedItems(
        PlayerInventory inventory,
        ItemInstance containerItem,
        ItemDefinition paperDefinition,
        HashSet<ItemInstance> visited,
        out Receipt receipt)
    {
        receipt = null;
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
            return false;

        ItemContainerState container = containerItem.ContainerState;
        for (int i = 0; i < container.SlotCount; i++)
        {
            if (TryConsumeFromSlot(inventory, container.GetSlot(i), container, paperDefinition, visited, out receipt))
                return true;
        }

        return false;
    }

    private static Receipt ConsumeSlotItem(
        PlayerInventory inventory,
        InventorySlot slot,
        ItemContainerState parentContainer,
        ItemInstance item)
    {
        if (item.Quantity <= 1)
        {
            slot.Clear();
            parentContainer?.NotifyChanged();
            inventory?.NotifyChanged();
            return new Receipt
            {
                inventory = inventory,
                slot = slot,
                parentContainer = parentContainer,
                item = item,
                removedWholeItem = true,
                fromEquipment = false
            };
        }

        int removed = item.RemoveQuantity(1);
        if (removed != 1)
            return null;

        parentContainer?.NotifyChanged();
        inventory?.NotifyChanged();
        return new Receipt
        {
            inventory = inventory,
            slot = slot,
            parentContainer = parentContainer,
            item = item,
            quantityDecremented = true
        };
    }

    private static Receipt ConsumeEquipmentItem(
        PlayerInventory inventory,
        PlayerEquipment equipment,
        BottomBarSlotType slotType,
        ItemInstance item)
    {
        if (item.Quantity <= 1)
        {
            ItemInstance removed = equipment.Remove(slotType);
            if (removed == null)
                return null;

            inventory.NotifyChanged();
            return new Receipt
            {
                inventory = inventory,
                equipment = equipment,
                equipmentSlot = slotType,
                item = removed,
                removedWholeItem = true,
                fromEquipment = true
            };
        }

        int amount = item.RemoveQuantity(1);
        if (amount != 1)
            return null;

        inventory.NotifyChanged();
        return new Receipt
        {
            inventory = inventory,
            equipment = equipment,
            equipmentSlot = slotType,
            item = item,
            quantityDecremented = true,
            fromEquipment = true
        };
    }

    private static bool Matches(ItemInstance item, ItemDefinition paperDefinition)
    {
        return item != null &&
               item.Definition != null &&
               item.Quantity > 0 &&
               item.Definition.ItemId == paperDefinition.ItemId;
    }
}
