using UnityEngine;

[DisallowMultipleComponent]
public sealed class RackStoredItemInteractable :
    MonoBehaviour,
    IPickupInteractable,
    IPickupPromptProvider,
    IInteractionLabelProvider,
    IInteractionPromptDisplayPolicyProvider
{
    [Header("Refs")]
    [SerializeField] private StorageModule storageModule;
    [SerializeField] private Transform promptAnchor;

    [Header("Boat Access")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    [Tooltip("If true, local OnMouseEnter highlight is disabled. Prefer Interactor2D-driven highlight later.")]
    [SerializeField] private bool disableLocalMouseHoverHighlight = true;

    private Boat _cachedBoat;

    [Header("Slot")]
    [SerializeField] private int slotIndex = -1;

    [Header("Pickup")]
    [SerializeField] private int pickupPriority = 35;
    [SerializeField] private PickupInteractionMode pickupMode = PickupInteractionMode.Hold;
    [SerializeField] private float pickupHoldDuration = 0.35f;
    [SerializeField] private float maxPickupDistance = 2.0f;

    [Header("Prompt / Hover")]
    [SerializeField] private bool allowPromptOutOfPickupRange = true;

    [Header("Hover Highlight")]
    [SerializeField] private bool enableHoverHighlight = true;

    [Tooltip("Sprite tint while hovered.")]
    [SerializeField] private Color hoverTint = new Color(1f, 0.92f, 0.45f, 1f);

    [Tooltip("How much larger the visual becomes while hovered.")]
    [SerializeField, Min(1f)] private float hoverScaleMultiplier = 1.12f;

    [Tooltip("How quickly hover tint/scale blend.")]
    [SerializeField, Min(0.01f)] private float hoverLerpSpeed = 16f;

    [Tooltip("If true, highlight only when the item is still valid/pickup-able at a basic slot level.")]
    [SerializeField] private bool requireValidStoredItemForHover = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public int PickupPriority => pickupPriority;
    public PickupInteractionMode PickupMode => pickupMode;
    public float PickupHoldDuration => Mathf.Max(0.05f, pickupHoldDuration);

    private SpriteRenderer _spriteRenderer;
    private Vector3 _baseScale;
    private Color _baseColor = Color.white;
    private bool _hovered;

    public void Initialize(StorageModule module, int index)
    {
        storageModule = module;
        slotIndex = index;

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheVisualRefs();
        CacheBoat();
    }

    private void Awake()
    {
        if (promptAnchor == null)
            promptAnchor = transform;

        CacheVisualRefs();
        CacheBoat();
    }

    private void OnEnable()
    {
        CacheVisualRefs();
        SnapHoverVisualState();
    }

    private void OnDisable()
    {
        _hovered = false;
        RestoreBaseVisuals();
    }

    private void Update()
    {
        UpdateHoverVisuals();
    }

    private void OnMouseEnter()
    {
        if (disableLocalMouseHoverHighlight)
            return;

        if (!enableHoverHighlight)
            return;

        if (requireValidStoredItemForHover && !HasValidStoredItem())
            return;

        _hovered = true;
    }

    private void OnMouseExit()
    {
        _hovered = false;
    }

    public bool CanPickup(in InteractContext context)
    {
        if (!CanAccessByBoatContext(context))
            return false;

        if (!IsInRange(context))
            return false;

        if (!TryGetSlot(out InventorySlot slot))
            return false;

        if (slot.IsEmpty || slot.Instance == null)
            return false;

        PlayerInventory inventory = FindPlayerInventory(context);
        if (inventory == null)
            return false;

        return true;
    }

    public void Pickup(in InteractContext context)
    {
        if (!CanPickup(context))
            return;

        if (!TryGetSlot(out InventorySlot slot))
            return;

        ItemInstance item = slot.Instance;
        if (item == null || item.Definition == null)
            return;

        PlayerInventory inventory = FindPlayerInventory(context);
        if (inventory == null)
            return;

        slot.Clear();

        if (inventory.TryAutoInsert(item, out ItemInstance remainder))
        {
            if (remainder != null && !remainder.IsDepleted())
            {
                // Partial insert. Put the remainder back in the rack slot.
                slot.Set(remainder);
            }

            storageModule.ContainerState.NotifyChanged();
            inventory.NotifyChanged();

            Log($"Picked up rack item '{DescribeItem(item)}' from slot {slotIndex}. remainder={DescribeItem(remainder)}");

            // The rack visual system should refresh from NotifyChanged and destroy/rebuild visuals.
            // Destroying this one immediately avoids a stale pickup target hanging around for a frame.
            Destroy(gameObject);
            return;
        }

        // Rollback if inventory rejected it entirely.
        slot.Set(item);
        storageModule.ContainerState.NotifyChanged();

        Log($"Pickup failed, rolled back item '{DescribeItem(item)}' to slot {slotIndex}.");
    }

    public string GetPickupPromptVerb(in InteractContext context)
    {
        if (TryGetSlot(out InventorySlot slot) &&
            slot != null &&
            !slot.IsEmpty &&
            slot.Instance != null &&
            slot.Instance.Definition != null)
        {
            return $"Pick Up {slot.Instance.Definition.DisplayName}";
        }

        return "Pick Up";
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    private void CacheVisualRefs()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        _baseScale = transform.localScale;

        if (_spriteRenderer != null)
            _baseColor = _spriteRenderer.color;
    }

    private void UpdateHoverVisuals()
    {
        if (!enableHoverHighlight)
            return;

        if (_spriteRenderer == null)
            CacheVisualRefs();

        bool effectiveHover = _hovered;

        if (effectiveHover && requireValidStoredItemForHover && !HasValidStoredItem())
            effectiveHover = false;

        Vector3 targetScale = effectiveHover
            ? _baseScale * hoverScaleMultiplier
            : _baseScale;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            1f - Mathf.Exp(-hoverLerpSpeed * Time.deltaTime));

        if (_spriteRenderer != null)
        {
            Color targetColor = effectiveHover ? hoverTint : _baseColor;

            _spriteRenderer.color = Color.Lerp(
                _spriteRenderer.color,
                targetColor,
                1f - Mathf.Exp(-hoverLerpSpeed * Time.deltaTime));
        }
    }

    private void SnapHoverVisualState()
    {
        if (_spriteRenderer == null)
            CacheVisualRefs();

        transform.localScale = _baseScale;

        if (_spriteRenderer != null)
            _spriteRenderer.color = _baseColor;
    }

    private void RestoreBaseVisuals()
    {
        transform.localScale = _baseScale;

        if (_spriteRenderer != null)
            _spriteRenderer.color = _baseColor;
    }

    private bool HasValidStoredItem()
    {
        if (!TryGetSlot(out InventorySlot slot))
            return false;

        return slot != null &&
               !slot.IsEmpty &&
               slot.Instance != null &&
               slot.Instance.Definition != null;
    }

    private bool TryGetSlot(out InventorySlot slot)
    {
        slot = null;

        if (storageModule == null)
            return false;

        storageModule.EnsureContainer();

        ItemContainerState state = storageModule.ContainerState;
        if (state == null)
            return false;

        if (slotIndex < 0 || slotIndex >= state.SlotCount)
            return false;

        slot = state.GetSlot(slotIndex);
        return slot != null;
    }

    private bool IsInRange(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxPickupDistance;
    }

    private static PlayerInventory FindPlayerInventory(in InteractContext context)
    {
        if (context.InteractorGO == null)
            return null;

        PlayerInventory inventory = context.InteractorGO.GetComponentInChildren<PlayerInventory>();
        if (inventory != null)
            return inventory;

        return context.InteractorGO.GetComponentInParent<PlayerInventory>();
    }

    private string DescribeItem(ItemInstance item)
    {
        if (item == null)
            return "empty";

        string id = item.Definition != null ? item.Definition.ItemId : "NO_DEF";
        return $"{id} x{item.Quantity} inst={item.InstanceId}";
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[RackStoredItemInteractable:{name}] {msg}", this);
    }

    public bool CanShowPickupPrompt(in InteractContext context)
    {
        if (!CanAccessByBoatContext(context))
            return false;

        if (!TryGetSlot(out InventorySlot slot))
            return false;

        if (slot.IsEmpty || slot.Instance == null)
            return false;

        if (!allowPromptOutOfPickupRange && !IsInRange(context))
            return false;

        return true;
    }

    public bool ShouldShowHoverLabel(in InteractContext context)
    {
        return CanShowPickupPrompt(context);
    }

    public string GetInteractionLabel(in InteractContext context)
    {
        if (TryGetSlot(out InventorySlot slot) &&
            slot != null &&
            !slot.IsEmpty &&
            slot.Instance != null &&
            slot.Instance.Definition != null)
        {
            return slot.Instance.Definition.DisplayName;
        }

        return "Stored Item";
    }

    private bool CanAccessByBoatContext(in InteractContext context)
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        CacheBoat();

        // If this rack item is not part of a boat, allow normal use.
        if (_cachedBoat == null)
            return true;

        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding == null)
            return false;

        if (!boarding.IsBoarded)
            return false;

        return boarding.CurrentBoatRoot == _cachedBoat.transform;
    }

    private PlayerBoardingState FindBoardingState(in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerBoardingState fromGO =
                context.InteractorGO.GetComponentInParent<PlayerBoardingState>();

            if (fromGO != null)
                return fromGO;
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState fromTransform =
                context.InteractorTransform.GetComponentInParent<PlayerBoardingState>();

            if (fromTransform != null)
                return fromTransform;
        }

        return null;
    }

    private void CacheBoat()
    {
        if (_cachedBoat != null)
            return;

        if (storageModule != null)
            _cachedBoat = storageModule.GetComponentInParent<Boat>();

        if (_cachedBoat == null)
            _cachedBoat = GetComponentInParent<Boat>();
    }
}