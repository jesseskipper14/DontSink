public sealed class BottomBarSlot
{
    public BottomBarSlotType SlotType;
    public InventorySlot InventorySlot; // for hotbar
    public PlayerEquipment Equipment;

    public ItemInstance GetItem()
    {
        if (InventorySlot != null)
            return InventorySlot.Instance;

        return Equipment?.Get(SlotType);
    }
}