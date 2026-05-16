using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventorySlotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public static InventorySlotUI CurrentHoveredSlot { get; private set; }

    [Header("Visuals")]
    [SerializeField] private Image icon;
    [SerializeField] private Text countText;
    [SerializeField] private Sprite cargoFallbackIcon;
    [SerializeField] private GameObject selectionHighlight;
    [SerializeField] private Image purposeIcon;
    [SerializeField] private Image background;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color invalidTargetColor = new Color(1f, 0.45f, 0.45f, 1f);

    private bool _dragBeganThisPress;
    private bool _pointerIsDown;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Sprite assignedPurposeIcon;
    private IInventorySlotBinding binding;

    private PlayerInventoryUI owner;
    private InventoryDragController dragController;

    public RectTransform RectTransform => transform as RectTransform;

    public bool IsEquipmentSlot => binding is EquipmentSlotBinding;

    public int HotbarIndex
    {
        get
        {
            if (binding is HotbarSlotBinding hotbarBinding)
                return PlayerInventory.SlotTypeToHotbarIndex(hotbarBinding.SlotType);

            return -1;
        }
    }

    public BottomBarSlotType SlotType =>
        binding != null ? binding.SlotType : BottomBarSlotType.None;

    public bool SupportsSelection =>
        binding != null && binding.SupportsSelection;

    public string DebugSlotName => gameObject.name;

    public void SetOwner(PlayerInventoryUI ownerUI)
    {
        owner = ownerUI;
        Log($"SetOwner | owner={(owner != null ? owner.name : "NULL")}");
    }

    public void SetDragController(InventoryDragController controller)
    {
        dragController = controller;
        Log($"SetDragController | drag={(dragController != null ? dragController.name : "NULL")}");
    }

    public void Bind(IInventorySlotBinding newBinding)
    {
        binding = newBinding;
        Log($"Bind | binding={(binding != null ? binding.GetType().Name : "NULL")} | slotType={SlotType}");
        Refresh();
        SetSelected(false);
    }

    public void Refresh()
    {
        ItemInstance instance = GetInstance();
        bool hasItem = instance != null && instance.Definition != null;

        CargoCrateStoredSnapshot cargoSnapshot = null;
        bool hasCargo = !hasItem && TryGetCargoSnapshot(out cargoSnapshot);

        if (icon != null)
        {
            icon.enabled = hasItem || hasCargo;

            if (hasItem)
            {
                icon.sprite = instance.Definition.Icon;
            }
            else if (hasCargo)
            {
                icon.sprite = ResolveCargoIcon(cargoSnapshot);
            }
            else
            {
                icon.sprite = null;
            }
        }

        if (purposeIcon != null)
        {
            bool showPurpose = !hasItem && !hasCargo && assignedPurposeIcon != null;
            purposeIcon.enabled = showPurpose;
            purposeIcon.sprite = showPurpose ? assignedPurposeIcon : null;
        }

        if (countText != null)
        {
            if (hasItem)
                countText.text = instance.Quantity > 1 ? instance.Quantity.ToString() : "";
            else if (hasCargo)
                countText.text = cargoSnapshot.quantity > 1 ? cargoSnapshot.quantity.ToString() : FormatCargoShortLabel(cargoSnapshot);
            else
                countText.text = "";
        }

        if (!SupportsSelection && selectionHighlight != null)
            selectionHighlight.SetActive(false);

        Log(
            $"Refresh | slotType={SlotType} | hasItem={hasItem} | hasCargo={hasCargo} " +
            $"| item={DescribeItem(instance)} | cargo={(cargoSnapshot != null ? cargoSnapshot.itemId : "NULL")} " +
            $"| countText='{(countText != null ? countText.text : "NO_TEXT")}'");
    }

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.SetActive(selected && SupportsSelection);

        Log($"SetSelected | slotType={SlotType} | selected={selected} | supportsSelection={SupportsSelection}");
    }

    public ItemInstance RemoveItem()
    {
        if (binding == null)
        {
            LogWarning("RemoveItem failed because binding is null.");
            return null;
        }

        ItemInstance removed = binding.RemoveItem();
        Log($"RemoveItem | slotType={SlotType} | item={DescribeItem(removed)}");
        return removed;
    }

    public bool TryPlaceItem(ItemInstance incoming, out ItemInstance displaced)
    {
        displaced = null;

        if (binding == null)
        {
            LogWarning("TryPlaceItem failed because binding is null.");
            return false;
        }

        if (incoming == null)
        {
            LogWarning("TryPlaceItem called with NULL incoming item.");
            return false;
        }

        bool ok = binding.TryPlaceItem(incoming, out displaced);
        Log($"TryPlaceItem | slotType={SlotType} | binding={binding.GetType().Name} | incoming={DescribeItem(incoming)} | ok={ok} | displaced={DescribeItem(displaced)}");
        return ok;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CurrentHoveredSlot = this;
        Log($"OnPointerEnter | slotType={SlotType}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CurrentHoveredSlot == this)
            CurrentHoveredSlot = null;

        Log($"OnPointerExit | slotType={SlotType}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        // If a drag started during this press, do not also treat it like a quick-transfer click.
        if (ctrlHeld && !_dragBeganThisPress)
        {
            if (TryQuickTransfer())
            {
                Log($"OnPointerClick quick transfer | slotType={SlotType}");
                return;
            }
        }

        Log($"OnPointerClick | slotType={SlotType}");
        owner?.HandleSlotClicked(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        _pointerIsDown = true;
        _dragBeganThisPress = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        _pointerIsDown = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragController == null)
            return;

        if (dragController.IsDragging)
        {
            Log($"OnBeginDrag ignored because drag already active | slotType={SlotType}");
            return;
        }

        _dragBeganThisPress = true;

        ItemInstance boundItem = GetBoundItem();
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (ctrlHeld && boundItem != null && boundItem.CanSplit)
        {
            int splitAmount = Mathf.CeilToInt(boundItem.Quantity * 0.5f);
            ItemInstance split = boundItem.SplitOff(splitAmount);

            if (split != null)
            {
                Log($"OnBeginDrag split drag | slotType={SlotType} | split={DescribeItem(split)} | remaining={DescribeItem(boundItem)}");
                Refresh();
                dragController.BeginDrag(split, this);
                return;
            }
        }

        Log($"OnBeginDrag normal drag | slotType={SlotType} | item={DescribeItem(boundItem)}");
        dragController.BeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Log($"OnEndDrag | slotType={SlotType} | hovered={(CurrentHoveredSlot != null ? CurrentHoveredSlot.SlotType.ToString() : "NULL")}");
        dragController?.EndDrag();
    }

    public void SetPurposeIcon(Sprite sprite)
    {
        assignedPurposeIcon = sprite;
        Log($"SetPurposeIcon | slotType={SlotType} | icon={(sprite != null ? sprite.name : "NULL")}");
    }

    public ItemInstance GetBoundItem()
    {
        return binding?.GetItem();
    }

    public bool HasContainerItem()
    {
        ItemInstance item = GetBoundItem();
        return item != null && item.IsContainer && item.ContainerState != null;
    }

    private ItemInstance GetInstance()
    {
        return binding?.GetItem();
    }

    private string DescribeItem(ItemInstance item)
    {
        if (item == null)
            return "empty";

        string itemId = item.Definition != null ? item.Definition.ItemId : "NO_DEF";
        return $"{itemId} x{item.Quantity} inst={item.InstanceId}";
    }

    private bool TryGetCargoSnapshot(out CargoCrateStoredSnapshot snapshot)
    {
        snapshot = null;

        if (binding is StorageModuleSlotBinding storageBinding)
            return storageBinding.TryGetCargoSnapshot(out snapshot);

        return false;
    }

    private Sprite ResolveCargoIcon(CargoCrateStoredSnapshot snapshot)
    {
        if (snapshot == null)
            return cargoFallbackIcon;

        // For now, use the optional fallback icon.
        // We are intentionally not resolving TradeCargoPrefabCatalog here yet,
        // because InventorySlotUI is generic UI and should not know cargo catalogs forever.
        return cargoFallbackIcon;
    }

    private string FormatCargoShortLabel(CargoCrateStoredSnapshot snapshot)
    {
        if (snapshot == null)
            return "Cargo";

        if (string.IsNullOrWhiteSpace(snapshot.itemId))
            return "Cargo";

        string raw = snapshot.itemId.Replace("_", " ").Replace("-", " ").Trim();

        if (raw.Length == 0)
            return "Cargo";

        // Keep this short because it is going into tiny count text.
        return raw.Length <= 3
            ? raw.ToUpperInvariant()
            : char.ToUpperInvariant(raw[0]) + raw.Substring(1).ToLowerInvariant();
    }

    private bool isShowingInvalidTarget;

    public void SetInvalidTargetVisual(bool invalid)
    {
        isShowingInvalidTarget = invalid;

        if (background != null)
            background.color = invalid ? invalidTargetColor : normalColor;
    }

    public bool CanAcceptPreview(ItemInstance incoming)
    {
        if (incoming == null)
            return false;

        return binding != null && binding.CanAccept(incoming);
    }

    public void PlayInvalidTargetFeedback()
    {
        StopAllCoroutines();
        StartCoroutine(ShakeInvalidRoutine());
    }

    private IEnumerator ShakeInvalidRoutine()
    {
        RectTransform rt = RectTransform;
        if (rt == null)
            yield break;

        Vector2 original = rt.anchoredPosition;
        const float duration = 0.12f;
        const float magnitude = 8f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float x = Mathf.Sin(t * 24f) * magnitude * (1f - t);
            rt.anchoredPosition = original + new Vector2(x, 0f);
            yield return null;
        }

        rt.anchoredPosition = original;
    }

    public void ClearInvalidTargetVisual()
    {
        SetInvalidTargetVisual(false);
    }

    private bool TryQuickTransfer()
    {
        if (dragController != null && dragController.IsDragging)
            return false;

        if (binding == null)
            return false;

        ItemInstance current = binding.GetItem();
        if (current == null)
            return false;

        bool isExternalLikeSlot =
            binding is ExternalInventorySlotBinding ||
            binding is StorageModuleSlotBinding;

        // Case 1: player inventory/equipment/hotbar -> open external container/storage
        if (!isExternalLikeSlot)
        {
            ExternalContainerOverlayUI overlay = FindFirstObjectByType<ExternalContainerOverlayUI>();
            if (overlay == null || !overlay.IsOpen)
                return false;

            ItemInstance removed = binding.RemoveItem();
            if (removed == null)
                return false;

            bool inserted = false;
            ItemInstance remainder = removed;

            if (overlay.CurrentStorageModule != null)
            {
                inserted = ContainerPlacementUtility.TryAutoInsert(
                    overlay.CurrentStorageModule,
                    removed,
                    out remainder);
            }
            else if (overlay.CurrentContainer != null)
            {
                if (ReferenceEquals(overlay.CurrentContainer, current))
                {
                    binding.TryPlaceItem(removed, out _);
                    Refresh();
                    return false;
                }

                inserted = ContainerPlacementUtility.TryAutoInsert(
                    overlay.CurrentContainer,
                    removed,
                    out remainder);
            }

            if (inserted)
            {
                if (remainder != null && !remainder.IsDepleted())
                    binding.TryPlaceItem(remainder, out _);

                Refresh();
                return true;
            }

            // rollback
            binding.TryPlaceItem(removed, out _);
            Refresh();
            return false;
        }

        // Case 2: external container/storage -> player inventory
        PlayerInventory playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (playerInventory == null)
            return false;

        ItemInstance removedFromSource = binding.RemoveItem();
        if (removedFromSource == null)
            return false;

        if (playerInventory.TryAutoInsert(removedFromSource, out ItemInstance playerRemainder))
        {
            if (playerRemainder != null && !playerRemainder.IsDepleted())
                binding.TryPlaceItem(playerRemainder, out _);

            Refresh();
            return true;
        }

        // rollback
        binding.TryPlaceItem(removedFromSource, out _);
        Refresh();
        return false;
    }

    private void OnDisable()
    {
        if (dragController != null && dragController.IsDraggingFrom(this))
        {
            Log($"OnDisable | cancelling drag because this slot is active drag source | slotType={SlotType}");
            dragController.CancelDrag();
        }

        if (CurrentHoveredSlot == this)
            CurrentHoveredSlot = null;
    }

    private void Log(string msg)
    {
        if (!verboseLogging) return;
        Debug.Log($"[InventorySlotUI:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging) return;
        Debug.LogWarning($"[InventorySlotUI:{name}] {msg}", this);
    }
}