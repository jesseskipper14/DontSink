using UnityEngine;

/// <summary>
/// Authoritative transaction boundary for winch tether-line inventory transfers.
///
/// The cartridge only emits intent. This class resolves the live requester-owned
/// inventory/equipment and the live winch slot again at execution time, validates
/// the expected ItemInstance id, and performs the mutation with rollback where
/// practical.
///
/// Today WinchOverlayRunner invokes this locally on the authoritative peer. A
/// future network transport can deliver the same payload + authenticated requester
/// identity to host authority without moving mutation logic back into UI code.
/// </summary>
public static class WinchLineTransferAuthority
{
    public static bool TryApply(
        WinchModule winch,
        GameObject requester,
        WinchLineTransferIntentPayload request,
        out string message)
    {
        message = null;

        if (!GameplayAuthority.IsAuthoritative)
        {
            message = "WINCH LINE TRANSFER REQUIRES AUTHORITY";
            return false;
        }

        if (winch == null)
        {
            message = "WINCH IS NO LONGER AVAILABLE";
            return false;
        }

        if (requester == null)
        {
            message = "WINCH REQUESTER IS UNAVAILABLE";
            return false;
        }

        if (request == null)
        {
            message = "MISSING WINCH LINE TRANSFER REQUEST";
            return false;
        }

        if (request.version != WinchLineTransferIntentPayload.CurrentVersion)
        {
            message = $"UNSUPPORTED WINCH LINE TRANSFER VERSION {request.version}";
            return false;
        }

        if (request.winchSlotIndex < 0 ||
            request.winchSlotIndex >= winch.LineSlotCount)
        {
            message = "INVALID WINCH LINE SLOT";
            return false;
        }

        if (winch.HasDeployedPayload)
        {
            message = "STOW PAYLOAD BEFORE CHANGING LINE";
            return false;
        }

        PlayerInventory inventory =
            ResolveRequesterInventory(
                requester);

        PlayerEquipment equipment =
            ResolveRequesterEquipment(
                requester,
                inventory);

        switch (request.operation)
        {
            case WinchLineTransferOperation.LoadOneFromPlayer:
                return TryLoadOne(
                    winch,
                    inventory,
                    equipment,
                    request,
                    out message);

            case WinchLineTransferOperation.UnloadToPlayer:
                return TryUnload(
                    winch,
                    inventory,
                    request,
                    out message);

            default:
                message = "UNKNOWN WINCH LINE TRANSFER OPERATION";
                return false;
        }
    }

    private static bool TryLoadOne(
        WinchModule winch,
        PlayerInventory inventory,
        PlayerEquipment equipment,
        WinchLineTransferIntentPayload request,
        out string message)
    {
        message = null;

        WinchLineSlotBinding binding =
            new WinchLineSlotBinding(
                winch,
                request.winchSlotIndex);

        if (binding.GetItem() != null)
        {
            message = "LINE SLOT IS ALREADY OCCUPIED";
            return false;
        }

        if (!TryResolveRequestedPlayerSource(
                inventory,
                equipment,
                request,
                out ItemInstance sourceItem,
                out InventorySlot hotbarSlot))
        {
            message = "REQUESTED TETHER ITEM IS NO LONGER AVAILABLE";
            return false;
        }

        if (sourceItem == null ||
            sourceItem.Definition == null ||
            sourceItem.Quantity <= 0 ||
            !InstanceMatches(
                sourceItem,
                request.expectedInstanceId) ||
            !winch.CanAcceptLine(
                sourceItem))
        {
            message = "REQUESTED TETHER ITEM CHANGED OR IS INCOMPATIBLE";
            return false;
        }

        if (!TryTakeOneFromRequestedSource(
                inventory,
                equipment,
                request,
                sourceItem,
                hotbarSlot,
                out ItemInstance oneLine))
        {
            message = "COULD NOT TAKE REQUESTED TETHER ITEM";
            return false;
        }

        if (oneLine == null ||
            oneLine.Quantity != 1)
        {
            ReturnLineToRequestedPlayerSource(
                inventory,
                equipment,
                request,
                oneLine);

            message = "TETHER STACK COULD NOT BE SPLIT";
            return false;
        }

        if (!binding.TryPlaceItem(
                oneLine,
                out ItemInstance displaced))
        {
            ReturnLineToRequestedPlayerSource(
                inventory,
                equipment,
                request,
                oneLine);

            message = "WINCH REJECTED TETHER ITEM";
            return false;
        }

        if (displaced != null &&
            !displaced.IsDepleted())
        {
            // This should be impossible because we validated an empty slot. Restore
            // the unexpected previous winch state rather than quietly stealing it.
            binding.RemoveItem();

            if (displaced.Quantity == 1)
            {
                binding.TryPlaceItem(
                    displaced,
                    out _);
            }

            ReturnLineToRequestedPlayerSource(
                inventory,
                equipment,
                request,
                oneLine);

            message = "WINCH LINE SLOT CHANGED DURING TRANSFER";
            return false;
        }

        inventory?.NotifyChanged();

        message =
            oneLine.Definition != null
                ? $"LOADED 1 {oneLine.Definition.DisplayName}"
                : "LOADED TETHER ITEM";

        return true;
    }

