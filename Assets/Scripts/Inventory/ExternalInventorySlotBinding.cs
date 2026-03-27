public sealed class ExternalInventorySlotBinding : IInventorySlotBinding
{
    private readonly ItemInstance containerItem;
    private readonly int slotIndex;

    public ExternalInventorySlotBinding(ItemInstance containerItem, int slotIndex)
    {
        this.containerItem = containerItem;
        this.slotIndex = slotIndex;
    }

    public BottomBarSlotType SlotType => BottomBarSlotType.None;
    public bool SupportsSelection => false;

    public ItemInstance GetItem()
    {
        return containerItem?.ContainerState?.GetSlot(slotIndex)?.Instance;
    }

    public ItemInstance RemoveItem()
    {
        ItemContainerState state = containerItem?.ContainerState;
        if (state == null)
            return null;

        InventorySlot slot = state.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
            return null;

        ItemInstance inst = slot.Instance;
        slot.Clear();
        state.NotifyChanged();
        return inst;
    }

    public bool TryPlaceItem(ItemInstance incoming, out ItemInstance displaced)
    {
        displaced = incoming;

        if (incoming == null)
            return false;

        if (ContainerPlacementUtility.TryPlaceIntoSlot(
            containerItem,
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
        return ContainerPlacementUtility.CanPlaceIntoSlot(containerItem, slotIndex, incoming);
    }
}