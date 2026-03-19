using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventorySlotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public static InventorySlotUI CurrentHoveredSlot { get; private set; }

    [SerializeField] private Image icon;
    [SerializeField] private Text countText;
    [SerializeField] private GameObject selectionHighlight;
    [SerializeField] private Image purposeIcon;
    private Sprite assignedPurposeIcon;

    private PlayerInventory boundInventory;
    private PlayerEquipment boundEquipment;
    private BottomBarSlotType boundSlotType = BottomBarSlotType.None;
    private int boundHotbarIndex = -1;
    private bool isEquipmentSlot;

    private PlayerInventoryUI owner;
    private InventoryDragController dragController;

    public bool IsEquipmentSlot => isEquipmentSlot;
    public int HotbarIndex => boundHotbarIndex;
    public BottomBarSlotType SlotType => isEquipmentSlot ? boundSlotType : PlayerInventory.HotbarIndexToSlotType(boundHotbarIndex);

    public void SetOwner(PlayerInventoryUI ownerUI)
    {
        owner = ownerUI;
    }

    public void SetDragController(InventoryDragController controller)
    {
        dragController = controller;
    }

    public void BindInventory(PlayerInventory inventory, int index)
    {
        boundInventory = inventory;
        boundHotbarIndex = index;
        boundEquipment = null;
        boundSlotType = BottomBarSlotType.None;
        isEquipmentSlot = false;
        Refresh();
    }

    public void BindEquipment(PlayerEquipment equipment, BottomBarSlotType slotType)
    {
        boundEquipment = equipment;
        boundSlotType = slotType;
        boundInventory = null;
        boundHotbarIndex = -1;
        isEquipmentSlot = true;
        Refresh();
    }

    public void Refresh()
    {
        ItemInstance instance = GetInstance();
        bool hasItem = instance != null && instance.Definition != null;

        if (icon != null)
        {
            icon.enabled = hasItem;
            icon.sprite = hasItem ? instance.Definition.Icon : null;
        }

        if (purposeIcon != null)
        {
            bool showPurpose = !hasItem && assignedPurposeIcon != null;
            purposeIcon.enabled = showPurpose;
            purposeIcon.sprite = showPurpose ? assignedPurposeIcon : null;
        }

        if (countText != null)
            countText.text = hasItem && instance.Quantity > 1 ? instance.Quantity.ToString() : "";
    }

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.SetActive(selected);
    }

    public ItemInstance RemoveItem()
    {
        if (!isEquipmentSlot)
        {
            InventorySlot slot = boundInventory?.GetSlot(boundHotbarIndex);
            if (slot == null || slot.IsEmpty)
                return null;

            ItemInstance item = slot.Instance;
            slot.Clear();
            boundInventory?.NotifyChanged();
            return item;
        }

        return boundEquipment?.Remove(boundSlotType);
    }

    public bool TryPlaceItem(ItemInstance incoming, out ItemInstance displaced)
    {
        displaced = null;

        if (incoming == null)
            return false;

        if (!isEquipmentSlot)
        {
            InventorySlot slot = boundInventory?.GetSlot(boundHotbarIndex);
            if (slot == null)
                return false;

            if (slot.IsEmpty)
            {
                slot.Set(incoming);
                boundInventory?.NotifyChanged();
                return true;
            }

            if (slot.Instance != null && slot.Instance.CanStackWith(incoming))
            {
                int moved = slot.Instance.AddQuantity(incoming.Quantity);
                incoming.RemoveQuantity(moved);
                boundInventory?.NotifyChanged();

                if (incoming.IsDepleted())
                    return true;

                displaced = incoming;
                return true;
            }

            displaced = slot.Instance;
            slot.Set(incoming);
            boundInventory?.NotifyChanged();
            return true;
        }

        if (boundEquipment == null)
            return false;

        bool ok = boundEquipment.TryPlace(boundSlotType, incoming, out displaced);
        return ok;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CurrentHoveredSlot = this;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CurrentHoveredSlot == this)
            CurrentHoveredSlot = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        owner?.HandleSlotClicked(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragController?.BeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragController?.EndDrag();
    }

    private ItemInstance GetInstance()
    {
        if (!isEquipmentSlot)
            return boundInventory?.GetSlot(boundHotbarIndex)?.Instance;

        return boundEquipment?.Get(boundSlotType);
    }

    public void SetPurposeIcon(Sprite sprite)
    {
        assignedPurposeIcon = sprite;
    }
}