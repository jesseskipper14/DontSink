using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItem : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [SerializeField] private ItemInstance itemInstance;
    [SerializeField] private int interactionPriority = 10;
    [SerializeField] private float maxPickupDistance = 1.5f;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private GameObject highlightObject;

    public ItemInstance Instance => itemInstance;
    public ItemDefinition Item => itemInstance != null ? itemInstance.Definition : null;
    public int Quantity => itemInstance != null ? itemInstance.Quantity : 0;

    public int InteractionPriority => interactionPriority;

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

    public bool CanInteract(in InteractContext context)
    {
        if (itemInstance == null || itemInstance.Definition == null || itemInstance.Quantity <= 0)
            return false;

        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxPickupDistance)
            return false;

        PlayerInventory inventory = FindInventory(context.InteractorGO);
        if (inventory == null)
            return false;

        return inventory.CanFullyAdd(itemInstance);
    }

    public void Interact(in InteractContext context)
    {
        if (itemInstance == null || itemInstance.Definition == null || itemInstance.Quantity <= 0)
            return;

        PlayerInventory inventory = FindInventory(context.InteractorGO);
        if (inventory == null)
            return;

        if (!inventory.TryAddInstance(itemInstance))
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

    private static PlayerInventory FindInventory(GameObject actor)
    {
        if (actor == null)
            return null;

        PlayerInventory inventory = actor.GetComponent<PlayerInventory>();
        if (inventory != null)
            return inventory;

        inventory = actor.GetComponentInChildren<PlayerInventory>(true);
        if (inventory != null)
            return inventory;

        return actor.GetComponentInParent<PlayerInventory>();
    }
}