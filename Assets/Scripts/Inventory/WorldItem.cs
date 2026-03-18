using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItem : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Contents")]
    [SerializeField] private ItemDefinition item;
    [Min(1)]
    [SerializeField] private int quantity = 1;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 10;
    [SerializeField] private float maxPickupDistance = 1.5f;
    [SerializeField] private Transform promptAnchor;

    [Header("Highlight")]
    [SerializeField] private GameObject highlightObject;

    public ItemDefinition Item => item;
    public int Quantity => Mathf.Max(1, quantity);

    public int InteractionPriority => interactionPriority;

    private void Awake()
    {
        CacheHighlightObjectIfNeeded();
        SetHighlighted(false);
    }

    public void Initialize(ItemDefinition definition, int amount)
    {
        item = definition;
        quantity = Mathf.Max(1, amount);

        if (item != null)
            gameObject.name = $"WorldItem_{item.DisplayName}_{quantity}";

        CacheHighlightObjectIfNeeded();
        SetHighlighted(false);
    }

    public bool CanInteract(in InteractContext context)
    {
        if (item == null || quantity <= 0)
            return false;

        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxPickupDistance)
            return false;

        PlayerInventory inventory = FindInventory(context.InteractorGO);
        if (inventory == null)
            return false;

        return CanInventoryFullyAccept(inventory, item, quantity);
    }

    public void Interact(in InteractContext context)
    {
        if (item == null || quantity <= 0)
            return;

        PlayerInventory inventory = FindInventory(context.InteractorGO);
        if (inventory == null)
            return;

        bool success = inventory.TryAdd(item, quantity);
        if (!success)
            return;

        SetHighlighted(false);
        Destroy(gameObject);
    }

    public string GetPromptVerb(in InteractContext context)
    {
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

    private void CacheHighlightObjectIfNeeded()
    {
        if (highlightObject != null)
            return;

        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == transform)
                continue;

            if (t.CompareTag("Highlight"))
            {
                highlightObject = t.gameObject;
                break;
            }
        }
    }

    private static PlayerInventory FindInventory(GameObject actor)
    {
        if (actor == null)
            return null;

        if (actor.TryGetComponent<PlayerInventory>(out var inventory))
            return inventory;

        inventory = actor.GetComponentInChildren<PlayerInventory>(true);
        if (inventory != null)
            return inventory;

        inventory = actor.GetComponentInParent<PlayerInventory>();
        return inventory;
    }

    private static bool CanInventoryFullyAccept(PlayerInventory inventory, ItemDefinition item, int quantity)
    {
        if (inventory == null || item == null || quantity <= 0)
            return false;

        if (!item.StowableInInventory)
            return false;

        int remaining = quantity;

        for (int i = 0; i < inventory.Slots.Count; i++)
        {
            InventorySlot slot = inventory.Slots[i];
            if (slot == null || slot.IsEmpty) continue;
            if (slot.Item != item) continue;

            remaining -= slot.RemainingCapacityFor(item);
            if (remaining <= 0)
                return true;
        }

        for (int i = 0; i < inventory.Slots.Count; i++)
        {
            InventorySlot slot = inventory.Slots[i];
            if (slot == null || !slot.IsEmpty) continue;

            remaining -= item.MaxStack;
            if (remaining <= 0)
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        quantity = Mathf.Max(1, quantity);
    }
#endif
}