    private static bool TryUnload(
        WinchModule winch,
        PlayerInventory inventory,
        WinchLineTransferIntentPayload request,
        out string message)
    {
        message = null;

        if (inventory == null)
        {
            message = "PLAYER INVENTORY IS UNAVAILABLE";
            return false;
        }

        WinchLineSlotBinding binding =
            new WinchLineSlotBinding(
                winch,
                request.winchSlotIndex);

        ItemInstance loaded =
            binding.GetItem();

        if (loaded == null)
        {
            message = "LINE SLOT IS EMPTY";
            return false;
        }

        if (!InstanceMatches(
                loaded,
                request.expectedInstanceId))
        {
            message = "WINCH LINE SLOT CHANGED BEFORE TRANSFER";
            return false;
        }

        if (!inventory.CanFullyAdd(
                loaded))
        {
            message = "NO INVENTORY ROOM TO UNLOAD LINE";
            return false;
        }

        ItemInstance removed =
            binding.RemoveItem();

        if (removed == null)
        {
            message = "COULD NOT REMOVE LINE";
            return false;
        }

        if (!inventory.TryAutoInsert(
                removed,
                out ItemInstance remainder) ||
            (remainder != null &&
             !remainder.IsDepleted()))
        {
            // Preserve the existing historical-stack safety rule: legal one-item
            // line slots can be rolled back directly. A legacy illegal stack is
            // returned to the player inventory rather than inventing another bad slot.
            if (removed.Quantity == 1)
            {
                binding.TryPlaceItem(
                    removed,
                    out _);
            }
            else
            {
                inventory.TryAutoInsert(
                    removed,
                    out _);
            }

            inventory.NotifyChanged();

            message = "FAILED TO RETURN LINE TO INVENTORY";
            return false;
        }

        inventory.NotifyChanged();

        message =
            removed.Definition != null
                ? $"UNLOADED {removed.Definition.DisplayName}"
                : "UNLOADED TETHER ITEM";

        return true;
    }

    private static bool TryResolveRequestedPlayerSource(
        PlayerInventory inventory,
        PlayerEquipment equipment,
        WinchLineTransferIntentPayload request,
        out ItemInstance item,
        out InventorySlot hotbarSlot)
    {
        item = null;
        hotbarSlot = null;

        switch (request.playerSourceKind)
        {
            case WinchLinePlayerSourceKind.Hotbar:
                if (inventory == null ||
                    request.playerHotbarIndex < 0 ||
                    request.playerHotbarIndex >= inventory.HotbarSlotCount)
                {
                    return false;
                }

                hotbarSlot =
                    inventory.GetSlot(
                        request.playerHotbarIndex);

                item =
                    hotbarSlot != null
                        ? hotbarSlot.Instance
                        : null;

                return item != null;

            case WinchLinePlayerSourceKind.Hands:
                if (equipment == null)
                    return false;

                item =
                    equipment.Get(
                        BottomBarSlotType.Hands);

                return item != null;

            default:
                return false;
        }
    }

