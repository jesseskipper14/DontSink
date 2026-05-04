using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ItemContainerState
{
    [SerializeField] private int slotCount;
    [SerializeField] private int columnCount = 4;
    [SerializeReference] private List<InventorySlot> slots = new();

    [NonSerialized] public Action Changed;

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

        if (slots == null)
            slots = new List<InventorySlot>();

        while (slots.Count < slotCount)
            slots.Add(new InventorySlot());

        if (slots.Count > slotCount)
            slots.RemoveRange(slotCount, slots.Count - slotCount);

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                slots[i] = new InventorySlot();
        }
    }

    public InventorySlot GetSlot(int index)
    {
        if (slots == null || index < 0 || index >= slots.Count)
            return null;

        if (slots[index] == null)
            slots[index] = new InventorySlot();

        return slots[index];
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    public ItemContainerSnapshot ToSnapshot()
    {
        ItemContainerSnapshot snapshot = new ItemContainerSnapshot
        {
            version = 1,
            slotCount = slotCount,
            columnCount = columnCount,
            slots = new List<ItemInstanceSnapshot>(slotCount)
        };

        for (int i = 0; i < slotCount; i++)
        {
            InventorySlot slot = (slots != null && i < slots.Count) ? slots[i] : null;

            if (slot == null || slot.IsEmpty || slot.Instance == null)
            {
                snapshot.slots.Add(null);
                continue;
            }

            snapshot.slots.Add(slot.Instance.ToSnapshot());
        }

        return snapshot;
    }

    public static ItemContainerState FromSnapshot(ItemContainerSnapshot snapshot, IItemDefinitionResolver resolver)
    {
        if (snapshot == null)
            return null;

        ItemContainerState state = new ItemContainerState(snapshot.slotCount, snapshot.columnCount);

        int count = Mathf.Min(state.slots.Count, snapshot.slots != null ? snapshot.slots.Count : 0);
        for (int i = 0; i < count; i++)
        {
            ItemInstance instance = ItemInstance.FromSnapshot(snapshot.slots[i], resolver);
            state.slots[i].Set(instance);
        }

        return state;
    }
}