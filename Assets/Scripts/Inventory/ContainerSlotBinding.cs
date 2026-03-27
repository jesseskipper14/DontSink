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

        if (incoming == null || ownerContainer == null || containerState == null)
            return false;

        InventorySlot slot = containerState.GetSlot(slotIndex);
        if (slot == null)
            return false;

        // Dragging onto a slotted container means insert into that contained container.
        if (slot.Instance != null && slot.Instance.IsContainer && !incoming.IsContainer)
        {
            if (ContainerPlacementUtility.TryAutoInsert(slot.Instance, incoming, out ItemInstance nestedRemainder))
            {
                containerState.NotifyChanged();
                displaced = nestedRemainder;
                return true;
            }

            return false;
        }

        if (ContainerPlacementUtility.TryPlaceIntoSlot(
            ownerContainer,
            slotIndex,
            incoming,
            out ItemInstance remainder,
            out ItemInstance slotDisplaced))
        {
            if (slotDisplaced != null)
            {
                displaced = slotDisplaced;
                return true;
            }

            displaced = remainder;
            return true;
        }

        return false;
    }

    public bool CanAccept(ItemInstance incoming)
    {
        if (incoming == null || ownerContainer == null || containerState == null)
            return false;

        InventorySlot slot = containerState.GetSlot(slotIndex);
        if (slot == null)
            return false;

        if (slot.Instance != null && slot.Instance.IsContainer && !incoming.IsContainer)
            return ContainerPlacementUtility.CanAutoInsert(slot.Instance, incoming);

        return ContainerPlacementUtility.CanPlaceIntoSlot(ownerContainer, slotIndex, incoming);
    }
}