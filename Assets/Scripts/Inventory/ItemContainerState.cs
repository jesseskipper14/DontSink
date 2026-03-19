using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ItemContainerState
{
    [SerializeField] private int slotCount;
    [SerializeField] private int columnCount = 4;
    [SerializeField] private List<InventorySlot> slots = new();

    public int SlotCount => Mathf.Max(0, slotCount);
    public int ColumnCount => Mathf.Max(1, columnCount);
    public IReadOnlyList<InventorySlot> Slots => slots;

    public ItemContainerState(int slotCount, int columnCount = 4)
    {
        EnsureLayout(slotCount, columnCount);
    }

    public void EnsureLayout(int desiredSlotCount, int desiredColumnCount = 4)
    {
        slotCount = Mathf.Max(0, desiredSlotCount);
        columnCount = Mathf.Max(1, desiredColumnCount);

        while (slots.Count < slotCount)
            slots.Add(new InventorySlot());

        if (slots.Count > slotCount)
            slots.RemoveRange(slotCount, slots.Count - slotCount);
    }

    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
            return null;

        return slots[index];
    }

    public ItemContainerSnapshot ToSnapshot()
    {
        ItemContainerSnapshot snapshot = new ItemContainerSnapshot
        {
            version = 1,
            slotCount = slotCount,
            columnCount = columnCount
        };

        snapshot.slots = new List<ItemInstanceSnapshot>(slots.Count);

        for (int i = 0; i < slots.Count; i++)
        {
            ItemInstanceSnapshot itemSnapshot = slots[i] != null && !slots[i].IsEmpty
                ? slots[i].Instance.ToSnapshot()
                : null;

            snapshot.slots.Add(itemSnapshot);
        }

        return snapshot;
    }

    public static ItemContainerState FromSnapshot(ItemContainerSnapshot snapshot, IItemDefinitionResolver resolver)
    {
        if (snapshot == null)
            return null;

        ItemContainerState state = new ItemContainerState(snapshot.slotCount, snapshot.columnCount);

        int count = Mathf.Min(state.slots.Count, snapshot.slots.Count);
        for (int i = 0; i < count; i++)
        {
            ItemInstance instance = ItemInstance.FromSnapshot(snapshot.slots[i], resolver);
            state.slots[i].Set(instance);
        }

        return state;
    }
}