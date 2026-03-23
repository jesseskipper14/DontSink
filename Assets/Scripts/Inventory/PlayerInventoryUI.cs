using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventoryUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private InventoryDragController dragController;

    [Header("Layout")]
    [SerializeField] private Transform appendagesGroup;
    [SerializeField] private Transform hotbarGroup;
    [SerializeField] private Transform bodyGroup;

    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private GameObject inventoryPanelRoot;
    [SerializeField] private ContainerInventoryPanelUI containerPanel;

    [Header("Runtime")]
    [SerializeField] private List<InventorySlotUI> allSlots = new();

    [Header("Purpose Icons")]
    [SerializeField] private Sprite handsSlotIcon;
    [SerializeField] private Sprite headSlotIcon;
    [SerializeField] private Sprite feetSlotIcon;
    [SerializeField] private Sprite toolbeltSlotIcon;
    [SerializeField] private Sprite backpackSlotIcon;
    [SerializeField] private Sprite bodySlotIcon;
    [SerializeField] private LoadoutContainerOverlayUI loadoutOverlay;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;


    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>(true);

        if (equipment == null)
            equipment = GetComponentInParent<PlayerEquipment>(true);

        if (dragController == null)
            dragController = GetComponentInParent<InventoryDragController>(true);

        if (containerPanel == null)
            containerPanel = GetComponentInChildren<ContainerInventoryPanelUI>(true);

        if (loadoutOverlay == null)
            loadoutOverlay = GetComponentInChildren<LoadoutContainerOverlayUI>(true);

        Log($"Awake | inventory={(inventory != null ? inventory.name : "NULL")} | equipment={(equipment != null ? equipment.name : "NULL")} | drag={(dragController != null ? dragController.name : "NULL")}");

        RebuildBottomBar();
        RefreshAll();
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.InventoryChanged += RefreshAll;
            inventory.SelectionChanged += RefreshAll;
        }

        if (equipment != null)
            equipment.EquipmentChanged += RefreshAll;

        Log("OnEnable | subscribed to events");
        RefreshAll();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.InventoryChanged -= RefreshAll;
            inventory.SelectionChanged -= RefreshAll;
        }

        if (equipment != null)
            equipment.EquipmentChanged -= RefreshAll;

        Log("OnDisable | unsubscribed from events");
    }

    public void ToggleInventoryPanel()
    {
        if (inventoryPanelRoot != null)
        {
            inventoryPanelRoot.SetActive(!inventoryPanelRoot.activeSelf);
            Log($"ToggleInventoryPanel | nowActive={inventoryPanelRoot.activeSelf}");
        }
    }

    public void HandleSlotClicked(InventorySlotUI slotUI)
    {
        if (slotUI == null || inventory == null)
        {
            LogWarning("HandleSlotClicked ignored because slotUI or inventory was null.");
            return;
        }

        Log($"HandleSlotClicked | slot={slotUI.name} | slotType={slotUI.SlotType} | isEquip={slotUI.IsEquipmentSlot} | hotbarIndex={slotUI.HotbarIndex}");

        if (slotUI.SupportsSelection)
            inventory.SetSelectedSlot(slotUI.SlotType);

        bool openAllActive = loadoutOverlay != null && loadoutOverlay.IsOpen;
        if (!openAllActive)
            TryToggleContainerPanel(slotUI);

        RefreshAll();
    }

    private void TryToggleContainerPanel(InventorySlotUI slotUI)
    {
        if (containerPanel == null || slotUI == null)
            return;

        ItemInstance item = slotUI.GetBoundItem();
        if (item == null || !item.IsContainer || item.ContainerState == null)
        {
            containerPanel.Hide();
            return;
        }

        containerPanel.ToggleForContainer(item, slotUI.SlotType);
    }

    public void RefreshAll()
    {
        Log($"RefreshAll | slotCount={allSlots.Count} | selected={(inventory != null ? inventory.SelectedSlot.ToString() : "NO_INVENTORY")}");

        for (int i = 0; i < allSlots.Count; i++)
        {
            InventorySlotUI ui = allSlots[i];
            if (ui == null)
                continue;

            ui.SetSelected(inventory != null && inventory.SelectedSlot == ui.SlotType);
            ui.Refresh();
        }

        RefreshContainerPanelState();
    }

    public IReadOnlyList<InventorySlotUI> GetAllVisibleLoadoutSlotUIs()
    {
        _visibleSlotsScratch.Clear();

        for (int i = 0; i < allSlots.Count; i++)
        {
            InventorySlotUI ui = allSlots[i];
            if (ui == null || !ui.isActiveAndEnabled || !ui.gameObject.activeInHierarchy)
                continue;

            _visibleSlotsScratch.Add(ui);
        }

        return _visibleSlotsScratch;
    }

    public bool IsInventoryPanelOpen => inventoryPanelRoot == null || inventoryPanelRoot.activeSelf;

    private readonly List<InventorySlotUI> _visibleSlotsScratch = new();

    private void RefreshContainerPanelState()
    {
        if (containerPanel == null || !containerPanel.IsOpen)
            return;

        BottomBarSlotType slotType = containerPanel.BoundSourceSlotType;
        ItemInstance current = GetCurrentItemForSlotType(slotType);

        if (current == null || !current.IsContainer || current.ContainerState == null)
        {
            containerPanel.Hide();
            return;
        }

        if (!ReferenceEquals(current, containerPanel.BoundContainerItem))
        {
            containerPanel.Show(current, slotType);
            return;
        }

        containerPanel.RefreshAll();
    }

    private InventorySlotUI FindSlotUI(BottomBarSlotType slotType)
    {
        for (int i = 0; i < allSlots.Count; i++)
        {
            InventorySlotUI ui = allSlots[i];
            if (ui == null)
                continue;

            if (ui.SlotType == slotType)
                return ui;
        }

        return null;
    }

    private void RebuildBottomBar()
    {
        Log("RebuildBottomBar BEGIN");
        ClearExisting();

        SpawnEquipmentSlot(appendagesGroup, BottomBarSlotType.Hands);
        SpawnEquipmentSlot(appendagesGroup, BottomBarSlotType.Feet);
        SpawnEquipmentSlot(appendagesGroup, BottomBarSlotType.Head);

        if (inventory == null)
        {
            LogError("RebuildBottomBar failed because inventory is NULL.");
            return;
        }

        for (int i = 0; i < inventory.HotbarSlotCount; i++)
            SpawnHotbarSlot(hotbarGroup, i);

        SpawnEquipmentSlot(bodyGroup, BottomBarSlotType.Toolbelt);
        SpawnEquipmentSlot(bodyGroup, BottomBarSlotType.Body);
        SpawnEquipmentSlot(bodyGroup, BottomBarSlotType.Backpack);

        Log($"RebuildBottomBar END | builtSlots={allSlots.Count}");
    }

    private void SpawnHotbarSlot(Transform parent, int hotbarIndex)
    {
        InventorySlotUI ui = Instantiate(slotPrefab, parent);
        ui.SetOwner(this);
        ui.SetDragController(dragController);
        ui.Bind(new HotbarSlotBinding(inventory, hotbarIndex));
        allSlots.Add(ui);

        Log($"SpawnHotbarSlot | index={hotbarIndex} | ui={ui.name} | parent={(parent != null ? parent.name : "NULL")}");
    }

    private void SpawnEquipmentSlot(Transform parent, BottomBarSlotType slotType)
    {
        InventorySlotUI ui = Instantiate(slotPrefab, parent);
        ui.SetOwner(this);
        ui.SetDragController(dragController);
        ui.SetPurposeIcon(GetPurposeIcon(slotType));
        ui.Bind(new EquipmentSlotBinding(equipment, slotType));
        allSlots.Add(ui);

        Log($"SpawnEquipmentSlot | slotType={slotType} | ui={ui.name} | parent={(parent != null ? parent.name : "NULL")}");
    }

    private void ClearExisting()
    {
        for (int i = 0; i < allSlots.Count; i++)
        {
            if (allSlots[i] != null)
                Destroy(allSlots[i].gameObject);
        }

        allSlots.Clear();
        Log("ClearExisting | cleared all slot UI");
    }

    private Sprite GetPurposeIcon(BottomBarSlotType slotType)
    {
        return slotType switch
        {
            BottomBarSlotType.Hands => handsSlotIcon,
            BottomBarSlotType.Head => headSlotIcon,
            BottomBarSlotType.Feet => feetSlotIcon,
            BottomBarSlotType.Toolbelt => toolbeltSlotIcon,
            BottomBarSlotType.Backpack => backpackSlotIcon,
            BottomBarSlotType.Body => bodySlotIcon,
            _ => null
        };
    }

    private ItemInstance GetCurrentItemForSlotType(BottomBarSlotType slotType)
    {
        if (slotType >= BottomBarSlotType.Hotbar0 && slotType <= BottomBarSlotType.Hotbar7)
        {
            int hotbarIndex = PlayerInventory.SlotTypeToHotbarIndex(slotType);
            return inventory != null ? inventory.GetSlot(hotbarIndex)?.Instance : null;
        }

        return equipment != null ? equipment.Get(slotType) : null;
    }

    public void HideSingleContainerPanel()
    {
        if (containerPanel != null)
            containerPanel.Hide();
    }

    public void RefreshDragPreview(ItemInstance draggedItem)
    {
        for (int i = 0; i < allSlots.Count; i++)
        {
            InventorySlotUI ui = allSlots[i];
            if (ui == null)
                continue;

            bool invalid = false;

            if (draggedItem != null)
                invalid = !ui.CanAcceptPreview(draggedItem);

            ui.SetInvalidTargetVisual(invalid);
        }

        if (containerPanel != null && containerPanel.IsOpen)
            containerPanel.RefreshDragPreview(draggedItem);
    }

    private void ClearDragPreview()
    {
        for (int i = 0; i < allSlots.Count; i++)
        {
            if (allSlots[i] != null)
                allSlots[i].ClearInvalidTargetVisual();
        }
    }

    private void Log(string msg)
    {
        if (!verboseLogging) return;
        Debug.Log($"[PlayerInventoryUI:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging) return;
        Debug.LogWarning($"[PlayerInventoryUI:{name}] {msg}", this);
    }

    private void LogError(string msg)
    {
        Debug.LogError($"[PlayerInventoryUI:{name}] {msg}", this);
    }
}