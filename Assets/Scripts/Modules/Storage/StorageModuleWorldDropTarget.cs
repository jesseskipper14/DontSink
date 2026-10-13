using UnityEngine;

[DisallowMultipleComponent]
public sealed class StorageModuleWorldDropTarget : MonoBehaviour, IWorldItemDropTarget
{
    [Header("Refs")]
    [SerializeField] private StorageModule storageModule;
    [SerializeField] private InstalledModule installedModule;

    [Header("Access")]
    [SerializeField, Min(0f)] private float maxDepositDistance = 2.25f;

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

    public bool CanAcceptWorldDrop(
        in WorldItemDropContext context,
        ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        CacheRefs();

        if (storageModule == null || !CanAccess(context))
            return false;

        storageModule.EnsureContainer();

        return ContainerPlacementUtility.CanAutoInsert(storageModule, item);
    }

    public bool TryAcceptWorldDrop(
        in WorldItemDropContext context,
        ItemInstance item,
        out ItemInstance remainder)
    {
        remainder = item;

        if (!GameplayAuthority.CanRun(GameplayAuthorityMode.SinglePlayerOrAuthoritative))
            return false;

        if (!CanAcceptWorldDrop(
                in context,
                item))
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

    private bool CanAccess(in WorldItemDropContext context)
    {
        if (!context.HasRequester ||
            Vector2.Distance(context.Requester.transform.position, transform.position) > maxDepositDistance)
            return false;

        // Check the live actor position, not a caller-supplied origin.
        if (_cachedBoat == null) return true;
        var boarding = context.Requester.GetComponentInParent<PlayerBoardingState>()
            ?? context.Requester.GetComponentInChildren<PlayerBoardingState>(true);
        if (boarding == null || !boarding.IsBoarded || boarding.CurrentBoatRoot == null)
            return false;
        var boardedBoat = boarding.CurrentBoatRoot.GetComponent<Boat>()
            ?? boarding.CurrentBoatRoot.GetComponentInParent<Boat>();
        return boardedBoat == _cachedBoat;
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
