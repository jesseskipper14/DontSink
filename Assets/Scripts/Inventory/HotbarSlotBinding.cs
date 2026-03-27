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

        if (incoming == null || inventory == null)
            return false;

        if (incoming.Definition != null && !incoming.Definition.IsAllowedInParentSlot(SlotType))
            return false;

        InventorySlot slot = inventory.GetSlot(hotbarIndex);
        if (slot == null)
            return false;

        if (slot.Instance != null && slot.Instance.IsContainer && !incoming.IsContainer)
        {
            if (ContainerPlacementUtility.TryAutoInsert(slot.Instance, incoming, out ItemInstance nestedRemainder))
            {
                inventory.NotifyChanged();
                displaced = nestedRemainder;
                return true;
            }

            return false;
        }

        if (slot.IsEmpty)
        {
            slot.Set(incoming);
            inventory.NotifyChanged();
            return true;
        }

        if (slot.Instance != null && slot.Instance.CanStackWith(incoming) && slot.Instance.RemainingStackSpace > 0)
        {
            int moved = slot.Instance.AddQuantity(incoming.Quantity);
            if (moved <= 0)
                return false;

            incoming.RemoveQuantity(moved);
            inventory.NotifyChanged();

            displaced = incoming.IsDepleted() ? null : incoming;
            return true;
        }

        ItemInstance existing = slot.Instance;

        if (existing != null && existing.Definition != null &&
            !existing.Definition.IsAllowedInParentSlot(SlotType))
            return false;

        slot.Set(incoming);
        inventory.NotifyChanged();
        displaced = existing;
        return true;
    }

    public bool CanAccept(ItemInstance incoming)
    {
        if (incoming == null || inventory == null)
            return false;

        if (incoming.Definition != null &&
            !incoming.Definition.IsAllowedInParentSlot(SlotType))
            return false;

        InventorySlot slot = inventory.GetSlot(hotbarIndex);
        if (slot == null)
            return false;

        ItemInstance existing = slot.Instance;

        if (existing != null && existing.IsContainer && !incoming.IsContainer)
            return ContainerPlacementUtility.CanAutoInsert(existing, incoming);

        if (existing == null)
            return true;

        if (existing.CanStackWith(incoming) && existing.RemainingStackSpace > 0)
            return true;

        return true;
    }
}