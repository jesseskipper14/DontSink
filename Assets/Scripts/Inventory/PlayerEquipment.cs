using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerEquipment : MonoBehaviour
{
    [SerializeReference] private ItemInstance hands;
    [SerializeReference] private ItemInstance head;
    [SerializeReference] private ItemInstance feet;
    [SerializeReference] private ItemInstance toolbelt;
    [SerializeReference] private ItemInstance backpack;
    [SerializeReference] private ItemInstance body;

    public event Action EquipmentChanged;

    public ItemInstance Get(BottomBarSlotType slot)
    {
        ItemInstance raw = GetDirect(slot);
        return IsValidEquippedItem(raw) ? raw : null;
    }

    public bool IsSlotOccupiedOrBlocked(BottomBarSlotType slot)
    {
        return TryGetOccupyingItem(slot, out _, out _);
    }

    public bool TryGetOccupyingItem(
        BottomBarSlotType slot,
        out ItemInstance occupyingItem,
        out BottomBarSlotType anchorSlot)
    {
        occupyingItem = null;
        anchorSlot = BottomBarSlotType.None;

        ItemInstance direct = Get(slot);
        if (direct != null)
        {
            occupyingItem = direct;
            anchorSlot = slot;
            return true;
        }

        foreach (BottomBarSlotType candidateAnchor in EnumerateEquipmentSlots(includeHands: false))
        {
            if (candidateAnchor == slot)
                continue;

            ItemInstance candidate = Get(candidateAnchor);
            if (candidate == null || candidate.Definition == null)
                continue;

            if (candidate.Definition.OccupiesEquipSlot(candidateAnchor, slot))
            {
                occupyingItem = candidate;
                anchorSlot = candidateAnchor;
                return true;
            }
        }

        return false;
    }

    public ItemInstance Remove(BottomBarSlotType slot)
    {
        // V1 rule:
        // Only the anchor slot actually owns/removes the item.
        // A blocked/proxy slot returns null.
        ItemInstance current = Get(slot);
        if (current == null)
            return null;

        SetDirect(slot, null);
        EquipmentChanged?.Invoke();
        return current;
    }

    public bool TryPlace(BottomBarSlotType slot, ItemInstance item, out ItemInstance displaced)
    {
        displaced = null;

        if (item == null || item.Definition == null)
            return false;

        if (!CanEquip(slot, item))
            return false;

        // V1 rule:
        // No displacement. If anything in the footprint is occupied/blocked, CanEquip rejects.
        SetDirect(slot, item);
        EquipmentChanged?.Invoke();
        return true;
    }

    public bool CanEquip(BottomBarSlotType slot, ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        if (!IsEquipmentSlot(slot))
            return false;

        // Reject if the target slot already has an item or is blocked by another multi-slot wearable.
        if (TryGetOccupyingItem(slot, out _, out _))
            return false;

        // Hands remain the general "hold this thing" slot.
        // It does not use EquipSlot / wearable footprint rules.
        if (slot == BottomBarSlotType.Hands)
            return true;

        if (!item.Definition.IsEquippable)
            return false;

        if (item.Definition.EquipSlot != slot)
            return false;

        // Check the entire wearable footprint.
        IReadOnlyList<BottomBarSlotType> occupiedSlots = item.Definition.OccupiedEquipSlots;

        if (occupiedSlots != null && occupiedSlots.Count > 0)
        {
            for (int i = 0; i < occupiedSlots.Count; i++)
            {
                BottomBarSlotType occupiedSlot = occupiedSlots[i];

                if (occupiedSlot == BottomBarSlotType.None)
                    continue;

                if (!IsEquipmentSlot(occupiedSlot))
                    return false;

                // Anchor was already checked above.
                if (occupiedSlot == slot)
                    continue;

                if (TryGetOccupyingItem(occupiedSlot, out _, out _))
                    return false;
            }
        }

        return true;
    }

    public bool TryPlaceIntoPreferredSlotIfEmpty(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        BottomBarSlotType preferred = item.Definition.EquipSlot;
        if (preferred == BottomBarSlotType.None)
            return false;

        if (preferred == BottomBarSlotType.Hands)
            return false;

        if (!CanEquip(preferred, item))
            return false;

        SetDirect(preferred, item);
        EquipmentChanged?.Invoke();
        return true;
    }

    private ItemInstance GetDirect(BottomBarSlotType slot)
    {
        return slot switch
        {
            BottomBarSlotType.Hands => hands,
            BottomBarSlotType.Head => head,
            BottomBarSlotType.Feet => feet,
            BottomBarSlotType.Toolbelt => toolbelt,
            BottomBarSlotType.Backpack => backpack,
            BottomBarSlotType.Body => body,
            _ => null
        };
    }

    private void SetDirect(BottomBarSlotType slot, ItemInstance item)
    {
        switch (slot)
        {
            case BottomBarSlotType.Hands:
                hands = item;
                break;

            case BottomBarSlotType.Head:
                head = item;
                break;

            case BottomBarSlotType.Feet:
                feet = item;
                break;

            case BottomBarSlotType.Toolbelt:
                toolbelt = item;
                break;

            case BottomBarSlotType.Backpack:
                backpack = item;
                break;

            case BottomBarSlotType.Body:
                body = item;
                break;
        }
    }

    private static bool IsValidEquippedItem(ItemInstance item)
    {
        return item != null &&
               item.Definition != null &&
               item.Quantity > 0;
    }

    private static bool IsEquipmentSlot(BottomBarSlotType slot)
    {
        return slot == BottomBarSlotType.Hands ||
               slot == BottomBarSlotType.Head ||
               slot == BottomBarSlotType.Feet ||
               slot == BottomBarSlotType.Toolbelt ||
               slot == BottomBarSlotType.Backpack ||
               slot == BottomBarSlotType.Body;
    }

    private static System.Collections.Generic.IEnumerable<BottomBarSlotType> EnumerateEquipmentSlots(bool includeHands)
    {
        if (includeHands)
            yield return BottomBarSlotType.Hands;

        yield return BottomBarSlotType.Head;
        yield return BottomBarSlotType.Feet;
        yield return BottomBarSlotType.Toolbelt;
        yield return BottomBarSlotType.Backpack;
        yield return BottomBarSlotType.Body;
    }

    public EquipmentSnapshot CaptureSnapshot()
    {
        return new EquipmentSnapshot
        {
            version = 1,
            hands = hands != null ? hands.ToSnapshot() : null,
            head = head != null ? head.ToSnapshot() : null,
            feet = feet != null ? feet.ToSnapshot() : null,
            toolbelt = toolbelt != null ? toolbelt.ToSnapshot() : null,
            backpack = backpack != null ? backpack.ToSnapshot() : null,
            body = body != null ? body.ToSnapshot() : null
        };
    }

    public void RestoreSnapshot(EquipmentSnapshot snapshot, IItemDefinitionResolver resolver)
    {
        hands = ItemInstance.FromSnapshot(snapshot?.hands, resolver);
        head = ItemInstance.FromSnapshot(snapshot?.head, resolver);
        feet = ItemInstance.FromSnapshot(snapshot?.feet, resolver);
        toolbelt = ItemInstance.FromSnapshot(snapshot?.toolbelt, resolver);
        backpack = ItemInstance.FromSnapshot(snapshot?.backpack, resolver);
        body = ItemInstance.FromSnapshot(snapshot?.body, resolver);

        EquipmentChanged?.Invoke();
    }

    public void NotifyChanged()
    {
        EquipmentChanged?.Invoke();
    }
}