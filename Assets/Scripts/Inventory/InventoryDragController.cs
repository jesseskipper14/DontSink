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

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private ItemInstance draggedItem;
    private InventorySlotUI sourceSlot;
    private bool isDragging;

    private readonly List<RaycastResult> _raycastResults = new();

    private void Awake()
    {
        if (displacedItemResolver == null)
            displacedItemResolver = GetComponentInParent<DisplacedItemResolver>(true);

        HideVisual();
        Log($"Awake | canvas={(canvas != null ? canvas.name : "NULL")} | dragIcon={(dragIcon != null ? dragIcon.name : "NULL")}");
    }

    private void Update()
    {
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
            }
        }
        else
        {
            Log("EndDrag | no target under mouse, returning item to source.");
        }

        if (sourceSlot != null)
        {
            sourceSlot.TryPlaceItem(working, out _);
            Log($"EndDrag | returned item to source | source={DescribeSlot(sourceSlot)} | item={DescribeItem(working)}");
        }

        Cleanup();
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