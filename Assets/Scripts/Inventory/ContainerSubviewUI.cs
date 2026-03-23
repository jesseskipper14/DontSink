using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ContainerSubviewUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform slotParent;
    [SerializeField] private GridLayoutGroup grid;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private InventoryDragController dragController;
    [SerializeField] private PlayerInventoryUI owner;

    private readonly List<InventorySlotUI> spawnedSlots = new();

    private ItemInstance boundContainerItem;
    private InventorySlotUI sourceSlot;
    private ItemContainerState subscribedState;

    public InventorySlotUI SourceSlot => sourceSlot;

    private void Awake()
    {
        if (root == null)
            root = transform as RectTransform;
    }

    public void Bind(
        ItemInstance containerItem,
        InventorySlotUI source,
        PlayerInventoryUI ownerUI,
        InventoryDragController drag)
    {
        boundContainerItem = containerItem;
        sourceSlot = source;
        owner = ownerUI;
        dragController = drag;

        bool valid =
            boundContainerItem != null &&
            boundContainerItem.IsContainer &&
            boundContainerItem.ContainerState != null &&
            root != null &&
            slotParent != null &&
            grid != null &&
            slotPrefab != null &&
            owner != null &&
            dragController != null;

        if (!valid)
        {
            Debug.LogWarning(
                $"[ContainerSubviewUI:{name}] Bind invalid | " +
                $"item={(boundContainerItem != null ? boundContainerItem.Definition.ItemId : "NULL")} | " +
                $"isContainer={(boundContainerItem != null && boundContainerItem.IsContainer)} | " +
                $"state={(boundContainerItem != null && boundContainerItem.ContainerState != null)} | " +
                $"root={(root != null)} | slotParent={(slotParent != null)} | grid={(grid != null)} | " +
                $"slotPrefab={(slotPrefab != null)} | owner={(owner != null)} | drag={(dragController != null)}",
                this);

            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        SubscribeTo(boundContainerItem.ContainerState);
        RebuildSlots();
        RefreshAll();
    }

    public void SetAnchoredPosition(Vector2 pos)
    {
        if (root != null)
            root.anchoredPosition = pos;
    }

    public Vector2 GetSize()
    {
        return root != null ? root.rect.size : Vector2.zero;
    }

    public void RefreshAll()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            InventorySlotUI ui = spawnedSlots[i];
            if (ui == null)
                continue;

            ui.SetSelected(false);
            ui.Refresh();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private void RebuildSlots()
    {
        ClearSlots();

        if (boundContainerItem == null || boundContainerItem.ContainerState == null)
            return;

        ItemContainerState state = boundContainerItem.ContainerState;

        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, state.ColumnCount);
        }

        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlotUI ui = Instantiate(slotPrefab, slotParent);
            ui.SetOwner(owner);
            ui.SetDragController(dragController);
            ui.Bind(new ContainerSlotBinding(boundContainerItem, i));
            spawnedSlots.Add(ui);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    public Vector2 GetAnchoredPosition()
    {
        return root != null ? root.anchoredPosition : Vector2.zero;
    }

    public void RefreshDragPreview(ItemInstance draggedItem)
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            InventorySlotUI ui = spawnedSlots[i];
            if (ui == null)
                continue;

            bool invalid = draggedItem != null && !ui.CanAcceptPreview(draggedItem);
            ui.SetInvalidTargetVisual(invalid);
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null)
                Destroy(spawnedSlots[i].gameObject);
        }

        spawnedSlots.Clear();
    }

    private void SubscribeTo(ItemContainerState state)
    {
        UnsubscribeFromCurrent();
        subscribedState = state;

        if (subscribedState != null)
            subscribedState.Changed += RefreshAll;
    }

    private void UnsubscribeFromCurrent()
    {
        if (subscribedState != null)
            subscribedState.Changed -= RefreshAll;

        subscribedState = null;
    }

    private void OnDestroy()
    {
        UnsubscribeFromCurrent();
    }
}