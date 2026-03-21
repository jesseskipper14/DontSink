public sealed class EquipmentSlotBinding : IInventorySlotBinding
{
    private readonly PlayerEquipment equipment;
    private readonly BottomBarSlotType slotType;

    public EquipmentSlotBinding(PlayerEquipment equipment, BottomBarSlotType slotType)
    {
        this.equipment = equipment;
        this.slotType = slotType;
    }

    public BottomBarSlotType SlotType => slotType;
    public bool SupportsSelection => true;

    public ItemInstance GetItem()
    {
        return equipment?.Get(slotType);
    }

    public ItemInstance RemoveItem()
    {
        return equipment?.Remove(slotType);
    }

    public bool TryPlaceItem(ItemInstance incoming, out ItemInstance displaced)
    {
        displaced = null;

        if (equipment == null || incoming == null)
            return false;

        ItemInstance current = equipment.Get(slotType);

        // NEW:
        // If target currently holds a container item and incoming is NOT a container,
        // dragging onto it means "try insert into that container".
        // If insert fails, do NOT fall back to swap/equip.
        if (current != null && current.IsContainer && !incoming.IsContainer)
        {
            if (current.TryInsertIntoContainer(incoming, out ItemInstance remainder))
            {
                if (remainder == null || remainder.IsDepleted())
                    return true;

                displaced = remainder;
                return true;
            }

            return false;
        }

        return equipment.TryPlace(slotType, incoming, out displaced);
    }
}