using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItem : MonoBehaviour, IPickupInteractable, IInteractPromptProvider
{
    [SerializeField] private ItemInstance itemInstance;
    [SerializeField] private int interactionPriority = 10;
    [SerializeField] private float maxPickupDistance = 1.5f;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private GameObject highlightObject;

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

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private void Awake()
    {
        if (itemInstance != null)
            itemInstance.EnsureContainerStateMatchesDefinition();

        SetHighlighted(false);
    }

    public void Initialize(ItemInstance instance)
    {
        itemInstance = instance;

        if (itemInstance != null)
            itemInstance.EnsureContainerStateMatchesDefinition();

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

        var resolver = FindAcquisitionResolver(context.InteractorGO);
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

        ItemAcquisitionResolver resolver = FindAcquisitionResolver(context.InteractorGO);
        if (resolver == null)
            return;

        if (!resolver.TryAcquire(itemInstance))
            return;

        itemInstance = null;
        SetHighlighted(false);
        Destroy(gameObject);
    }

    public string GetPromptVerb(in InteractContext context) => "Pick Up";
    public Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public void SetHighlighted(bool highlighted)
    {
        if (highlightObject != null)
            highlightObject.SetActive(highlighted);
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
        if (!verboseLogging) return;
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