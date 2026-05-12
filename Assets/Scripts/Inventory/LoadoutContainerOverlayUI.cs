using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LoadoutContainerOverlayUI : MonoBehaviour, IEscapeClosable
{
    [Header("Refs")]
    [SerializeField] private PlayerInventoryUI playerInventoryUI;
    [SerializeField] private RectTransform appendagesNestedRoot;
    [SerializeField] private RectTransform hotbarNestedRoot;
    [SerializeField] private RectTransform equipNestedRoot;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera uiCamera;
    [SerializeField] private ContainerSubviewUI subviewPrefab;
    [SerializeField] private InventoryDragController dragController;

    [Header("Escape Routing")]
    [SerializeField] private bool closeViaGlobalEscapeRouter = true;
    [SerializeField] private int escapePriority = 350;

    public int EscapePriority => escapePriority;
    public bool IsEscapeOpen => IsOpen;

    public bool IsOpen => activeSubviews.Count > 0;

    [Header("Layout")]
    [SerializeField] private Vector2 defaultOffset = Vector2.zero;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private readonly List<ContainerSubviewUI> activeSubviews = new();

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (dragController == null)
            dragController = GetComponentInParent<InventoryDragController>(true);

        if (playerInventoryUI == null)
            playerInventoryUI = GetComponentInParent<PlayerInventoryUI>(true);
    }

    private void OnDisable()
    {
        UnregisterFromEscape();
    }

    public void ToggleAll()
    {
        if (activeSubviews.Count > 0)
            CloseAll();
        else
            OpenAll();
    }

    public void OpenAll()
    {
        CloseAll();

        if (playerInventoryUI == null || subviewPrefab == null)
        {
            LogWarning("OpenAll aborted due to missing references.");
            return;
        }

        IReadOnlyList<InventorySlotUI> slots = playerInventoryUI.GetAllVisibleLoadoutSlotUIs();
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotUI slot = slots[i];
            if (slot == null)
                continue;

            ItemInstance item = slot.GetBoundItem();
            if (item == null || !item.IsContainer || item.ContainerState == null)
                continue;

            RectTransform parentRoot = ResolveNestedRoot(slot.SlotType);
            if (parentRoot == null)
            {
                LogWarning($"OpenAll skipped slot {slot.DebugSlotName} because no nested root matched slotType={slot.SlotType}");
                continue;
            }

            CreateSubview(parentRoot, slot, item);
        }

        if (playerInventoryUI != null)
            playerInventoryUI.HideSingleContainerPanel();

        if (IsOpen)
            RegisterWithEscape();

        Log($"OpenAll | created={activeSubviews.Count}");
    }

    public void CloseAll()
    {
        if (dragController != null && dragController.IsDragging)
            dragController.CancelDrag();

        for (int i = 0; i < activeSubviews.Count; i++)
        {
            if (activeSubviews[i] != null)
                Destroy(activeSubviews[i].gameObject);
        }

        activeSubviews.Clear();

        UnregisterFromEscape();

        Log("CloseAll");
    }

    public bool CloseFromEscape()
    {
        if (!IsOpen)
            return false;

        CloseAll();
        return true;
    }

    public void RefreshPositions()
    {
        for (int i = 0; i < activeSubviews.Count; i++)
        {
            ContainerSubviewUI subview = activeSubviews[i];
            if (subview == null || subview.SourceSlot == null)
                continue;

            RectTransform parentRoot = ResolveNestedRoot(subview.SourceSlot.SlotType);
            if (parentRoot == null)
                continue;

            PositionSubview(subview, subview.SourceSlot, parentRoot);
        }
    }

    private void CreateSubview(RectTransform parentRoot, InventorySlotUI sourceSlot, ItemInstance item)
    {
        ContainerSubviewUI subview = Instantiate(subviewPrefab, parentRoot);
        subview.Bind(item, sourceSlot, playerInventoryUI, dragController);
        PositionSubview(subview, sourceSlot, parentRoot);
        activeSubviews.Add(subview);

        Log($"CreateSubview | source={sourceSlot.DebugSlotName} | item={item.Definition.ItemId} | parentRoot={parentRoot.name}");
    }

    private void PositionSubview(ContainerSubviewUI subview, InventorySlotUI sourceSlot, RectTransform parentRoot)
    {
        RectTransform slotRect = sourceSlot.RectTransform;
        if (slotRect == null || parentRoot == null)
            return;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(GetEventCamera(), slotRect.position);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRoot,
                screenPoint,
                GetEventCamera(),
                out Vector2 localPoint))
            return;

        Vector2 anchored = subview.GetAnchoredPosition();
        anchored.x = localPoint.x;
        anchored.y = 0f;

        subview.SetAnchoredPosition(anchored + defaultOffset);
    }

    private RectTransform ResolveNestedRoot(BottomBarSlotType slotType)
    {
        if (slotType == BottomBarSlotType.Hands ||
            slotType == BottomBarSlotType.Head ||
            slotType == BottomBarSlotType.Feet)
            return appendagesNestedRoot;

        if (slotType >= BottomBarSlotType.Hotbar0 &&
            slotType <= BottomBarSlotType.Hotbar7)
            return hotbarNestedRoot;

        if (slotType == BottomBarSlotType.Toolbelt ||
            slotType == BottomBarSlotType.Backpack ||
            slotType == BottomBarSlotType.Body)
            return equipNestedRoot;

        return null;
    }

    private Camera GetEventCamera()
    {
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return uiCamera != null ? uiCamera : canvas.worldCamera;

        return null;
    }

    public void RefreshDragPreview(ItemInstance draggedItem)
    {
        for (int i = 0; i < activeSubviews.Count; i++)
        {
            ContainerSubviewUI subview = activeSubviews[i];
            if (subview == null)
                continue;

            subview.RefreshDragPreview(draggedItem);
        }
    }

    private void RegisterWithEscape()
    {
        if (!closeViaGlobalEscapeRouter)
            return;

        EscapeCloseRegistry registry = EscapeCloseRegistry.GetOrFind();
        if (registry != null)
            registry.Register(this);
    }

    private void UnregisterFromEscape()
    {
        if (EscapeCloseRegistry.I != null)
            EscapeCloseRegistry.I.Unregister(this);
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[LoadoutContainerOverlayUI:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[LoadoutContainerOverlayUI:{name}] {msg}", this);
    }
}