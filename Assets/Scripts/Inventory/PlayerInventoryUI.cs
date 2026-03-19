using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private InventoryDragController dragController;

    [SerializeField] private Transform appendagesGroup;
    [SerializeField] private Transform hotbarGroup;
    [SerializeField] private Transform bodyGroup;

    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private GameObject inventoryPanelRoot;

    [SerializeField] private List<InventorySlotUI> allSlots = new();

    [SerializeField] private Sprite handsSlotIcon;
    [SerializeField] private Sprite headSlotIcon;
    [SerializeField] private Sprite feetSlotIcon;
    [SerializeField] private Sprite toolbeltSlotIcon;
    [SerializeField] private Sprite backpackSlotIcon;
    [SerializeField] private Sprite bodySlotIcon;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>(true);

        if (equipment == null)
            equipment = GetComponentInParent<PlayerEquipment>(true);

        if (dragController == null)
            dragController = GetComponentInParent<InventoryDragController>(true);

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
    }

    public void ToggleInventoryPanel()
    {
        if (inventoryPanelRoot != null)
            inventoryPanelRoot.SetActive(!inventoryPanelRoot.activeSelf);
    }

    public void HandleSlotClicked(InventorySlotUI slotUI)
    {
        if (slotUI == null || inventory == null)
            return;

        inventory.SetSelectedSlot(slotUI.SlotType);
        RefreshAll();
    }

    public void RefreshAll()
    {
        for (int i = 0; i < allSlots.Count; i++)
        {
            InventorySlotUI ui = allSlots[i];
            if (ui == null)
                continue;

            ui.SetSelected(inventory != null && inventory.SelectedSlot == ui.SlotType);
            ui.Refresh();
        }
    }

    private void RebuildBottomBar()
    {
        ClearExisting();

        SpawnEquipmentSlot(appendagesGroup, BottomBarSlotType.Hands);
        SpawnEquipmentSlot(appendagesGroup, BottomBarSlotType.Head);
        SpawnEquipmentSlot(appendagesGroup, BottomBarSlotType.Feet);

        for (int i = 0; i < inventory.HotbarSlotCount; i++)
            SpawnHotbarSlot(hotbarGroup, i);

        SpawnEquipmentSlot(bodyGroup, BottomBarSlotType.Toolbelt);
        SpawnEquipmentSlot(bodyGroup, BottomBarSlotType.Backpack);
        SpawnEquipmentSlot(bodyGroup, BottomBarSlotType.Body);
    }

    private void SpawnHotbarSlot(Transform parent, int hotbarIndex)
    {
        InventorySlotUI ui = Instantiate(slotPrefab, parent);
        ui.SetOwner(this);
        ui.SetDragController(dragController);
        ui.BindInventory(inventory, hotbarIndex);
        allSlots.Add(ui);
    }

    private void SpawnEquipmentSlot(Transform parent, BottomBarSlotType slotType)
    {
        InventorySlotUI ui = Instantiate(slotPrefab, parent);
        ui.SetOwner(this);
        ui.SetDragController(dragController);
        ui.SetPurposeIcon(GetPurposeIcon(slotType));
        ui.BindEquipment(equipment, slotType);
        allSlots.Add(ui);
    }

    private void ClearExisting()
    {
        for (int i = 0; i < allSlots.Count; i++)
        {
            if (allSlots[i] != null)
                Destroy(allSlots[i].gameObject);
        }

        allSlots.Clear();
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
}