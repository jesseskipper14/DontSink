public sealed class StorageModuleSlotBinding : IInventorySlotBinding
{
    private readonly StorageModule storageModule;
    private readonly int slotIndex;

    public StorageModuleSlotBinding(StorageModule storageModule, int slotIndex)
    {
        this.storageModule = storageModule;
        this.slotIndex = slotIndex;
    }

    public StorageModule StorageModule => storageModule;
    public int SlotIndex => slotIndex;

    public BottomBarSlotType SlotType => BottomBarSlotType.None;
    public bool SupportsSelection => false;

    public ItemInstance GetItem()
    {
        storageModule?.EnsureContainer();
        return storageModule?.ContainerState?.GetSlot(slotIndex)?.Instance;
    }

    public ItemInstance RemoveItem()
    {
        if (storageModule == null)
            return null;

        storageModule.EnsureContainer();

        ItemContainerState state = storageModule.ContainerState;
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

        if (incoming == null || storageModule == null)
            return false;

        storageModule.EnsureContainer();

        if (ContainerPlacementUtility.TryPlaceIntoSlot(
                storageModule,
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
        if (storageModule == null || incoming == null)
            return false;

        storageModule.EnsureContainer();
        return ContainerPlacementUtility.CanPlaceIntoSlot(storageModule, slotIndex, incoming);
    }

    public bool TryGetCargoSnapshot(out CargoCrateStoredSnapshot snapshot)
    {
        snapshot = null;

        if (storageModule == null)
            return false;

        storageModule.EnsureContainer();

        CargoRackState cargoState = storageModule.CargoRackState;
        if (cargoState == null)
            return false;

        if (slotIndex < 0 || slotIndex >= cargoState.SlotCount)
            return false;

        CargoRackSlot slot = cargoState.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty || slot.Crate == null)
            return false;

        snapshot = slot.Crate;
        return true;
    }
}