using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerEquipment : MonoBehaviour
{
    [Header("Equipped Gear")]
    [SerializeField] private ItemDefinition equippedBackpack;
    [SerializeField] private ItemDefinition equippedToolbelt;

    public event Action EquipmentChanged;

    public ItemDefinition EquippedBackpack => equippedBackpack;
    public ItemDefinition EquippedToolbelt => equippedToolbelt;

    public int BonusInventorySlots
    {
        get
        {
            int total = 0;

            if (equippedBackpack != null)
                total += equippedBackpack.BonusInventorySlots;

            if (equippedToolbelt != null)
                total += equippedToolbelt.BonusInventorySlots;

            return total;
        }
    }

    public bool TrySetBackpack(ItemDefinition item)
    {
        if (item != null && item.EquipSlot != ItemDefinition.EquipSlotType.Backpack)
            return false;

        if (equippedBackpack == item)
            return true;

        equippedBackpack = item;
        EquipmentChanged?.Invoke();
        return true;
    }

    public bool TrySetToolbelt(ItemDefinition item)
    {
        if (item != null && item.EquipSlot != ItemDefinition.EquipSlotType.Toolbelt)
            return false;

        if (equippedToolbelt == item)
            return true;

        equippedToolbelt = item;
        EquipmentChanged?.Invoke();
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (equippedBackpack != null &&
            equippedBackpack.EquipSlot != ItemDefinition.EquipSlotType.Backpack)
        {
            equippedBackpack = null;
        }

        if (equippedToolbelt != null &&
            equippedToolbelt.EquipSlot != ItemDefinition.EquipSlotType.Toolbelt)
        {
            equippedToolbelt = null;
        }
    }
#endif
}