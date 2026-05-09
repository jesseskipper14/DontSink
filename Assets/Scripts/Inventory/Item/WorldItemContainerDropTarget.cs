using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItemContainerDropTarget : MonoBehaviour, IWorldItemDropTarget
{
    [SerializeField] private WorldItem worldItem;

    [Header("Range")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float maxDepositDistance = 2.25f;

    [Header("Boat Access")]
    [Tooltip("If true, world containers that belong to a Boat can only accept drops from players boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    [Tooltip("If true, world containers not owned by or parented under a Boat remain usable. This preserves normal dock/world containers.")]
    [SerializeField] private bool allowAccessWhenNotPartOfBoat = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private BoatOwnedItem _ownedItem;
    private Boat _cachedParentBoat;

    private void Awake()
    {
        if (worldItem == null)
            worldItem = GetComponent<WorldItem>();

        if (playerTransform == null)
        {
            PlayerInventory playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (playerInventory != null)
                playerTransform = playerInventory.transform;
        }

        CacheBoatContext();
    }

    public bool CanAcceptWorldDrop(ItemInstance incoming)
    {
        if (!IsInRange())
            return false;

        if (!CanAccessByBoatContext())
            return false;

        if (incoming == null)
            return false;

        ItemInstance containerItem = worldItem != null ? worldItem.Instance : null;
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
            return false;

        if (ReferenceEquals(incoming, containerItem))
            return false;

        return ContainerPlacementUtility.CanAutoInsert(containerItem, incoming);
    }

    public bool TryAcceptWorldDrop(ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (!IsInRange())
            return false;

        if (!CanAccessByBoatContext())
            return false;

        if (incoming == null)
            return false;

        ItemInstance containerItem = worldItem != null ? worldItem.Instance : null;
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
            return false;

        if (ReferenceEquals(incoming, containerItem))
            return false;

        return ContainerPlacementUtility.TryAutoInsert(containerItem, incoming, out remainder);
    }

    private bool IsInRange()
    {
        if (playerTransform == null)
            return false;

        float dist = Vector2.Distance(playerTransform.position, transform.position);
        return dist <= maxDepositDistance;
    }

    private bool CanAccessByBoatContext()
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        CacheBoatContext();

        if (_ownedItem != null && _ownedItem.IsOwnedByBoat)
        {
            bool ok = IsPlayerBoardedOnBoatId(_ownedItem.OwningBoatInstanceId);

            Log(
                $"Drop access by BoatOwnedItem | item='{name}' ownedBoatId='{_ownedItem.OwningBoatInstanceId}' ok={ok}");

            return ok;
        }

        if (_cachedParentBoat != null)
        {
            bool ok = IsPlayerBoardedOnBoat(_cachedParentBoat);

            Log(
                $"Drop access by parent Boat | item='{name}' boat='{_cachedParentBoat.name}' id='{_cachedParentBoat.BoatInstanceId}' ok={ok}");

            return ok;
        }

        PlayerBoardingState boarding = FindPlayerBoardingState();
        if (boarding != null && boarding.IsBoarded)
        {
            Log($"Drop access denied: player is boarded, but container '{name}' is not boat-owned.");
            return false;
        }

        return allowAccessWhenNotPartOfBoat;
    }

    private bool IsPlayerBoardedOnBoatId(string boatInstanceId)
    {
        if (string.IsNullOrWhiteSpace(boatInstanceId))
            return false;

        PlayerBoardingState boarding = FindPlayerBoardingState();
        if (boarding == null || !boarding.IsBoarded || boarding.CurrentBoatRoot == null)
            return false;

        Boat currentBoat =
            boarding.CurrentBoatRoot.GetComponent<Boat>() ??
            boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

        if (currentBoat == null)
            return false;

        return currentBoat.BoatInstanceId == boatInstanceId;
    }

    private bool IsPlayerBoardedOnBoat(Boat requiredBoat)
    {
        if (requiredBoat == null)
            return false;

        PlayerBoardingState boarding = FindPlayerBoardingState();
        if (boarding == null || !boarding.IsBoarded || boarding.CurrentBoatRoot == null)
            return false;

        Boat currentBoat =
            boarding.CurrentBoatRoot.GetComponent<Boat>() ??
            boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

        if (currentBoat == null)
            return false;

        if (!string.IsNullOrWhiteSpace(requiredBoat.BoatInstanceId) &&
            !string.IsNullOrWhiteSpace(currentBoat.BoatInstanceId))
        {
            return currentBoat.BoatInstanceId == requiredBoat.BoatInstanceId;
        }

        return currentBoat == requiredBoat || boarding.CurrentBoatRoot == requiredBoat.transform;
    }

    private PlayerBoardingState FindPlayerBoardingState()
    {
        if (playerTransform != null)
        {
            PlayerBoardingState fromTransform =
                playerTransform.GetComponentInParent<PlayerBoardingState>();

            if (fromTransform != null)
                return fromTransform;

            fromTransform =
                playerTransform.GetComponentInChildren<PlayerBoardingState>(true);

            if (fromTransform != null)
                return fromTransform;
        }

        return FindFirstObjectByType<PlayerBoardingState>();
    }

    private void CacheBoatContext()
    {
        if (_ownedItem == null)
            _ownedItem = GetComponent<BoatOwnedItem>();

        if (_cachedParentBoat == null)
            _cachedParentBoat = GetComponentInParent<Boat>();
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[WorldItemContainerDropTarget:{name}] {msg}", this);
    }
}