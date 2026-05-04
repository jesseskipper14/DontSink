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
    public int currentCharges;

    [SerializeReference]
    public ItemContainerSnapshot container;
}

[Serializable]
public sealed class ItemContainerSnapshot
{
    public int version = 1;
    public int slotCount;
    public int columnCount;

    [SerializeReference]
    public List<ItemInstanceSnapshot> slots = new();
}

[Serializable]
public sealed class InventorySnapshot
{
    public int version = 1;
    public int hotbarSlotCount;

    [SerializeReference]
    public List<ItemInstanceSnapshot> hotbarSlots = new();

    public BottomBarSlotType selectedSlot = BottomBarSlotType.Hotbar0;
}

[Serializable]
public sealed class EquipmentSnapshot
{
    public int version = 1;

    [SerializeReference] public ItemInstanceSnapshot hands;
    [SerializeReference] public ItemInstanceSnapshot head;
    [SerializeReference] public ItemInstanceSnapshot feet;
    [SerializeReference] public ItemInstanceSnapshot toolbelt;
    [SerializeReference] public ItemInstanceSnapshot backpack;
    [SerializeReference] public ItemInstanceSnapshot body;
}

[Serializable]
public sealed class PlayerLoadoutSnapshot
{
    public int version = 1;

    [SerializeReference]
    public InventorySnapshot inventory;

    [SerializeReference]
    public EquipmentSnapshot equipment;
}