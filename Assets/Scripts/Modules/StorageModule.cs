using UnityEngine;

[DisallowMultipleComponent]
public sealed class StorageModule : MonoBehaviour, IInstalledModuleLifecycle
{
    [Header("Runtime")]
    [SerializeField] private InstalledModule installedModule;
    [SerializeField] private ItemContainerState containerState;
    [SerializeField] private CargoRackState cargoRackState;

    public InstalledModule InstalledModule => installedModule;
    public ItemContainerState ContainerState => containerState;

    public bool HasContainer => containerState != null;
    public CargoRackState CargoRackState => cargoRackState;
    public bool HasCargoRackState => cargoRackState != null;

    public StorageModuleMode Mode
    {
        get
        {
            ModuleDefinition def = GetModuleDefinition();

            return def != null && def.Storage != null
                ? def.Storage.Mode
                : StorageModuleMode.None;
        }
    }

    public bool IsFixedStorage => Mode == StorageModuleMode.FixedStorage;
    public bool IsContainerRack => Mode == StorageModuleMode.ContainerRack;

    private void Awake()
    {
        CacheRefs();
        EnsureContainer();
    }

    public void OnInstalled(Hardpoint hardpoint)
    {
        CacheRefs();
        EnsureContainer();
    }

    public void OnRemoved()
    {
        // Later:
        // - block removal if container has contents
        // - or intentionally eject/drop contents
        // - or require the player to empty storage first
        //
        // For now, this component only owns the storage state.
    }

    public void EnsureContainer()
    {
        ModuleDefinition def = GetModuleDefinition();

        if (def == null || !def.HasStorage || def.Storage == null)
            return;

        int slotCount = Mathf.Max(1, def.Storage.SlotCount);
        int columnCount = Mathf.Max(1, def.Storage.ColumnCount);

        if (containerState == null)
        {
            containerState = new ItemContainerState(slotCount, columnCount);
        }
        else
        {
            containerState.EnsureLayout(slotCount, columnCount);
        }

        if (def.Storage.IsContainerRack)
        {
            if (cargoRackState == null)
                cargoRackState = new CargoRackState(slotCount, columnCount);
            else
                cargoRackState.EnsureLayout(slotCount, columnCount);
        }
    }

    public bool CanAcceptItem(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        ModuleDefinition def = GetModuleDefinition();

        if (def == null || !def.HasStorage || def.Storage == null)
            return false;

        StorageModuleDefinition storage = def.Storage;

        if (storage.IsFixedStorage)
            return true;

        if (storage.IsContainerRack)
            return CanRackAcceptItem(storage, item);

        return false;
    }

    public bool HasAnyContents()
    {
        if (containerState == null || containerState.Slots == null)
            return false;

        for (int i = 0; i < containerState.Slots.Count; i++)
        {
            InventorySlot slot = containerState.Slots[i];

            if (slot != null && !slot.IsEmpty && slot.Instance != null)
                return true;
        }

        return false;
    }

    private bool CanRackAcceptItem(StorageModuleDefinition storage, ItemInstance item)
    {
        ItemDefinition itemDef = item.Definition;

        if (itemDef == null)
            return false;

        bool isPortableContainer = itemDef.IsContainer;

        // Temporary cargo crate detection.
        // Later this should become a real ItemCategoryFlags.CargoCrate or
        // ItemDefinition flag, because string-matching IDs is how code starts smoking indoors.
        bool isCargoCrate = IsProbablyCargoCrate(itemDef);

        if (isPortableContainer && storage.AcceptsPortableContainers)
            return true;

        if (isCargoCrate && storage.AcceptsCargoCrates)
            return true;

        return false;
    }

    private static bool IsProbablyCargoCrate(ItemDefinition itemDef)
    {
        if (itemDef == null)
            return false;

        string id = itemDef.ItemId;
        string display = itemDef.DisplayName;

        if (!string.IsNullOrWhiteSpace(id) &&
            id.ToLowerInvariant().Contains("crate"))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(display) &&
            display.ToLowerInvariant().Contains("crate"))
        {
            return true;
        }

