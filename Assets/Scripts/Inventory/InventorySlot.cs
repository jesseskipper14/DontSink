using System;
using UnityEngine;

[Serializable]
public sealed class InventorySlot
{
    [SerializeField] private ItemDefinition item;
    [Min(0)]
    [SerializeField] private int quantity;

    public ItemDefinition Item => item;
    public int Quantity => quantity;

    public bool IsEmpty => item == null || quantity <= 0;

    public void Clear()
    {
        item = null;
        quantity = 0;
    }

    public void Set(ItemDefinition definition, int amount)
    {
        if (definition == null || amount <= 0)
        {
            Clear();
            return;
        }

        item = definition;
        quantity = amount;
    }

    public bool CanAccept(ItemDefinition definition)
    {
        if (definition == null) return false;
        if (IsEmpty) return true;
        if (item != definition) return false;
        return quantity < item.MaxStack;
    }

    public int RemainingCapacityFor(ItemDefinition definition)
    {
        if (definition == null) return 0;
        if (IsEmpty) return definition.MaxStack;
        if (item != definition) return 0;
        return Mathf.Max(0, item.MaxStack - quantity);
    }

    public int Add(ItemDefinition definition, int amount)
    {
        if (definition == null || amount <= 0) return 0;

        if (IsEmpty)
        {
            int accepted = Mathf.Min(amount, definition.MaxStack);
            item = definition;
            quantity = accepted;
            return accepted;
        }

        if (item != definition) return 0;

        int room = Mathf.Max(0, item.MaxStack - quantity);
        int added = Mathf.Min(room, amount);
        quantity += added;
        return added;
    }

    public int Remove(int amount)
    {
        if (IsEmpty || amount <= 0) return 0;

        int removed = Mathf.Min(amount, quantity);
        quantity -= removed;

        if (quantity <= 0)
        {
            Clear();
        }

        return removed;
    }
}