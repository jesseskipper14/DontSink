using UnityEngine;

[DisallowMultipleComponent]
public sealed class StorageModule : MonoBehaviour, IInstalledModuleLifecycle
{
    [Header("Runtime")]
    [SerializeField] private InstalledModule installedModule;
    [SerializeField] private ItemContainerState containerState;

    private ItemContainerState subscribedContainerState;

    public InstalledModule InstalledModule => installedModule;
    public ItemContainerState ContainerState => containerState;

    public bool HasContainer => containerState != null;
    public float ContentsMass =>
        containerState != null
            ? containerState.ContentsMass
            : 0f;

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
        RefreshInstalledModuleMass();
    }

    public void OnRemoved()
    {
        UnbindContainerChanged();

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

        BindContainerChanged();
        RefreshInstalledModuleMass();
    }

    public bool CanAcceptItem(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        ModuleDefinition def = GetModuleDefinition();

        if (def == null || !def.HasStorage || def.Storage == null)
            return false;

        StorageModuleDefinition storage = def.Storage;

        // Installed storage policy is authored on ModuleDefinition.Storage.
        // This runtime component owns state and delegates acceptance decisions.
        return storage.CanAcceptItem(item.Definition);
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

        UnbindContainerChanged();

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
        return !IsItemSlotOccupied(slotIndex);
    }

    public bool CanAcceptItemInSlot(ItemInstance item, int slotIndex)
    {
        if (item == null)
            return false;

        // A tether deployment module may temporarily remove its payload
        // ItemInstance from the physical storage slot while the WorldItem is
        // deployed. That slot is still logically reserved for that payload.
        TetherDeploymentModule tetherDeployment =
            GetComponent<TetherDeploymentModule>();

        if (tetherDeployment != null &&
            tetherDeployment.IsPayloadSlotReserved(slotIndex))
        {
            return false;
        }

        return CanAcceptItem(item);
    }

    private void BindContainerChanged()
    {
        if (ReferenceEquals(subscribedContainerState, containerState))
            return;

        UnbindContainerChanged();

        subscribedContainerState = containerState;

        if (subscribedContainerState != null)
            subscribedContainerState.Changed += HandleContainerChanged;
    }

    private void UnbindContainerChanged()
    {
        if (subscribedContainerState != null)
            subscribedContainerState.Changed -= HandleContainerChanged;

        subscribedContainerState = null;
    }

    private void HandleContainerChanged()
    {
        RefreshInstalledModuleMass();
    }

    private void RefreshInstalledModuleMass()
    {
        CacheRefs();

        if (installedModule != null)
            installedModule.RefreshMassFromDefinitionAndContents();
    }

    private void CacheRefs()
    {
        if (installedModule == null)
            installedModule = GetComponent<InstalledModule>();
    }

    private void OnDestroy()
    {
        UnbindContainerChanged();
    }
}