    private static bool TryTakeOneFromRequestedSource(
        PlayerInventory inventory,
        PlayerEquipment equipment,
        WinchLineTransferIntentPayload request,
        ItemInstance sourceItem,
        InventorySlot hotbarSlot,
        out ItemInstance oneLine)
    {
        oneLine = null;

        switch (request.playerSourceKind)
        {
            case WinchLinePlayerSourceKind.Hotbar:
                {
                    if (inventory == null ||
                        hotbarSlot == null ||
                        hotbarSlot.IsEmpty ||
                        hotbarSlot.Instance == null ||
                        !ReferenceEquals(
                            hotbarSlot.Instance,
                            sourceItem))
                    {
                        return false;
                    }

                    if (sourceItem.Quantity > 1)
                    {
                        if (!sourceItem.CanSplit)
                            return false;

                        oneLine =
                            sourceItem.SplitOff(
                                1);

                        if (oneLine == null)
                            return false;
                    }
                    else
                    {
                        oneLine =
                            sourceItem;

                        hotbarSlot.Clear();
                    }

                    inventory.NotifyChanged();
                    return true;
                }

            case WinchLinePlayerSourceKind.Hands:
                {
                    if (equipment == null)
                        return false;

                    ItemInstance current =
                        equipment.Get(
                            BottomBarSlotType.Hands);

                    if (current == null ||
                        !ReferenceEquals(
                            current,
                            sourceItem))
                    {
                        return false;
                    }

                    ItemInstance removed =
                        equipment.Remove(
                            BottomBarSlotType.Hands);

                    if (removed == null)
                        return false;

                    if (removed.Quantity > 1)
                    {
                        if (!removed.CanSplit)
                        {
                            equipment.TryPlace(
                                BottomBarSlotType.Hands,
                                removed,
                                out _);

                            return false;
                        }

                        oneLine =
                            removed.SplitOff(
                                1);

                        if (oneLine == null)
                        {
                            equipment.TryPlace(
                                BottomBarSlotType.Hands,
                                removed,
                                out _);

                            return false;
                        }

                        if (!equipment.TryPlace(
                                BottomBarSlotType.Hands,
                                removed,
                                out ItemInstance displaced) ||
                            displaced != null)
                        {
                            if (removed.CanStackWith(
                                    oneLine))
                            {
                                int moved =
                                    removed.AddQuantity(
                                        oneLine.Quantity);

                                oneLine.RemoveQuantity(
                                    moved);
                            }

                            inventory?.TryAutoInsert(
                                removed,
                                out _);

                            inventory?.NotifyChanged();

                            return false;
                        }
                    }
                    else
                    {
                        oneLine =
                            removed;
                    }

                    inventory?.NotifyChanged();
                    return true;
                }
        }

        return false;
    }

    private static void ReturnLineToRequestedPlayerSource(
        PlayerInventory inventory,
        PlayerEquipment equipment,
        WinchLineTransferIntentPayload request,
        ItemInstance item)
    {
        if (item == null ||
            item.IsDepleted())
        {
            return;
        }

        if (request.playerSourceKind ==
                WinchLinePlayerSourceKind.Hotbar &&
            inventory != null &&
            request.playerHotbarIndex >= 0 &&
            request.playerHotbarIndex < inventory.HotbarSlotCount)
        {
            InventorySlot slot =
                inventory.GetSlot(
                    request.playerHotbarIndex);

            if (slot != null)
            {
                if (slot.IsEmpty)
                {
                    slot.Set(
                        item);

                    inventory.NotifyChanged();
                    return;
                }

                if (slot.Instance != null &&
                    slot.Instance.CanStackWith(
                        item))
                {
                    int moved =
                        slot.Instance.AddQuantity(
                            item.Quantity);

                    item.RemoveQuantity(
                        moved);

                    if (item.IsDepleted())
                    {
                        inventory.NotifyChanged();
                        return;
                    }
                }
            }
        }

        if (request.playerSourceKind ==
                WinchLinePlayerSourceKind.Hands &&
            equipment != null &&
            equipment.Get(
                BottomBarSlotType.Hands) ==
            null)
        {
            if (equipment.TryPlace(
                    BottomBarSlotType.Hands,
                    item,
                    out ItemInstance displaced) &&
                displaced == null)
            {
                inventory?.NotifyChanged();
                return;
            }
        }

        if (inventory != null)
        {
            inventory.TryAutoInsert(
                item,
                out _);

            inventory.NotifyChanged();
        }
    }

    private static bool InstanceMatches(
        ItemInstance item,
        string expectedInstanceId)
    {
        return
            item != null &&
            !string.IsNullOrWhiteSpace(
                expectedInstanceId) &&
            item.InstanceId ==
                expectedInstanceId;
    }

    private static PlayerInventory ResolveRequesterInventory(
        GameObject requester)
    {
        if (requester == null)
            return null;

        return
            requester.GetComponent<PlayerInventory>() ??
            requester.GetComponentInChildren<PlayerInventory>(true) ??
            requester.GetComponentInParent<PlayerInventory>(true);
    }

    private static PlayerEquipment ResolveRequesterEquipment(
        GameObject requester,
        PlayerInventory inventory)
    {
        if (inventory != null &&
            inventory.Equipment != null)
        {
            return inventory.Equipment;
        }

        if (requester == null)
            return null;

        return
            requester.GetComponent<PlayerEquipment>() ??
            requester.GetComponentInChildren<PlayerEquipment>(true) ??
            requester.GetComponentInParent<PlayerEquipment>(true);
    }
}
