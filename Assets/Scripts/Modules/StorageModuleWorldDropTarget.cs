using UnityEngine;

[DisallowMultipleComponent]
public sealed class StorageModuleWorldDropTarget : MonoBehaviour, IWorldItemDropTarget
{
    [Header("Refs")]
    [SerializeField] private StorageModule storageModule;
    [SerializeField] private InstalledModule installedModule;

    [Header("Access")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;
    [SerializeField] private bool allowWhenNotPartOfBoat = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Boat _cachedBoat;

    private void Reset()
    {
        CacheRefs();
    }

    private void Awake()
    {
        CacheRefs();
    }

    public bool CanAcceptWorldDrop(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        CacheRefs();

        if (storageModule == null)
            return false;

        storageModule.EnsureContainer();

        return ContainerPlacementUtility.CanAutoInsert(storageModule, item);
    }

    public bool TryAcceptWorldDrop(ItemInstance item, out ItemInstance remainder)
    {
        remainder = item;

        if (!CanAcceptWorldDrop(item))
            return false;

        bool ok = ContainerPlacementUtility.TryAutoInsert(
            storageModule,
            item,
            out remainder);

        if (ok)
        {
            Log($"Accepted world drop into storage. item='{DescribeItem(item)}' remainder='{DescribeItem(remainder)}'");
            return true;
        }

        return false;
    }

    private void CacheRefs()
    {
        if (storageModule == null)
            storageModule = GetComponent<StorageModule>();

        if (storageModule == null)
            storageModule = GetComponentInParent<StorageModule>();

        if (installedModule == null)
            installedModule = GetComponent<InstalledModule>();

        if (installedModule == null)
            installedModule = GetComponentInParent<InstalledModule>();

        if (_cachedBoat == null)
            _cachedBoat = GetComponentInParent<Boat>();
    }

    private string DescribeItem(ItemInstance item)
    {
        if (item == null)
            return "empty";

        string itemId = item.Definition != null ? item.Definition.ItemId : "NO_DEF";
        return $"{itemId} x{item.Quantity} inst={item.InstanceId}";
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[StorageModuleWorldDropTarget:{name}] {msg}", this);
    }
}