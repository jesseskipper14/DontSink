using System;
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
        ItemInstance raw = slot switch
        {
            BottomBarSlotType.Hands => hands,
            BottomBarSlotType.Head => head,
            BottomBarSlotType.Feet => feet,
            BottomBarSlotType.Toolbelt => toolbelt,
            BottomBarSlotType.Backpack => backpack,
            BottomBarSlotType.Body => body,
            _ => null
        };

        return IsValidEquippedItem(raw) ? raw : null;
    }

    public ItemInstance Remove(BottomBarSlotType slot)
    {
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

        displaced = Get(slot);
        SetDirect(slot, item);
        EquipmentChanged?.Invoke();
        return true;
    }

    public bool CanEquip(BottomBarSlotType slot, ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        if (slot == BottomBarSlotType.Hands)
            return true;

        return item.Definition.EquipSlot == slot;
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

        if (Get(preferred) != null)
            return false;

        if (!CanEquip(preferred, item))
            return false;

        SetDirect(preferred, item);
        EquipmentChanged?.Invoke();
        return true;
    }

    private void SetDirect(BottomBarSlotType slot, ItemInstance item)
    {
        switch (slot)
        {
            case BottomBarSlotType.Hands: hands = item; break;
            case BottomBarSlotType.Head: head = item; break;
            case BottomBarSlotType.Feet: feet = item; break;
            case BottomBarSlotType.Toolbelt: toolbelt = item; break;
            case BottomBarSlotType.Backpack: backpack = item; break;
            case BottomBarSlotType.Body: body = item; break;
        }
    }

    private static bool IsValidEquippedItem(ItemInstance item)
    {
        return item != null &&
               item.Definition != null &&
               item.Quantity > 0;
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
}