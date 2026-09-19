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
        if (equipment == null)
            return null;

        ItemInstance current =
            equipment.Get(
                slotType);

        if (slotType ==
                BottomBarSlotType.Hands &&
            current != null)
        {
            HandheldSoundingLineController sounder =
                FindSoundingLineController();

            if (sounder != null &&
                sounder.IsActiveDeployedSounder(
                    current))
            {
                // Dragging a physically deployed sounder out of Hands is not a
                // magical inventory teleport. Release the already-visible
                // physical proxy into the world and report no draggable item.
                sounder.TryReleaseDeployedSounderToWorld(
                    current,
                    out _);

                return null;
            }
        }

        return equipment.Remove(
            slotType);
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
                equipment.NotifyChanged();

                if (remainder == null || remainder.IsDepleted())
                    return true;

                displaced = remainder;
                return true;
            }

            return false;
        }

        if (slotType ==
                BottomBarSlotType.Hands &&
            current != null)
        {
            HandheldSoundingLineController sounder =
                FindSoundingLineController();

            if (sounder != null &&
                sounder.IsActiveDeployedSounder(
                    current))
            {
                if (!sounder.TryReleaseDeployedSounderToWorld(
                        current,
                        out _))
                {
                    return false;
                }
            }
        }

        return equipment.TryPlace(
            slotType,
            incoming,
            out displaced);
    }

    private HandheldSoundingLineController FindSoundingLineController()
    {
        if (equipment == null)
            return null;

        HandheldSoundingLineController controller =
            equipment.GetComponent<HandheldSoundingLineController>();

        if (controller != null)
            return controller;

        controller =
            equipment.GetComponentInParent<HandheldSoundingLineController>();

        if (controller != null)
            return controller;

        return
            equipment.GetComponentInChildren<HandheldSoundingLineController>(
                true);
    }

    public bool CanAccept(ItemInstance incoming)
    {
        if (incoming == null)
            return false;

        if (equipment == null)
            return false;

        return equipment.CanEquip(slotType, incoming);
    }
}