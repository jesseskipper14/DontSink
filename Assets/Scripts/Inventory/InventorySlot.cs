using System;
using UnityEngine;

public enum BottomBarSlotType
{
    None = 0,

    Hands,
    Head,
    Feet,

    Hotbar0,
    Hotbar1,
    Hotbar2,
    Hotbar3,
    Hotbar4,
    Hotbar5,
    Hotbar6,
    Hotbar7,

    Toolbelt,
    Backpack,
    Body
}

[Serializable]
public sealed class InventorySlot
{
    [SerializeField] private ItemInstance itemInstance;

    public ItemInstance Instance => itemInstance;
    public ItemDefinition Item => itemInstance != null ? itemInstance.Definition : null;
    public int Quantity => itemInstance != null ? itemInstance.Quantity : 0;

    public bool IsEmpty => itemInstance == null || itemInstance.Definition == null || itemInstance.Quantity <= 0;

    public void Clear()
    {
        itemInstance = null;
    }

    public void Set(ItemInstance instance)
    {
        itemInstance = instance;
    }
}