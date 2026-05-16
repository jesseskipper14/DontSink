using UnityEngine;

public static class ContainerPlacementUtility
{
    // ---------------------------------------------------------------------
    // Existing item-container API
    // Used by portable containers like chests, backpacks, etc.
    // ---------------------------------------------------------------------

    public static bool CanPlaceIntoSlot(ItemInstance containerItem, int slotIndex, ItemInstance incoming)
    {
        if (!TryGetValidatedContext(containerItem, slotIndex, incoming, out InventorySlot slot))
            return false;

        if (slot.IsEmpty || slot.Instance == null)
            return true;

        if (slot.Instance.CanStackWith(incoming) && slot.Instance.RemainingStackSpace > 0)
            return true;

        return false;
    }

    public static bool TryPlaceIntoSlot(
        ItemInstance containerItem,
        int slotIndex,
        ItemInstance incoming,
        out ItemInstance remainder,
        out ItemInstance displaced)
    {
        remainder = incoming;
        displaced = null;

        if (!TryGetValidatedContext(containerItem, slotIndex, incoming, out InventorySlot slot))
            return false;

        // Empty slot
        if (slot.IsEmpty || slot.Instance == null)
        {
            slot.Set(incoming);
            remainder = null;
            containerItem.ContainerState.NotifyChanged();
            return true;
        }

        ItemInstance existing = slot.Instance;

        // Stack merge
        if (existing.CanStackWith(incoming) && existing.RemainingStackSpace > 0)
        {
            int moved = existing.AddQuantity(incoming.Quantity);
            if (moved <= 0)
                return false;

            incoming.RemoveQuantity(moved);
            remainder = incoming.IsDepleted() ? null : incoming;
            containerItem.ContainerState.NotifyChanged();
            return true;
        }

        // Swap.
        // Important: incoming already passed container rules.
        // For swap, the displaced existing item must also be valid for this container,
        // otherwise the slot could have become invalid through old data or rule changes.
        if (!containerItem.Definition.CanContainerAccept(existing.Definition))
            return false;

        slot.Set(incoming);
        displaced = existing;
        remainder = null;
        containerItem.ContainerState.NotifyChanged();
        return true;
    }

    public static bool CanAutoInsert(ItemInstance containerItem, ItemInstance incoming)
    {
        if (!TryGetValidatedContainer(containerItem, incoming, out ItemContainerState state))
            return false;

        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null)
                continue;

            if (slot.IsEmpty || slot.Instance == null)
                return true;

