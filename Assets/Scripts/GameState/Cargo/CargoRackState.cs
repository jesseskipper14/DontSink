using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CargoRackSlot
{
    [SerializeField] private CargoCrateStoredSnapshot crate;

    public CargoCrateStoredSnapshot Crate => crate;
    public bool IsEmpty => crate == null;

    public void Set(CargoCrateStoredSnapshot snapshot)
    {
        crate = snapshot;
    }

    public CargoCrateStoredSnapshot Clear()
    {
        CargoCrateStoredSnapshot old = crate;
        crate = null;
        return old;
    }
}

[Serializable]
public sealed class CargoRackSlotSnapshot
{
    public bool hasCrate;
    public CargoCrateStoredSnapshot crate;
}

[Serializable]
public sealed class CargoRackStateSnapshot
{
    public int version = 1;
    public int slotCount;
    public int columnCount;
    public List<CargoRackSlotSnapshot> slots = new();
}

[Serializable]
public sealed class CargoRackState
{
    [SerializeField] private int slotCount;
    [SerializeField] private int columnCount = 2;
    [SerializeField] private List<CargoRackSlot> slots = new();

    [NonSerialized] public Action Changed;

    public int SlotCount => Mathf.Max(0, slotCount);
    public int ColumnCount => Mathf.Max(1, columnCount);
    public IReadOnlyList<CargoRackSlot> Slots => slots;

    public CargoRackState(int slotCount, int columnCount = 2)
    {
        EnsureLayout(slotCount, columnCount);
    }

    public void EnsureLayout(int desiredSlotCount, int desiredColumnCount = 2)
    {
        slotCount = Mathf.Max(0, desiredSlotCount);
        columnCount = Mathf.Max(1, desiredColumnCount);

        if (slots == null)
            slots = new List<CargoRackSlot>();

        while (slots.Count < slotCount)
            slots.Add(new CargoRackSlot());

        if (slots.Count > slotCount)
            slots.RemoveRange(slotCount, slots.Count - slotCount);

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                slots[i] = new CargoRackSlot();
        }
    }

    public CargoRackSlot GetSlot(int index)
    {
        if (slots == null || index < 0 || index >= slots.Count)
            return null;

        if (slots[index] == null)
            slots[index] = new CargoRackSlot();

        return slots[index];
    }

    public bool IsSlotEmpty(int index)
    {
        CargoRackSlot slot = GetSlot(index);
        return slot == null || slot.IsEmpty;
    }

    public bool TryFindFirstEmptySlot(out int index)
    {
        index = -1;

        for (int i = 0; i < SlotCount; i++)
        {
            CargoRackSlot slot = GetSlot(i);
            if (slot != null && slot.IsEmpty)
            {
                index = i;
                return true;
            }
        }

        return false;
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    public CargoRackStateSnapshot ToSnapshot()
    {
        CargoRackStateSnapshot snapshot = new CargoRackStateSnapshot
        {
            version = 1,
            slotCount = slotCount,
            columnCount = columnCount,
            slots = new List<CargoRackSlotSnapshot>(slotCount)
        };

        for (int i = 0; i < slotCount; i++)
        {
            CargoRackSlot slot = GetSlot(i);

            snapshot.slots.Add(new CargoRackSlotSnapshot
            {
                hasCrate = slot != null && !slot.IsEmpty,
                crate = slot != null ? slot.Crate : null
            });
        }

        return snapshot;
    }

    public static CargoRackState FromSnapshot(CargoRackStateSnapshot snapshot)
    {
        if (snapshot == null)
            return null;

        CargoRackState state = new CargoRackState(snapshot.slotCount, snapshot.columnCount);

        int count = Mathf.Min(
            state.slots.Count,
            snapshot.slots != null ? snapshot.slots.Count : 0);

        for (int i = 0; i < count; i++)
        {
            CargoRackSlotSnapshot slotSnapshot = snapshot.slots[i];
            if (slotSnapshot == null || !slotSnapshot.hasCrate)
                continue;

            state.slots[i].Set(slotSnapshot.crate);
        }

        return state;
    }
}