using UnityEngine;

public sealed class HotbarSlotBinding : IInventorySlotBinding
{
    private readonly PlayerInventory inventory;
    private readonly int hotbarIndex;

    public HotbarSlotBinding(PlayerInventory inventory, int hotbarIndex)
    {
        this.inventory = inventory;
        this.hotbarIndex = hotbarIndex;
    }

    public BottomBarSlotType SlotType => PlayerInventory.HotbarIndexToSlotType(hotbarIndex);
    public bool SupportsSelection => true;

    public ItemInstance GetItem()
    {
        return inventory?.GetSlot(hotbarIndex)?.Instance;
    }

    public ItemInstance RemoveItem()
    {
        InventorySlot slot = inventory?.GetSlot(hotbarIndex);
        if (slot == null || slot.IsEmpty)
            return null;

        ItemInstance item = slot.Instance;
        slot.Clear();
        inventory?.NotifyChanged();
        return item;
    }

    public bool TryPlaceItem(ItemInstance incoming, out ItemInstance displaced)
    {
        displaced = null;

        if (incoming == null)
            return false;

        InventorySlot slot = inventory?.GetSlot(hotbarIndex);
        if (slot == null)
            return false;

        if (slot.IsEmpty)
        {
            slot.Set(incoming);
            inventory?.NotifyChanged();
            return true;
        }

        // NEW:
        // If target slot contains a container item and incoming is NOT a container,
        // dragging onto it means "try insert into that container".
        // If insert fails, do NOT fall back to swap.
        if (slot.Instance != null && slot.Instance.IsContainer && !incoming.IsContainer)
        {
            if (slot.Instance.TryInsertIntoContainer(incoming, out ItemInstance remainder))
            {
                inventory?.NotifyChanged();

                if (remainder == null || remainder.IsDepleted())
                    return true;

                displaced = remainder;
                return true;
            }

            return false;
        }

        if (slot.Instance != null && slot.Instance.CanStackWith(incoming))
        {
            int moved = slot.Instance.AddQuantity(incoming.Quantity);
            incoming.RemoveQuantity(moved);
            inventory?.NotifyChanged();

            if (incoming.IsDepleted())
                return true;

            displaced = incoming;
            return true;
        }

        displaced = slot.Instance;
        slot.Set(incoming);
        inventory?.NotifyChanged();
        return true;
    }
}