        return false;
    }

    private ModuleDefinition GetModuleDefinition()
    {
        CacheRefs();

        return installedModule != null
            ? installedModule.Definition
            : null;
    }

    public ItemContainerSnapshot CaptureContainerSnapshot()
    {
        EnsureContainer();

        return containerState != null
            ? containerState.ToSnapshot()
            : null;
    }

    public void RestoreContainerSnapshot(ItemContainerSnapshot snapshot, IItemDefinitionResolver resolver)
    {
        ModuleDefinition def = GetModuleDefinition();

        if (def == null || !def.HasStorage || def.Storage == null)
            return;

        if (snapshot != null)
        {
            containerState = ItemContainerState.FromSnapshot(snapshot, resolver);
        }
        else
        {
            int slotCount = Mathf.Max(1, def.Storage.SlotCount);
            int columnCount = Mathf.Max(1, def.Storage.ColumnCount);
            containerState = new ItemContainerState(slotCount, columnCount);
        }

        EnsureContainer();

        if (containerState != null)
            containerState.NotifyChanged();
    }

    public bool IsCargoSlotOccupied(int slotIndex)
    {
        EnsureContainer();

        if (cargoRackState == null)
            return false;

        CargoRackSlot slot = cargoRackState.GetSlot(slotIndex);
        return slot != null && !slot.IsEmpty;
    }

    public bool IsItemSlotOccupied(int slotIndex)
    {
        EnsureContainer();

        if (containerState == null)
            return false;

        InventorySlot slot = containerState.GetSlot(slotIndex);
        return slot != null && !slot.IsEmpty && slot.Instance != null;
    }

    public bool IsRackSlotCompletelyEmpty(int slotIndex)
    {
        return !IsItemSlotOccupied(slotIndex) && !IsCargoSlotOccupied(slotIndex);
    }

    public bool CanAcceptItemInSlot(ItemInstance item, int slotIndex)
    {
        if (item == null)
            return false;

        if (IsCargoSlotOccupied(slotIndex))
            return false;

        return CanAcceptItem(item);
    }

    public bool CanStoreCargoCrate(CargoCrate crate)
    {
        if (crate == null)
            return false;

        ModuleDefinition def = GetModuleDefinition();
        if (def == null || !def.HasStorage || def.Storage == null)
            return false;

        if (!def.Storage.IsContainerRack)
            return false;

        if (!def.Storage.AcceptsCargoCrates)
            return false;

        EnsureContainer();

        if (cargoRackState == null)
            return false;

        for (int i = 0; i < cargoRackState.SlotCount; i++)
        {
            if (IsRackSlotCompletelyEmpty(i))
                return true;
        }

        return false;
    }

    public bool TryStoreCargoCrate(CargoCrate crate, out int storedSlotIndex)
    {
        storedSlotIndex = -1;

        if (!CanStoreCargoCrate(crate))
            return false;

        CargoCrateStoredSnapshot snapshot = CargoCrateSnapshotUtility.Capture(crate);
        if (snapshot == null)
            return false;

        for (int i = 0; i < cargoRackState.SlotCount; i++)
        {
            if (!IsRackSlotCompletelyEmpty(i))
                continue;

            CargoRackSlot slot = cargoRackState.GetSlot(i);
            if (slot == null)
                continue;

            slot.Set(snapshot);
            storedSlotIndex = i;

            cargoRackState.NotifyChanged();

            if (containerState != null)
                containerState.NotifyChanged();

            Destroy(crate.gameObject);
            return true;
        }

        return false;
    }

    public CargoCrateStoredSnapshot RemoveCargoCrateSnapshotAt(int slotIndex)
    {
        EnsureContainer();

        if (cargoRackState == null)
            return null;

        CargoRackSlot slot = cargoRackState.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
            return null;

        CargoCrateStoredSnapshot snapshot = slot.Clear();

        cargoRackState.NotifyChanged();

        if (containerState != null)
            containerState.NotifyChanged();

        return snapshot;
    }

    public CargoRackStateSnapshot CaptureCargoRackSnapshot()
    {
        EnsureContainer();

        return cargoRackState != null
            ? cargoRackState.ToSnapshot()
            : null;
    }

    public void RestoreCargoRackSnapshot(CargoRackStateSnapshot snapshot)
    {
        ModuleDefinition def = GetModuleDefinition();

        if (def == null || !def.HasStorage || def.Storage == null)
            return;

        if (!def.Storage.IsContainerRack)
            return;

        if (snapshot != null)
        {
            cargoRackState = CargoRackState.FromSnapshot(snapshot);
        }
        else
        {
            int slotCount = Mathf.Max(1, def.Storage.SlotCount);
            int columnCount = Mathf.Max(1, def.Storage.ColumnCount);
            cargoRackState = new CargoRackState(slotCount, columnCount);
        }

        EnsureContainer();

        cargoRackState?.NotifyChanged();
        containerState?.NotifyChanged();
    }

    private void CacheRefs()
    {
        if (installedModule == null)
            installedModule = GetComponent<InstalledModule>();
    }
}