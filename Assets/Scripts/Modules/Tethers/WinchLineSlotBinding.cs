public sealed class WinchLineSlotBinding : IInventorySlotBinding
{
    private readonly WinchModule winch;
    private readonly int slotIndex;

    public WinchLineSlotBinding(WinchModule winch, int slotIndex)
    {
        this.winch = winch;
        this.slotIndex = slotIndex;
    }

    public WinchModule Winch => winch;
    public int SlotIndex => slotIndex;
    public BottomBarSlotType SlotType => BottomBarSlotType.None;
    public bool SupportsSelection => false;

    public ItemInstance GetItem()
    {
        return winch?.LineContainer?.GetSlot(slotIndex)?.Instance;
    }

    public ItemInstance RemoveItem()
    {
        if (winch == null)
            return null;

        InventorySlot slot = winch.LineContainer?.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty || slot.Instance == null)
            return null;

        ItemInstance removed = slot.Instance;
        slot.Clear();
        winch.NotifyLineContainerChanged();
        return removed;
    }

    public bool TryPlaceItem(ItemInstance incoming, out ItemInstance displaced)
    {
        displaced = incoming;

        if (winch == null ||
            incoming == null ||
            incoming.Quantity != 1 ||
            !winch.CanAcceptLine(incoming))
        {
            return false;
        }

        InventorySlot slot = winch.LineContainer?.GetSlot(slotIndex);
        if (slot == null)
            return false;

        ItemInstance old = slot.IsEmpty ? null : slot.Instance;
        slot.Set(incoming);
        winch.NotifyLineContainerChanged();
        displaced = old;
        return true;
    }

    public bool CanAccept(ItemInstance incoming)
    {
        return
            winch != null &&
            incoming != null &&
            incoming.Quantity == 1 &&
            winch.CanAcceptLine(incoming);
    }
}