            if (slot.Instance.CanStackWith(incoming) && slot.Instance.RemainingStackSpace > 0)
                return true;
        }

        return false;
    }

    public static bool TryAutoInsert(ItemInstance containerItem, ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (!TryGetValidatedContainer(containerItem, incoming, out ItemContainerState state))
            return false;

        bool changed = false;

        // 1) Stack first
        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            if (!slot.Instance.CanStackWith(incoming))
                continue;

            int moved = slot.Instance.AddQuantity(incoming.Quantity);
            if (moved <= 0)
                continue;

            incoming.RemoveQuantity(moved);
            changed = true;

            if (incoming.IsDepleted())
            {
                remainder = null;
                state.NotifyChanged();
                return true;
            }
        }

        // 2) Then empty slot
        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || !slot.IsEmpty)
                continue;

            slot.Set(incoming);
            remainder = null;
            state.NotifyChanged();
            return true;
        }

        if (changed)
            state.NotifyChanged();

        remainder = incoming;
        return false;
    }

    private static bool TryGetValidatedContext(
        ItemInstance containerItem,
        int slotIndex,
        ItemInstance incoming,
        out InventorySlot slot)
    {
        slot = null;

        if (!TryGetValidatedContainer(containerItem, incoming, out ItemContainerState state))
            return false;

        if (slotIndex < 0 || slotIndex >= state.SlotCount)
            return false;

        slot = state.GetSlot(slotIndex);
        return slot != null;
    }

    private static bool TryGetValidatedContainer(
        ItemInstance containerItem,
        ItemInstance incoming,
        out ItemContainerState state)
    {
        state = null;

        if (containerItem == null || incoming == null)
            return false;

        if (!containerItem.IsContainer || containerItem.Definition == null)
            return false;

        state = containerItem.ContainerState;
        if (state == null)
            return false;

        if (incoming.Definition == null)
            return false;

        return containerItem.Definition.CanContainerAccept(incoming.Definition);
    }

    // ---------------------------------------------------------------------
    // Installed storage-module API
    // Used by lockers, cabinets, racks, cargo shelves, etc.
    // ---------------------------------------------------------------------

    public static bool CanPlaceIntoSlot(StorageModule storageModule, int slotIndex, ItemInstance incoming)
    {
        if (!TryGetValidatedContext(storageModule, slotIndex, incoming, out InventorySlot slot))
            return false;

        if (slot.IsEmpty || slot.Instance == null)
            return true;

        if (slot.Instance.CanStackWith(incoming) && slot.Instance.RemainingStackSpace > 0)
            return true;

        return false;
    }

    public static bool TryPlaceIntoSlot(
        StorageModule storageModule,
        int slotIndex,
        ItemInstance incoming,
        out ItemInstance remainder,
        out ItemInstance displaced)
    {
        remainder = incoming;
        displaced = null;

        if (!TryGetValidatedContext(storageModule, slotIndex, incoming, out InventorySlot slot))
            return false;

        ItemContainerState state = storageModule.ContainerState;
        if (state == null)
            return false;

        // Empty slot
        if (slot.IsEmpty || slot.Instance == null)
        {
            slot.Set(incoming);
            remainder = null;
            state.NotifyChanged();
            return true;
        }

        ItemInstance existing = slot.Instance;

        // Stack merge
        if (existing.CanStackWith(incoming) && existing.RemainingStackSpace > 0)
        {
            int moved = existing.AddQuantity(incoming.Quantity);
            if (moved <= 0)
                return false;

            incoming.RemoveQuantity(moved);
            remainder = incoming.IsDepleted() ? null : incoming;
            state.NotifyChanged();
            return true;
        }

        // Swap.
        // Incoming already passed storage rules.
        // Existing must also be valid for this storage module, or we refuse the swap.
        if (!storageModule.CanAcceptItem(existing))
            return false;

        slot.Set(incoming);
        displaced = existing;
        remainder = null;
        state.NotifyChanged();
        return true;
    }

    public static bool CanAutoInsert(StorageModule storageModule, ItemInstance incoming)
    {
        if (!TryGetValidatedContainer(storageModule, incoming, out ItemContainerState state))
            return false;

        for (int i = 0; i < state.SlotCount; i++)
        {
            if (!storageModule.CanAcceptItemInSlot(incoming, i))
                continue;

            InventorySlot slot = state.GetSlot(i);
            if (slot == null)
                continue;

            if (slot.IsEmpty || slot.Instance == null)
                return true;

            if (slot.Instance.CanStackWith(incoming) && slot.Instance.RemainingStackSpace > 0)
                return true;
        }

        return false;
    }

    public static bool TryAutoInsert(StorageModule storageModule, ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (!TryGetValidatedContainer(storageModule, incoming, out ItemContainerState state))
            return false;

        bool changed = false;

        // 1) Stack first
        for (int i = 0; i < state.SlotCount; i++)
        {
            if (!storageModule.CanAcceptItemInSlot(incoming, i))
                continue;

            InventorySlot slot = state.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            if (!slot.Instance.CanStackWith(incoming))
                continue;

            int moved = slot.Instance.AddQuantity(incoming.Quantity);
            if (moved <= 0)
                continue;

            incoming.RemoveQuantity(moved);
            changed = true;

            if (incoming.IsDepleted())
            {
                remainder = null;
                state.NotifyChanged();
                return true;
            }
        }

        // 2) Then empty slot
        for (int i = 0; i < state.SlotCount; i++)
        {
            if (!storageModule.CanAcceptItemInSlot(incoming, i))
                continue;

            InventorySlot slot = state.GetSlot(i);
            if (slot == null || !slot.IsEmpty)
                continue;

            slot.Set(incoming);
            remainder = null;
            state.NotifyChanged();
            return true;
        }

        if (changed)
            state.NotifyChanged();

        remainder = incoming;
        return false;
    }

    private static bool TryGetValidatedContext(
        StorageModule storageModule,
        int slotIndex,
        ItemInstance incoming,
        out InventorySlot slot)
    {
        slot = null;

        if (!TryGetValidatedContainer(storageModule, incoming, out ItemContainerState state))
            return false;

        if (slotIndex < 0 || slotIndex >= state.SlotCount)
            return false;

        if (!storageModule.CanAcceptItemInSlot(incoming, slotIndex))
            return false;

        slot = state.GetSlot(slotIndex);
        return slot != null;
    }

    private static bool TryGetValidatedContainer(
        StorageModule storageModule,
        ItemInstance incoming,
        out ItemContainerState state)
    {
        state = null;

        if (storageModule == null || incoming == null)
            return false;

        storageModule.EnsureContainer();

        state = storageModule.ContainerState;
        if (state == null)
            return false;

        if (incoming.Definition == null)
            return false;

        return storageModule.CanAcceptItem(incoming);
    }
}