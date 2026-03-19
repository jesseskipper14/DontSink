using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ItemInstanceSnapshot
{
    public int version = 1;
    public string instanceId;
    public string itemId;
    public int quantity;
    public ItemContainerSnapshot container;
}

[Serializable]
public sealed class ItemContainerSnapshot
{
    public int version = 1;
    public int slotCount;
    public int columnCount;
    public List<ItemInstanceSnapshot> slots = new();
}

[Serializable]
public sealed class InventorySnapshot
{
    public int version = 1;
    public int hotbarSlotCount;
    public List<ItemInstanceSnapshot> hotbarSlots = new();
    public BottomBarSlotType selectedSlot = BottomBarSlotType.Hotbar0;
}

[Serializable]
public sealed class EquipmentSnapshot
{
    public int version = 1;
    public ItemInstanceSnapshot hands;
    public ItemInstanceSnapshot head;
    public ItemInstanceSnapshot feet;
    public ItemInstanceSnapshot toolbelt;
    public ItemInstanceSnapshot backpack;
    public ItemInstanceSnapshot body;
}

[Serializable]
public sealed class PlayerLoadoutSnapshot
{
    public int version = 1;
    public InventorySnapshot inventory;
    public EquipmentSnapshot equipment;
}