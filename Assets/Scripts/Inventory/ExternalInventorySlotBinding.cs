public sealed class ExternalInventorySlotBinding : IInventorySlotBinding
{
    private readonly ItemContainerState state;
    private readonly int slotIndex;

    public ExternalInventorySlotBinding(ItemContainerState state, int slotIndex)
    {
        this.state = state;
        this.slotIndex = slotIndex;
    }

    public BottomBarSlotType SlotType => BottomBarSlotType.None;
    public bool SupportsSelection => false;

    public ItemInstance GetItem()
    {
        return state?.GetSlot(slotIndex)?.Instance;
    }

    public ItemInstance RemoveItem()
    {
        if (state == null)
            return null;

        var slot = state.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
            return null;

        ItemInstance inst = slot.Instance;
        slot.Clear();
        state.NotifyChanged();
        return inst;
    }

    public bool TryPlaceItem(ItemInstance incoming, out ItemInstance displaced)
    {
        displaced = null;

        if (state == null || incoming == null)
            return false;

        var slot = state.GetSlot(slotIndex);
        if (slot == null)
            return false;

        // container insert case (same behavior as equipment binding)
        if (!slot.IsEmpty && slot.Instance.IsContainer && !incoming.IsContainer)
        {
            if (slot.Instance.TryInsertIntoContainer(incoming, out ItemInstance remainder))
            {
                displaced = remainder;
                return true;
            }

            return false;
        }

        if (slot.IsEmpty)
        {
            slot.Set(incoming);
            state.NotifyChanged();
            return true;
        }

        // stacking
        if (slot.Instance.CanStackWith(incoming))
        {
            int moved = slot.Instance.AddQuantity(incoming.Quantity);
            incoming.RemoveQuantity(moved);

            if (incoming.IsDepleted())
            {
                state.NotifyChanged();
                return true;
            }

            displaced = incoming;
            state.NotifyChanged();
            return true;
        }

        // swap
        displaced = slot.Instance;
        slot.Set(incoming);
        state.NotifyChanged();
        return true;
    }

    public bool CanAccept(ItemInstance incoming)
    {
        if (state == null || incoming == null)
            return false;

        // NOTE: we don't have definition-level rules here yet
        // phase 1: allow anything except container-in-container
        if (incoming.IsContainer)
            return false;

        return true;
    }
}