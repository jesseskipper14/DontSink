using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryDragController : MonoBehaviour
{
    [SerializeField] private Image dragIcon;
    [SerializeField] private Text dragCount;
    [SerializeField] private Canvas canvas;

    private ItemInstance draggedItem;
    private InventorySlotUI sourceSlot;
    private bool isDragging;

    private void Awake()
    {
        HideVisual();
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
            return;

        ItemInstance item = slot.RemoveItem();
        if (item == null || item.Definition == null)
            return;

        draggedItem = item;
        sourceSlot = slot;
        isDragging = true;

        ShowVisual(item);
    }

    public void EndDrag()
    {
        if (!isDragging)
            return;

        InventorySlotUI target = InventorySlotUI.CurrentHoveredSlot;
        ItemInstance working = draggedItem;

        if (target != null)
        {
            if (target.TryPlaceItem(working, out ItemInstance displaced))
            {
                if (displaced == null || displaced.IsDepleted())
                {
                    Cleanup();
                    return;
                }

                if (sourceSlot != null && sourceSlot != target)
                {
                    if (sourceSlot.TryPlaceItem(displaced, out ItemInstance returned))
                    {
                        if (returned == null || returned.IsDepleted())
                        {
                            Cleanup();
                            return;
                        }

                        target.TryPlaceItem(returned, out _);
                        Cleanup();
                        return;
                    }
                }
            }
        }

        if (sourceSlot != null)
            sourceSlot.TryPlaceItem(working, out _);

        Cleanup();
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
        draggedItem = null;
        sourceSlot = null;
        isDragging = false;
        HideVisual();
    }
}