using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventory : MonoBehaviour
{
    private const int MaxSupportedHotbarSlots = 8;

    [SerializeField] private int hotbarSlotCount = 6;
    [SerializeField] private bool scrollIncludesEquipmentSlots = true;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private List<InventorySlot> hotbarSlots = new();
    [SerializeField] private BottomBarSlotType selectedSlot = BottomBarSlotType.Hotbar0;

    public event Action InventoryChanged;
    public event Action SelectionChanged;

    public int HotbarSlotCount => Mathf.Clamp(hotbarSlotCount, 1, MaxSupportedHotbarSlots);
    public bool ScrollIncludesEquipmentSlots => scrollIncludesEquipmentSlots;
    public BottomBarSlotType SelectedSlot => selectedSlot;

    private void Awake()
    {
        if (equipment == null)
            equipment = GetComponentInParent<PlayerEquipment>(true);

        EnsureSlotCount();
    }

    public InventorySlot GetSlot(int index)
    {
        EnsureSlotCount();

        if (index < 0 || index >= hotbarSlots.Count)
            return null;

        return hotbarSlots[index];
    }

    public void NotifyChanged()
    {
        InventoryChanged?.Invoke();
    }

    public void SetSelectedSlot(BottomBarSlotType slot)
    {
        if (selectedSlot == slot)
            return;

        selectedSlot = slot;
        SelectionChanged?.Invoke();
    }

    public void CycleSelection(int delta)
    {
        if (!scrollIncludesEquipmentSlots)
        {
            int currentHotbar = SlotTypeToHotbarIndex(selectedSlot);
            if (currentHotbar < 0)
                currentHotbar = 0;

            currentHotbar = Wrap(currentHotbar + delta, HotbarSlotCount);
            selectedSlot = HotbarIndexToSlotType(currentHotbar);
            SelectionChanged?.Invoke();
            return;
        }

        List<BottomBarSlotType> order = BuildSelectionOrder();
        int currentIndex = order.IndexOf(selectedSlot);
        if (currentIndex < 0)
            currentIndex = 0;

        currentIndex = Wrap(currentIndex + delta, order.Count);
        selectedSlot = order[currentIndex];
        SelectionChanged?.Invoke();
    }

    public bool CanFullyAdd(ItemInstance instance)
    {
        if (instance == null || instance.Definition == null || instance.Quantity <= 0)
            return false;

        if (!instance.Definition.StowableInInventory)
            return false;

        EnsureSlotCount();

        if (!instance.IsStackable)
            return FindFirstEmptySlot() >= 0;

        int remaining = instance.Quantity;

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            InventorySlot slot = hotbarSlots[i];
            if (slot.IsEmpty || slot.Instance == null)
                continue;

            if (!slot.Instance.CanStackWith(instance))
                continue;

            remaining -= slot.Instance.RemainingStackSpace;
            if (remaining <= 0)
                return true;
        }

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i].IsEmpty)
            {
                remaining -= instance.MaxStack;
                if (remaining <= 0)
                    return true;
            }
        }

        return false;
    }

    public bool TryAddInstance(ItemInstance instance)
    {
        if (!CanFullyAdd(instance))
            return false;

        EnsureSlotCount();

        if (instance.IsStackable)
        {
            for (int i = 0; i < hotbarSlots.Count; i++)
            {
                InventorySlot slot = hotbarSlots[i];
                if (slot.IsEmpty || slot.Instance == null)
                    continue;

                if (!slot.Instance.CanStackWith(instance))
                    continue;

                int moved = slot.Instance.AddQuantity(instance.Quantity);
                instance.RemoveQuantity(moved);

                if (instance.IsDepleted())
                {
                    InventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        int empty = FindFirstEmptySlot();
        if (empty < 0)
            return false;

        hotbarSlots[empty].Set(instance);
        InventoryChanged?.Invoke();
        return true;
    }

    public bool TryAdd(ItemDefinition definition, int quantity)
    {
        if (definition == null || quantity <= 0)
            return false;

        return TryAddInstance(ItemInstance.Create(definition, quantity));
    }

    public bool TryDropSelected(Vector3 worldPosition)
    {
        if (selectedSlot >= BottomBarSlotType.Hotbar0 && selectedSlot <= BottomBarSlotType.Hotbar7)
        {
            int hotbarIndex = SlotTypeToHotbarIndex(selectedSlot);
            return TryDropHotbarSlot(hotbarIndex, int.MaxValue, worldPosition);
        }

        if (equipment == null)
            return false;

        ItemInstance equipped = equipment.Get(selectedSlot);
        if (equipped == null || equipped.Definition == null)
            return false;

        if (!equipped.Definition.Droppable || equipped.Definition.WorldPrefab == null)
            return false;

        equipment.Remove(selectedSlot);

        WorldItem dropped = Instantiate(equipped.Definition.WorldPrefab, worldPosition, Quaternion.identity);
        dropped.Initialize(equipped);

        return true;
    }

    public bool TryDropHotbarSlot(int index, int quantity, Vector3 worldPosition)
    {
        EnsureSlotCount();

        InventorySlot slot = GetSlot(index);
        if (slot == null || slot.IsEmpty || slot.Instance == null)
            return false;

        ItemInstance instance = slot.Instance;
        if (instance.Definition == null || !instance.Definition.Droppable || instance.Definition.WorldPrefab == null)
            return false;

        int dropAmount = Mathf.Clamp(quantity, 1, instance.Quantity);

        ItemInstance droppedInstance;
        if (instance.IsStackable && dropAmount < instance.Quantity)
        {
            droppedInstance = instance.SplitOff(dropAmount);
            if (droppedInstance == null)
                return false;
        }
        else
        {
            droppedInstance = instance;
            slot.Clear();
        }

        WorldItem dropped = Instantiate(droppedInstance.Definition.WorldPrefab, worldPosition, Quaternion.identity);
        dropped.Initialize(droppedInstance);

        InventoryChanged?.Invoke();
        return true;
    }

    private void EnsureSlotCount()
    {
        int desired = HotbarSlotCount;

        while (hotbarSlots.Count < desired)
            hotbarSlots.Add(new InventorySlot());

        if (hotbarSlots.Count > desired)
            hotbarSlots.RemoveRange(desired, hotbarSlots.Count - desired);
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i].IsEmpty)
                return i;
        }

        return -1;
    }

    private List<BottomBarSlotType> BuildSelectionOrder()
    {
        List<BottomBarSlotType> result = new()
        {
            BottomBarSlotType.Hands,
            BottomBarSlotType.Head,
            BottomBarSlotType.Feet
        };

        for (int i = 0; i < HotbarSlotCount; i++)
            result.Add(HotbarIndexToSlotType(i));

        result.Add(BottomBarSlotType.Toolbelt);
        result.Add(BottomBarSlotType.Backpack);
        result.Add(BottomBarSlotType.Body);

        return result;
    }

    public static BottomBarSlotType HotbarIndexToSlotType(int index)
    {
        return (BottomBarSlotType)((int)BottomBarSlotType.Hotbar0 + index);
    }

    public static int SlotTypeToHotbarIndex(BottomBarSlotType slot)
    {
        if (slot < BottomBarSlotType.Hotbar0 || slot > BottomBarSlotType.Hotbar7)
            return -1;

        return (int)slot - (int)BottomBarSlotType.Hotbar0;
    }

    private static int Wrap(int value, int count)
    {
        if (count <= 0)
            return 0;

        int wrapped = value % count;
        if (wrapped < 0)
            wrapped += count;

        return wrapped;
    }

    public InventorySnapshot CaptureSnapshot()
    {
        EnsureSlotCount();

        InventorySnapshot snapshot = new InventorySnapshot
        {
            version = 1,
            hotbarSlotCount = HotbarSlotCount,
            selectedSlot = selectedSlot,
            hotbarSlots = new List<ItemInstanceSnapshot>(hotbarSlots.Count)
        };

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            ItemInstanceSnapshot itemSnapshot = hotbarSlots[i] != null && !hotbarSlots[i].IsEmpty
                ? hotbarSlots[i].Instance.ToSnapshot()
                : null;

            snapshot.hotbarSlots.Add(itemSnapshot);
        }

        return snapshot;
    }

    public void RestoreSnapshot(InventorySnapshot snapshot, IItemDefinitionResolver resolver)
    {
        if (snapshot == null)
        {
            ClearAllSlots();
            selectedSlot = BottomBarSlotType.Hotbar0;
            InventoryChanged?.Invoke();
            SelectionChanged?.Invoke();
            return;
        }

        if (snapshot != null && snapshot.hotbarSlotCount > 0)
            hotbarSlotCount = Mathf.Clamp(snapshot.hotbarSlotCount, 1, MaxSupportedHotbarSlots);

        EnsureSlotCount();

        for (int i = 0; i < hotbarSlots.Count; i++)
            hotbarSlots[i].Clear();

        int count = Mathf.Min(hotbarSlots.Count, snapshot.hotbarSlots.Count);
        for (int i = 0; i < count; i++)
        {
            ItemInstance instance = ItemInstance.FromSnapshot(snapshot.hotbarSlots[i], resolver);
            hotbarSlots[i].Set(instance);
        }

        selectedSlot = snapshot.selectedSlot;
        InventoryChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    private void ClearAllSlots()
    {
        EnsureSlotCount();

        for (int i = 0; i < hotbarSlots.Count; i++)
            hotbarSlots[i].Clear();
    }
}