using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ContainerInventoryPanelUI : MonoBehaviour
{
    private enum ContainerSection
    {
        None = 0,
        Appendages,
        Hotbar,
        Equip
    }

    [Header("Section Roots")]
    [SerializeField] private RectTransform appendagesPanelRoot;
    [SerializeField] private RectTransform hotbarPanelRoot;
    [SerializeField] private RectTransform equipPanelRoot;

    [Header("Section Slot Parents")]
    [SerializeField] private RectTransform appendagesSlotParent;
    [SerializeField] private RectTransform hotbarSlotParent;
    [SerializeField] private RectTransform equipSlotParent;

    [Header("Section Grids")]
    [SerializeField] private GridLayoutGroup appendagesGrid;
    [SerializeField] private GridLayoutGroup hotbarGrid;
    [SerializeField] private GridLayoutGroup equipGrid;

    [Header("Shared Refs")]
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private InventoryDragController dragController;
    [SerializeField] private PlayerInventoryUI owner;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private readonly List<InventorySlotUI> spawnedSlots = new();

    [System.NonSerialized] private ItemInstance boundContainerItem;
    private BottomBarSlotType boundSourceSlotType = BottomBarSlotType.None;
    private bool isOpen;
    private ContainerSection activeSection = ContainerSection.None;

    public bool IsOpen => isOpen;
    public BottomBarSlotType BoundSourceSlotType => boundSourceSlotType;
    public ItemInstance BoundContainerItem => boundContainerItem;

    [System.NonSerialized] private ItemContainerState subscribedState;

    private void Awake()
    {
        HideImmediate();
    }

    public void ToggleForContainer(ItemInstance containerItem, BottomBarSlotType sourceSlotType)
    {
        if (isOpen && boundContainerItem == containerItem && boundSourceSlotType == sourceSlotType)
        {
            Log($"ToggleForContainer | closing existing panel for {sourceSlotType}");
            Hide();
            return;
        }

        Show(containerItem, sourceSlotType);
    }

    public void Show(ItemInstance containerItem, BottomBarSlotType sourceSlotType)
    {
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
        {
            LogWarning($"Show failed | item invalid or not a container | slotType={sourceSlotType}");
            Hide();
            return;
        }

        SubscribeTo(containerItem.ContainerState);

        ContainerSection section = ResolveSection(sourceSlotType);
        if (section == ContainerSection.None)
        {
            LogWarning($"Show failed | could not resolve section for slotType={sourceSlotType}");
            Hide();
            return;
        }

        RectTransform slotParent = GetSlotParent(section);
        GridLayoutGroup grid = GetGrid(section);
        RectTransform panelRoot = GetPanelRoot(section);

        if (slotParent == null || grid == null || panelRoot == null)
        {
            LogWarning($"Show failed | missing refs for section={section}");
            Hide();
            return;
        }

        HideAllSectionRoots();
        ClearSlots();

        boundContainerItem = containerItem;
        boundSourceSlotType = sourceSlotType;
        activeSection = section;
        isOpen = true;

        if (panelRoot != null)
            panelRoot.gameObject.SetActive(true);

        RebuildSlots(containerItem, slotParent, grid);
        RefreshAll();

        Log($"Show | section={section} | slotType={sourceSlotType} | item={containerItem.Definition.ItemId} | slotCount={containerItem.ContainerState.SlotCount} | columns={containerItem.ContainerState.ColumnCount}");
    }

    public void Hide()
    {
        isOpen = false;
        boundContainerItem = null;
        boundSourceSlotType = BottomBarSlotType.None;
        activeSection = ContainerSection.None;

        UnsubscribeFromCurrent();

        HideAllSectionRoots();
        ClearSlots();

        Log("Hide");
    }

    public void HideImmediate()
    {
        isOpen = false;
        boundContainerItem = null;
        boundSourceSlotType = BottomBarSlotType.None;
        activeSection = ContainerSection.None;

        HideAllSectionRoots();
        ClearSlots();
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
    }

    private void RebuildSlots(ItemInstance containerItem, RectTransform slotParent, GridLayoutGroup grid)
    {
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
        {
            LogWarning("RebuildSlots skipped because containerItem is null or not a valid container.");
            return;
        }

        ItemContainerState containerState = containerItem.ContainerState;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, containerState.ColumnCount);

        for (int i = 0; i < containerState.SlotCount; i++)
        {
            InventorySlotUI ui = Instantiate(slotPrefab, slotParent);
            ui.SetOwner(owner);
            ui.SetDragController(dragController);
            ui.Bind(new ContainerSlotBinding(containerItem, i));
            spawnedSlots.Add(ui);
        }

        Log($"RebuildSlots | activeSection={activeSection} | count={spawnedSlots.Count}");
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

    private void HideAllSectionRoots()
    {
        if (appendagesPanelRoot != null)
            appendagesPanelRoot.gameObject.SetActive(false);

        if (hotbarPanelRoot != null)
            hotbarPanelRoot.gameObject.SetActive(false);

        if (equipPanelRoot != null)
            equipPanelRoot.gameObject.SetActive(false);
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

    private ContainerSection ResolveSection(BottomBarSlotType slotType)
    {
        if (slotType == BottomBarSlotType.Hands ||
            slotType == BottomBarSlotType.Head ||
            slotType == BottomBarSlotType.Feet)
            return ContainerSection.Appendages;

        if (slotType >= BottomBarSlotType.Hotbar0 && slotType <= BottomBarSlotType.Hotbar7)
            return ContainerSection.Hotbar;

        if (slotType == BottomBarSlotType.Toolbelt ||
            slotType == BottomBarSlotType.Backpack ||
            slotType == BottomBarSlotType.Body)
            return ContainerSection.Equip;

        return ContainerSection.None;
    }

    private RectTransform GetPanelRoot(ContainerSection section)
    {
        return section switch
        {
            ContainerSection.Appendages => appendagesPanelRoot,
            ContainerSection.Hotbar => hotbarPanelRoot,
            ContainerSection.Equip => equipPanelRoot,
            _ => null
        };
    }

    private RectTransform GetSlotParent(ContainerSection section)
    {
        return section switch
        {
            ContainerSection.Appendages => appendagesSlotParent,
            ContainerSection.Hotbar => hotbarSlotParent,
            ContainerSection.Equip => equipSlotParent,
            _ => null
        };
    }

    private GridLayoutGroup GetGrid(ContainerSection section)
    {
        return section switch
        {
            ContainerSection.Appendages => appendagesGrid,
            ContainerSection.Hotbar => hotbarGrid,
            ContainerSection.Equip => equipGrid,
            _ => null
        };
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

    private void Log(string msg)
    {
        if (!verboseLogging) return;
        Debug.Log($"[ContainerInventoryPanelUI:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging) return;
        Debug.LogWarning($"[ContainerInventoryPanelUI:{name}] {msg}", this);
    }
}