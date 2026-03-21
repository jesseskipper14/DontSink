public sealed class ContainerSlotBinding : IInventorySlotBinding
{
    private readonly ItemInstance ownerContainer;
    private readonly ItemContainerState containerState;
    private readonly int slotIndex;
    private readonly BottomBarSlotType slotType;

    public ContainerSlotBinding(ItemInstance ownerContainer, int slotIndex, BottomBarSlotType slotType = BottomBarSlotType.None)
    {
        this.ownerContainer = ownerContainer;
        this.containerState = ownerContainer != null ? ownerContainer.ContainerState : null;
        this.slotIndex = slotIndex;
        this.slotType = slotType;
    }

    public BottomBarSlotType SlotType => slotType;
    public bool SupportsSelection => false;

    public ItemInstance GetItem()
    {
        return containerState?.GetSlot(slotIndex)?.Instance;
    }

    public ItemInstance RemoveItem()
    {
        InventorySlot slot = containerState?.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
            return null;

        ItemInstance item = slot.Instance;
        slot.Clear();
        containerState?.NotifyChanged();
        return item;
    }

    public bool TryPlaceItem(ItemInstance incoming, out ItemInstance displaced)
    {
        displaced = null;

        if (incoming == null || containerState == null || ownerContainer == null)
            return false;

        InventorySlot slot = containerState.GetSlot(slotIndex);
        if (slot == null)
            return false;

        // NEW:
        // If this slot currently holds a container item and incoming is NOT a container,
        // dragging onto it means "try insert into that container".
        // If insert fails, do NOT fall back to swap.
        if (slot.Instance != null && slot.Instance.IsContainer && !incoming.IsContainer)
        {
            if (slot.Instance.TryInsertIntoContainer(incoming, out ItemInstance remainder))
            {
                containerState.NotifyChanged();

                if (remainder == null || remainder.IsDepleted())
                    return true;

                displaced = remainder;
                return true;
            }

            return false;
        }

        // TEMP RULE: no container-in-container placement.
        if (incoming.IsContainer)
            return false;

        if (!ownerContainer.CanAcceptIntoContainer(incoming))
            return false;

        if (slot.IsEmpty)
        {
            slot.Set(incoming);
            containerState.NotifyChanged();
            return true;
        }

        if (slot.Instance != null && slot.Instance.CanStackWith(incoming))
        {
            int moved = slot.Instance.AddQuantity(incoming.Quantity);
            incoming.RemoveQuantity(moved);
            containerState.NotifyChanged();

            if (incoming.IsDepleted())
                return true;

            displaced = incoming;
            return true;
        }

        // Optional safety: only allow swap if the existing item is also valid for this container.
        if (slot.Instance != null && !ownerContainer.CanAcceptIntoContainer(slot.Instance))
            return false;

        displaced = slot.Instance;
        slot.Set(incoming);
        containerState.NotifyChanged();
        return true;
    }
}