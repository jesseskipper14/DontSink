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

        int slotLimit = GetPortableSlotQuantityLimit(containerItem, incoming);

        if (slot.IsEmpty || slot.Instance == null)
            return slotLimit > 0;

        if (!slot.Instance.CanStackWith(incoming))
            return false;

        int roomByContainerRule = Mathf.Max(0, slotLimit - slot.Instance.Quantity);

        return roomByContainerRule > 0 &&
               slot.Instance.RemainingStackSpace > 0;
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

        int slotLimit = GetPortableSlotQuantityLimit(containerItem, incoming);

        // Empty slot: place only as much as this container allows in one slot.
        if (slot.IsEmpty || slot.Instance == null)
        {
            if (!TryTakeQuantityForEmptyPortableSlot(
                    incoming,
                    slotLimit,
                    out ItemInstance placed,
                    out remainder))
            {
                return false;
            }

            slot.Set(placed);
            containerItem.ContainerState.NotifyChanged();
            return true;
        }

        ItemInstance existing = slot.Instance;

        // Stack merge, respecting BOTH the item's normal MaxStack and the
        // portable container's per-slot quantity cap.
        if (existing.CanStackWith(incoming))
        {
            int roomByContainerRule =
                Mathf.Max(
                    0,
                    slotLimit - existing.Quantity);

            int movable =
                Mathf.Min(
                    incoming.Quantity,
                    Mathf.Min(
                        existing.RemainingStackSpace,
                        roomByContainerRule));

            if (movable > 0)
            {
                int moved = existing.AddQuantity(movable);
                if (moved <= 0)
                    return false;

                incoming.RemoveQuantity(moved);
                remainder = incoming.IsDepleted() ? null : incoming;
                containerItem.ContainerState.NotifyChanged();
                return true;
            }
        }

        // Swap.
        // Do not perform a PARTIAL swap. ContainerSlotBinding exposes only one
        // displaced-item output, so partially placing an oversized incoming stack
        // while also displacing the old item would create two return values that
        // the current binding cannot safely represent.
        if (incoming.Quantity > slotLimit)
            return false;

        // Incoming already passed container rules.
        // Existing must also still be valid for this container.
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

        int slotLimit = GetPortableSlotQuantityLimit(containerItem, incoming);

        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null)
                continue;

            if (slot.IsEmpty || slot.Instance == null)
                return slotLimit > 0;

            if (!slot.Instance.CanStackWith(incoming))
                continue;

            int roomByContainerRule =
                Mathf.Max(
                    0,
                    slotLimit - slot.Instance.Quantity);

            if (roomByContainerRule > 0 &&
                slot.Instance.RemainingStackSpace > 0)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryAutoInsert(ItemInstance containerItem, ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (!TryGetValidatedContainer(containerItem, incoming, out ItemContainerState state))
            return false;

        int slotLimit = GetPortableSlotQuantityLimit(containerItem, incoming);
        bool changed = false;

        // 1) Fill compatible existing stacks first, but never above the
        // portable container's per-slot cap.
        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            ItemInstance existing = slot.Instance;

            if (!existing.CanStackWith(incoming))
                continue;

            int roomByContainerRule =
                Mathf.Max(
                    0,
                    slotLimit - existing.Quantity);

            int movable =
                Mathf.Min(
                    incoming.Quantity,
                    Mathf.Min(
                        existing.RemainingStackSpace,
                        roomByContainerRule));

            if (movable <= 0)
                continue;

            int moved = existing.AddQuantity(movable);
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

        // 2) Fill empty slots. With Max Quantity Per Slot = 1, a stack of
        // eight rope items dropped onto a four-slot sounder becomes four
        // one-rope slots plus a remainder stack of four.
        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || !slot.IsEmpty)
                continue;

            if (!TryTakeQuantityForEmptyPortableSlot(
                    incoming,
                    slotLimit,
                    out ItemInstance placed,
                    out ItemInstance slotRemainder))
            {
                continue;
            }

            slot.Set(placed);
            incoming = slotRemainder;
            changed = true;

            if (incoming == null ||
                incoming.IsDepleted())
            {
                remainder = null;
                state.NotifyChanged();
                return true;
            }
        }

        if (changed)
        {
            state.NotifyChanged();
            remainder = incoming;

            // Partial insertion is still a successful insertion.
            // Callers already receive the leftover stack through remainder.
            return true;
        }

        remainder = incoming;
        return false;
    }

    private static bool TryTakeQuantityForEmptyPortableSlot(
        ItemInstance incoming,
        int slotLimit,
        out ItemInstance placed,
        out ItemInstance remainder)
    {
        placed = null;
        remainder = incoming;

        if (incoming == null ||
            incoming.Definition == null ||
            incoming.IsDepleted() ||
            slotLimit <= 0)
        {
            return false;
        }

        int quantityToPlace =
            Mathf.Min(
                incoming.Quantity,
                slotLimit);

        if (quantityToPlace <= 0)
            return false;

        if (quantityToPlace >= incoming.Quantity)
        {
            placed = incoming;
            remainder = null;
            return true;
        }

        // A quantity greater than one can only exist on a stackable item, so
        // SplitOff is the correct identity-preserving way to peel off the
        // amount that belongs in this slot.
        placed =
            incoming.SplitOff(
                quantityToPlace);

        if (placed == null)
            return false;

        remainder =
            incoming.IsDepleted()
                ? null
                : incoming;

        return true;
    }

    private static int GetPortableSlotQuantityLimit(
        ItemInstance containerItem,
        ItemInstance incoming)
    {
        if (incoming == null)
            return 0;

        int normalItemLimit =
            Mathf.Max(
                1,
                incoming.MaxStack);

        int configuredLimit =
            containerItem != null &&
            containerItem.Definition != null
                ? containerItem.Definition.ContainerMaxQuantityPerSlot
                : 0;

        if (configuredLimit <= 0)
            return normalItemLimit;

        return Mathf.Max(
            1,
            Mathf.Min(
                normalItemLimit,
                configuredLimit));
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