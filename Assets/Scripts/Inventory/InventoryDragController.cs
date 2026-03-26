using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryDragController : MonoBehaviour
{
    [SerializeField] private Image dragIcon;
    [SerializeField] private Text dragCount;
    [SerializeField] private Canvas canvas;
    [SerializeField] private DisplacedItemResolver displacedItemResolver;
    [SerializeField] private PlayerInventoryUI playerInventoryUI;
    [SerializeField] private LoadoutContainerOverlayUI loadoutOverlayUI;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerInventoryInput inventoryInput;

    [Header("World Drop Targets")]
    [SerializeField] private LayerMask worldDropTargetMask = ~0;
    [SerializeField] private float worldDropTargetRadius = 0.2f;
    [SerializeField] private Camera worldCamera;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private ItemInstance draggedItem;
    private InventorySlotUI sourceSlot;
    private bool isDragging;

    private readonly List<RaycastResult> _raycastResults = new();

    public bool IsDragging => isDragging;
    public ItemInstance DraggedItem => draggedItem;

    private void Awake()
    {
        if (displacedItemResolver == null)
            displacedItemResolver = GetComponentInParent<DisplacedItemResolver>(true);

        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>(true);

        if (inventoryInput == null)
            inventoryInput = GetComponentInParent<PlayerInventoryInput>(true);

        if (worldCamera == null)
            worldCamera = Camera.main;

        HideVisual();
        Log($"Awake | canvas={(canvas != null ? canvas.name : "NULL")} | dragIcon={(dragIcon != null ? dragIcon.name : "NULL")}");
    }

    private void Update()
    {
        if (!isDragging)
        {
            if (dragIcon != null && dragIcon.enabled)
                HideVisual();

            return;
        }

        if (!isDragging || dragIcon == null || canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            canvas.worldCamera,
            out Vector2 localPoint);

        dragIcon.rectTransform.anchoredPosition = localPoint;

        if (dragCount != null)
            dragCount.rectTransform.anchoredPosition = localPoint;

        if (isDragging)
        {
            playerInventoryUI?.RefreshDragPreview(draggedItem);
            loadoutOverlayUI?.RefreshDragPreview(draggedItem);
        }
        else
        {
            playerInventoryUI?.RefreshDragPreview(null);
            loadoutOverlayUI?.RefreshDragPreview(null);
        }

        if (Input.GetMouseButtonDown(1))
        {
            InventorySlotUI target = FindTargetSlotUnderMouse();

            if (target != null)
            {
                bool deposited = TryDepositSingleInto(target);
                if (deposited)
                    target.Refresh();
            }
            else
            {
                TryDropSingleToWorld();
            }
        }
    }

    public void BeginDrag(InventorySlotUI slot)
    {
        if (slot == null)
        {
            LogWarning("BeginDrag ignored because slot was null.");
            return;
        }

        ItemInstance item = slot.RemoveItem();
        if (item == null || item.Definition == null)
        {
            LogWarning($"BeginDrag failed | source={DescribeSlot(slot)} | removed item was null/invalid");
            return;
        }

        draggedItem = item;
        sourceSlot = slot;
        isDragging = true;

        ShowVisual(item);

        Log($"BeginDrag | source={DescribeSlot(sourceSlot)} | item={DescribeItem(draggedItem)}");
    }

    public void BeginDrag(ItemInstance item, InventorySlotUI slot)
    {
        if (item == null || item.Definition == null)
        {
            LogWarning("BeginDrag(item, slot) failed because item was null/invalid.");
            return;
        }

        if (isDragging)
        {
            LogWarning("BeginDrag(item, slot) ignored because drag is already active.");
            return;
        }

        draggedItem = item;
        sourceSlot = slot;
        isDragging = true;

        ShowVisual(item);

        Log($"BeginDrag(item, slot) | source={DescribeSlot(sourceSlot)} | item={DescribeItem(draggedItem)}");
    }

    public void EndDrag()
    {
        if (!isDragging)
        {
            Log("EndDrag ignored because not dragging.");
            return;
        }

        InventorySlotUI target = FindTargetSlotUnderMouse();
        ItemInstance working = draggedItem;

        Log($"EndDrag BEGIN | source={DescribeSlot(sourceSlot)} | target={DescribeSlot(target)} | item={DescribeItem(working)}");

        if (target != null)
        {
            if (target.TryPlaceItem(working, out ItemInstance displaced))
            {
                Log($"EndDrag | target accepted item | target={DescribeSlot(target)} | displaced={DescribeItem(displaced)}");

                if (displaced == null || displaced.IsDepleted())
                {
                    Cleanup();
                    return;
                }

                bool resolved = false;

                if (sourceSlot != null && sourceSlot != target)
                {
                    if (sourceSlot.TryPlaceItem(displaced, out ItemInstance returned))
                    {
                        Log($"EndDrag | source accepted displaced item back | source={DescribeSlot(sourceSlot)} | returned={DescribeItem(returned)}");

                        if (returned == null || returned.IsDepleted())
                        {
                            Cleanup();
                            return;
                        }

                        displaced = returned;
                    }
                }

                if (displacedItemResolver != null)
                    resolved = displacedItemResolver.TryResolve(displaced, sourceSlot);

                if (resolved)
                {
                    Cleanup();
                    return;
                }

                // Full rollback if displaced item could not be resolved anywhere.
                LogWarning("EndDrag | displaced item could not be resolved. Rolling back swap.");

                ItemInstance rolledBackWorking = target.RemoveItem();
                if (rolledBackWorking == null)
                    rolledBackWorking = working;

                bool targetRestored = target.TryPlaceItem(displaced, out ItemInstance targetRestoreRemainder);
                bool sourceRestored = sourceSlot != null && sourceSlot.TryPlaceItem(rolledBackWorking, out ItemInstance sourceRestoreRemainder);

                //LogWarning($"EndDrag rollback | targetRestored={targetRestored} remainder={DescribeItem(targetRestoreRemainder)} | sourceRestored={sourceRestored} remainder={DescribeItem(sourceRestoreRemainder)}");

                Cleanup();
                return;
            }
            else
            {
                LogWarning($"EndDrag | target rejected item | target={DescribeSlot(target)} | item={DescribeItem(working)}");
                target.PlayInvalidTargetFeedback();
            }
        }
        else
        {
            Log("EndDrag | no UI target under mouse, trying world container target.");

            if (TryDepositDraggedItemIntoWorldTarget(working, out ItemInstance remainder))
            {
                if (remainder == null || remainder.IsDepleted())
                {
                    Cleanup();
                    return;
                }

                draggedItem = remainder;
                ShowVisual(draggedItem);
                Log($"EndDrag | partial deposit into world target | remainder={DescribeItem(draggedItem)}");
                return;
            }

            Log("EndDrag | no valid world container target, dropping dragged item into world.");

            if (TryDropItemToWorld(working))
            {
                Cleanup();
                return;
            }

            LogWarning("EndDrag | world drop failed, returning item to source.");
        }

        if (sourceSlot != null)
        {
            sourceSlot.TryPlaceItem(working, out _);
            Log($"EndDrag | returned item to source | source={DescribeSlot(sourceSlot)} | item={DescribeItem(working)}");
        }

        Cleanup();
    }

    public bool IsDraggingFrom(InventorySlotUI slot)
    {
        return isDragging && sourceSlot == slot;
    }

    private InventorySlotUI FindTargetSlotUnderMouse()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            LogWarning("FindTargetSlotUnderMouse failed because EventSystem.current is null.");
            return null;
        }

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        _raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, _raycastResults);

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            GameObject go = _raycastResults[i].gameObject;
            if (go == null)
                continue;

            InventorySlotUI slot = go.GetComponentInParent<InventorySlotUI>();
            if (slot != null)
            {
                Log($"FindTargetSlotUnderMouse | hit={go.name} | resolvedSlot={DescribeSlot(slot)}");
                return slot;
            }
        }

        Log("FindTargetSlotUnderMouse | no InventorySlotUI hit.");
        return null;
    }

    private void ShowVisual(ItemInstance item)
    {
        if (dragIcon != null)
        {
            dragIcon.enabled = true;
            dragIcon.sprite = item.Definition != null ? item.Definition.Icon : null;
        }

        if (dragCount != null)
        {
            dragCount.gameObject.SetActive(item.Quantity > 1);
            dragCount.text = item.Quantity > 1 ? item.Quantity.ToString() : "";
        }
    }

    private void HideVisual()
    {
        if (dragIcon != null)
            dragIcon.enabled = false;

        if (dragCount != null)
        {
            dragCount.text = "";
            dragCount.gameObject.SetActive(false);
        }
    }

    private void Cleanup()
    {
        Log($"Cleanup | item={DescribeItem(draggedItem)} | source={DescribeSlot(sourceSlot)}");

        draggedItem = null;
        sourceSlot = null;
        isDragging = false;
        HideVisual();

        playerInventoryUI?.RefreshDragPreview(null);
        loadoutOverlayUI?.RefreshDragPreview(null);
    }

    private string DescribeSlot(InventorySlotUI slot)
    {
        if (slot == null)
            return "NULL";

        return $"{slot.name} type={slot.SlotType} equip={slot.IsEquipmentSlot} hotbarIndex={slot.HotbarIndex}";
    }

    private string DescribeItem(ItemInstance item)
    {
        if (item == null)
            return "empty";

        string itemId = item.Definition != null ? item.Definition.ItemId : "NO_DEF";
        return $"{itemId} x{item.Quantity} inst={item.InstanceId}";
    }

    public bool TryDepositSingleInto(InventorySlotUI target)
    {
        if (!isDragging || draggedItem == null || target == null)
            return false;

        if (draggedItem.Quantity <= 0)
            return false;

        if (draggedItem.Quantity == 1)
        {
            if (target.TryPlaceItem(draggedItem, out ItemInstance displaced))
            {
                if (displaced != null && !displaced.IsDepleted())
                {
                    LogWarning("TryDepositSingleInto | target displaced item unexpectedly during single final place.");
                    return false;
                }

                Cleanup();
                return true;
            }

            return false;
        }

        ItemInstance singleToPlace = draggedItem.SplitOff(1);
        if (singleToPlace == null)
            return false;

        if (target.TryPlaceItem(singleToPlace, out ItemInstance displacedRemainder))
        {
            if (displacedRemainder != null && !displacedRemainder.IsDepleted())
            {
                draggedItem.AddQuantity(1);
                return false;
            }

            ShowVisual(draggedItem);
            return true;
        }

        draggedItem.AddQuantity(1);
        return false;
    }

    public void CancelDrag()
    {
        if (!isDragging && draggedItem == null)
        {
            HideVisual();
            playerInventoryUI?.RefreshDragPreview(null);
            return;
        }

        Log($"CancelDrag | item={DescribeItem(draggedItem)} | source={DescribeSlot(sourceSlot)}");

        bool restored = false;

        if (draggedItem != null && sourceSlot != null && sourceSlot.isActiveAndEnabled && sourceSlot.gameObject.activeInHierarchy)
        {
            if (sourceSlot.TryPlaceItem(draggedItem, out ItemInstance displaced))
            {
                if (displaced == null || displaced.IsDepleted())
                    restored = true;
            }
        }

        if (!restored && draggedItem != null && displacedItemResolver != null)
            restored = displacedItemResolver.TryResolve(draggedItem, sourceSlot);

        if (!restored && draggedItem != null)
            LogWarning($"CancelDrag | failed to restore dragged item cleanly | item={DescribeItem(draggedItem)}");

        Cleanup();
    }

    private bool TryDropItemToWorld(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        if (inventory == null)
        {
            LogWarning($"TryDropItemToWorld failed because inventory is null | item={DescribeItem(item)}");
            return false;
        }

        Vector3 worldPos = inventoryInput != null
            ? inventoryInput.GetDropWorldPositionForUI()
            : transform.position;

        bool ok = inventory.TryDropInstance(item, worldPos);
        Log($"TryDropItemToWorld | item={DescribeItem(item)} | pos={worldPos} | ok={ok}");
        return ok;
    }

    private bool TryDropSingleToWorld()
    {
        if (!isDragging || draggedItem == null)
            return false;

        if (draggedItem.Quantity == 1)
        {
            if (TryDropItemToWorld(draggedItem))
            {
                Cleanup();
                return true;
            }

            return false;
        }

        ItemInstance single = draggedItem.SplitOff(1);
        if (single == null)
            return false;

        if (TryDropItemToWorld(single))
        {
            ShowVisual(draggedItem);
            return true;
        }

        draggedItem.AddQuantity(1);
        return false;
    }

    private bool TryGetWorldDropTargetUnderCursor(out IWorldItemDropTarget target)
    {
        target = null;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
        {
            LogWarning("TryGetWorldDropTargetUnderCursor failed because no camera was available.");
            return false;
        }

        Vector3 mouse = Input.mousePosition;
        Vector3 world = cam.ScreenToWorldPoint(mouse);
        Vector2 point = new Vector2(world.x, world.y);

        Collider2D[] hits = Physics2D.OverlapCircleAll(point, worldDropTargetRadius, worldDropTargetMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null)
                continue;

            target = col.GetComponent<IWorldItemDropTarget>();
            if (target != null)
            {
                Log($"TryGetWorldDropTargetUnderCursor | hit={col.name} | direct target found");
                return true;
            }

            target = col.GetComponentInParent<IWorldItemDropTarget>();
            if (target != null)
            {
                Log($"TryGetWorldDropTargetUnderCursor | hit={col.name} | parent target found");
                return true;
            }
        }

        Log("TryGetWorldDropTargetUnderCursor | no world drop target hit.");
        return false;
    }

    private bool TryDepositDraggedItemIntoWorldTarget(ItemInstance item, out ItemInstance remainder)
    {
        remainder = item;

        if (item == null)
            return false;

        if (!TryGetWorldDropTargetUnderCursor(out IWorldItemDropTarget target))
            return false;

        if (!target.CanAcceptWorldDrop(item))
        {
            Log($"TryDepositDraggedItemIntoWorldTarget | target rejected preview | item={DescribeItem(item)}");
            return false;
        }

        bool ok = target.TryAcceptWorldDrop(item, out remainder);
        Log($"TryDepositDraggedItemIntoWorldTarget | item={DescribeItem(item)} | ok={ok} | remainder={DescribeItem(remainder)}");
        return ok;
    }

    private void Log(string msg)
    {
        if (!verboseLogging) return;
        Debug.Log($"[InventoryDragController:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging) return;
        Debug.LogWarning($"[InventoryDragController:{name}] {msg}", this);
    }
}