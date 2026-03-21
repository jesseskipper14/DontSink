public interface IInventorySlotBinding
{
    ItemInstance GetItem();
    ItemInstance RemoveItem();
    bool TryPlaceItem(ItemInstance incoming, out ItemInstance displaced);

    BottomBarSlotType SlotType { get; }
    bool SupportsSelection { get; }
}