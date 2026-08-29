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

    [NonSerialized] private HashSet<ItemInstance> subscribedItems;

    public int SlotCount => Mathf.Max(0, slotCount);
    public int ColumnCount => Mathf.Max(1, columnCount);
    public IReadOnlyList<InventorySlot> Slots => slots;

    /// <summary>
    /// Recursive mass of every ItemInstance currently stored in this container.
    /// Each child ItemInstance owns the mass of its own nested contents.
    /// </summary>
    public float ContentsMass
    {
        get
        {
            if (slots == null)
                return 0f;

            float total = 0f;

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                ItemInstance instance = slot != null ? slot.Instance : null;

                if (instance == null ||
                    instance.Definition == null ||
                    instance.Quantity <= 0)
                {
                    continue;
                }

                total += instance.TotalMass;
            }

            return Mathf.Max(0f, total);
        }
    }

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

        RefreshContainedItemSubscriptions();
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
        RefreshContainedItemSubscriptions();
        Changed?.Invoke();
    }

    private void RefreshContainedItemSubscriptions()
    {
        if (subscribedItems == null)
            subscribedItems = new HashSet<ItemInstance>();

        HashSet<ItemInstance> currentItems = new HashSet<ItemInstance>();

        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                ItemInstance instance = slot != null ? slot.Instance : null;

                if (instance != null)
                    currentItems.Add(instance);
            }
        }

        foreach (ItemInstance oldItem in subscribedItems)
        {
            if (oldItem != null && !currentItems.Contains(oldItem))
                oldItem.Changed -= HandleContainedItemChanged;
        }

        foreach (ItemInstance currentItem in currentItems)
        {
            if (currentItem != null && !subscribedItems.Contains(currentItem))
                currentItem.Changed += HandleContainedItemChanged;
        }

        subscribedItems = currentItems;
    }

    private void HandleContainedItemChanged()
    {
        // Bubble nested quantity/container changes to the parent container.
        // This is what lets:
        //
        //   pearl -> chest -> locker -> installed module -> Boat
        //
        // update without polling every nesting level every frame.
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

        state.RefreshContainedItemSubscriptions();
        return state;
    }
}