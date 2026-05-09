using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItem : MonoBehaviour, IPickupInteractable, IInteractPromptProvider
{
    [SerializeReference] private ItemInstance itemInstance;
    [SerializeField] private int interactionPriority = 10;
    [SerializeField] private float maxPickupDistance = 1.5f;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private GameObject highlightObject;

    [Header("Boat Access")]
    [Tooltip("If true, world items that belong to a Boat can only be picked up by players boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    [Tooltip("If true, world items not under a Boat remain pickup-able. This preserves normal dock/world item behavior.")]
    [SerializeField] private bool allowAccessWhenNotPartOfBoat = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private BoatOwnedItem _ownedItem;

    public ItemInstance Instance => itemInstance;
    public ItemDefinition Item => itemInstance != null ? itemInstance.Definition : null;
    public int Quantity => itemInstance != null ? itemInstance.Quantity : 0;

    public int PickupPriority => interactionPriority;

    public PickupInteractionMode PickupMode =>
        itemInstance != null && itemInstance.Definition != null
            ? itemInstance.Definition.PickupMode
            : PickupInteractionMode.Instant;

    public float PickupHoldDuration =>
        itemInstance != null && itemInstance.Definition != null
            ? itemInstance.Definition.PickupHoldDuration
            : 0.4f;

    private Boat _cachedBoat;

    private void Reset()
    {
        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoat();
    }

    private void Awake()
    {
        CacheBoat();

        if (itemInstance != null)
            itemInstance.EnsureContainerStateMatchesDefinition();

        SetHighlighted(false);

    }

    public void Initialize(ItemInstance instance)
    {
        itemInstance = instance;

        if (itemInstance != null)
            itemInstance.EnsureContainerStateMatchesDefinition();

        CacheBoat();
        SetHighlighted(false);
    }

    public bool CanPickup(in InteractContext context)
    {
        if (itemInstance == null)
        {
            Log("CanPickup FAIL: itemInstance is null");
            return false;
        }

        if (itemInstance.Definition == null)
        {
            Log("CanPickup FAIL: definition is null");
            return false;
        }

        if (itemInstance.Quantity <= 0)
        {
            Log("CanPickup FAIL: quantity <= 0");
            return false;
        }

        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxPickupDistance)
        {
            Log($"CanPickup FAIL: too far | dist={dist:F2} max={maxPickupDistance}");
            return false;
        }

        if (!CanAccessByBoatContext(context))
        {
            Log("CanPickup FAIL: boat access denied");
            return false;
        }

        ItemAcquisitionResolver resolver = FindAcquisitionResolver(context.InteractorGO);
        if (resolver == null)
        {
            Log($"CanPickup FAIL: resolver NOT FOUND | actor={context.InteractorGO?.name}");
            return false;
        }

        bool canAcquire = resolver.CanAcquire(itemInstance);

        Log($"CanPickup RESULT: {canAcquire} | item={DescribeItem(itemInstance)}");

        return canAcquire;
    }

    public void Pickup(in InteractContext context)
    {
        if (itemInstance == null || itemInstance.Definition == null || itemInstance.Quantity <= 0)
            return;

        if (!CanAccessByBoatContext(context))
            return;

        ItemAcquisitionResolver resolver = FindAcquisitionResolver(context.InteractorGO);
        if (resolver == null)
            return;

        if (!resolver.TryAcquire(itemInstance))
            return;

        itemInstance = null;
        BoatOwnedItem owned = GetComponent<BoatOwnedItem>();
        if (owned != null)
            owned.ClearOwnership();

        SetHighlighted(false);
        Destroy(gameObject);
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (!CanAccessByBoatContext(context))
            return "Board Boat";

        return "Pick Up";
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (highlightObject != null)
            highlightObject.SetActive(highlighted);
    }

    private bool CanAccessByBoatContext(in InteractContext context)
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        CacheBoatOwnership();

        if (_ownedItem == null || !_ownedItem.IsOwnedByBoat)
        {
            PlayerBoardingState unownedBoarding = FindBoardingState(context);
            if (unownedBoarding != null && unownedBoarding.IsBoarded)
            {
                Log("CanAccessByBoatContext FAIL: interactor is boarded, but item is not boat-owned.");
                return false;
            }

            return allowAccessWhenNotPartOfBoat;
        }

        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding == null || !boarding.IsBoarded)
            return false;

        Boat currentBoat = null;

        if (boarding.CurrentBoatRoot != null)
            currentBoat =
                boarding.CurrentBoatRoot.GetComponent<Boat>() ??
                boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

        if (currentBoat == null)
            return false;

        return currentBoat.BoatInstanceId == _ownedItem.OwningBoatInstanceId;
    }

    private void CacheBoatOwnership()
    {
        if (_ownedItem == null)
            _ownedItem = GetComponent<BoatOwnedItem>();
    }

    private PlayerBoardingState FindBoardingState(in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerBoardingState fromGO =
                context.InteractorGO.GetComponentInParent<PlayerBoardingState>();

            if (fromGO != null)
                return fromGO;

            fromGO = context.InteractorGO.GetComponentInChildren<PlayerBoardingState>(true);

            if (fromGO != null)
                return fromGO;
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState fromTransform =
                context.InteractorTransform.GetComponentInParent<PlayerBoardingState>();

            if (fromTransform != null)
                return fromTransform;

            fromTransform =
                context.InteractorTransform.GetComponentInChildren<PlayerBoardingState>(true);

            if (fromTransform != null)
                return fromTransform;
        }

        return null;
    }

    private void CacheBoat()
    {
        if (_cachedBoat == null)
            _cachedBoat = GetComponentInParent<Boat>();
    }

    private static ItemAcquisitionResolver FindAcquisitionResolver(GameObject actor)
    {
        if (actor == null)
            return null;

        ItemAcquisitionResolver resolver = actor.GetComponent<ItemAcquisitionResolver>();
        if (resolver != null)
            return resolver;

        resolver = actor.GetComponentInChildren<ItemAcquisitionResolver>(true);
        if (resolver != null)
            return resolver;

        return actor.GetComponentInParent<ItemAcquisitionResolver>();
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[WorldItem:{name}] {msg}", this);
    }

    private string DescribeItem(ItemInstance item)
    {
        if (item == null)
            return "empty";

        string itemId = item.Definition != null ? item.Definition.ItemId : "NO_DEF";
        return $"{itemId} x{item.Quantity} inst={item.InstanceId}";
    }
}