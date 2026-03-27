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

        if (hotbarSlots[index] == null)
            hotbarSlots[index] = new InventorySlot();

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
            return FindFirstEmptySlot(instance) >= 0;

        int remaining = instance.Quantity;

        // First try stacking into existing valid stacks
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            InventorySlot slot = hotbarSlots[i];
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            BottomBarSlotType slotType = HotbarIndexToSlotType(i);
            if (!instance.Definition.IsAllowedInParentSlot(slotType))
                continue;

            if (!slot.Instance.CanStackWith(instance))
                continue;

            remaining -= slot.Instance.RemainingStackSpace;
            if (remaining <= 0)
                return true;
        }

        // Then count valid empty slots
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            InventorySlot slot = hotbarSlots[i];
            if (slot == null)
                continue;

            BottomBarSlotType slotType = HotbarIndexToSlotType(i);
            if (!instance.Definition.IsAllowedInParentSlot(slotType))
                continue;

            if (slot.IsEmpty)
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
        if (instance == null || instance.Definition == null || instance.Quantity <= 0)
            return false;

        if (!CanFullyAdd(instance))
            return false;

        EnsureSlotCount();

        if (instance.IsStackable)
        {
            for (int i = 0; i < hotbarSlots.Count; i++)
            {
                InventorySlot slot = hotbarSlots[i];
                if (slot == null || slot.IsEmpty || slot.Instance == null)
                    continue;

                BottomBarSlotType slotType = HotbarIndexToSlotType(i);
                if (!instance.Definition.IsAllowedInParentSlot(slotType))
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

        int empty = FindFirstEmptySlot(instance);
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
            return TryDropHotbarSlot(hotbarIndex, 1, worldPosition);
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

    public bool TryDropInstance(ItemInstance instance, Vector3 worldPosition)
    {
        if (instance == null || instance.Definition == null)
            return false;

        if (!instance.Definition.Droppable || instance.Definition.WorldPrefab == null)
            return false;

        WorldItem dropped = Instantiate(instance.Definition.WorldPrefab, worldPosition, Quaternion.identity);
        dropped.Initialize(instance);

        InventoryChanged?.Invoke();
        return true;
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
        if (snapshot != null && snapshot.hotbarSlotCount > 0)
            hotbarSlotCount = Mathf.Clamp(snapshot.hotbarSlotCount, 1, MaxSupportedHotbarSlots);

        EnsureSlotCount();

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i] == null)
                hotbarSlots[i] = new InventorySlot();

            hotbarSlots[i].Clear();
        }

        if (snapshot != null && snapshot.hotbarSlots != null)
        {
            int count = Mathf.Min(hotbarSlots.Count, snapshot.hotbarSlots.Count);
            for (int i = 0; i < count; i++)
            {
                ItemInstance instance = ItemInstance.FromSnapshot(snapshot.hotbarSlots[i], resolver);
                hotbarSlots[i].Set(instance);
            }

            selectedSlot = snapshot.selectedSlot;
        }
        else
        {
            selectedSlot = BottomBarSlotType.Hotbar0;
        }

        InventoryChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    private void ClearAllSlots()
    {
        EnsureSlotCount();

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i] == null)
                hotbarSlots[i] = new InventorySlot();

            hotbarSlots[i].Clear();
        }
    }

    private void EnsureSlotCount()
    {
        int desired = HotbarSlotCount;

        if (hotbarSlots == null)
            hotbarSlots = new List<InventorySlot>();

        while (hotbarSlots.Count < desired)
            hotbarSlots.Add(new InventorySlot());

        if (hotbarSlots.Count > desired)
            hotbarSlots.RemoveRange(desired, hotbarSlots.Count - desired);

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i] == null)
                hotbarSlots[i] = new InventorySlot();
        }
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i] != null && hotbarSlots[i].IsEmpty)
                return i;
        }

        return -1;
    }

    private int FindFirstEmptySlot(ItemInstance instance)
    {
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            InventorySlot slot = hotbarSlots[i];
            if (slot == null || !slot.IsEmpty)
                continue;

            if (instance != null && instance.Definition != null)
            {
                BottomBarSlotType slotType = HotbarIndexToSlotType(i);
                if (!instance.Definition.IsAllowedInParentSlot(slotType))
                    continue;
            }

            return i;
        }

        return -1;
    }

    private List<BottomBarSlotType> BuildSelectionOrder()
    {
        List<BottomBarSlotType> result = new()
        {
            BottomBarSlotType.Hands,
            BottomBarSlotType.Feet,
            BottomBarSlotType.Head
        };

        for (int i = 0; i < HotbarSlotCount; i++)
            result.Add(HotbarIndexToSlotType(i));

        result.Add(BottomBarSlotType.Toolbelt);
        result.Add(BottomBarSlotType.Body);
        result.Add(BottomBarSlotType.Backpack);

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

    public bool TryAutoInsert(ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (incoming == null || incoming.Definition == null || incoming.Quantity <= 0)
            return false;

        if (!incoming.Definition.StowableInInventory)
            return false;

        EnsureSlotCount();

        bool changed = false;

        // 1) Stack into non-hand equipment first
        if (TryStackIntoEquipment(incoming, includeHands: false))
        {
            changed = true;

            if (incoming.IsDepleted())
            {
                remainder = null;
                InventoryChanged?.Invoke();
                return true;
            }
        }

        // 2) Stack into hotbar
        if (TryStackIntoHotbar(incoming))
        {
            changed = true;

            if (incoming.IsDepleted())
            {
                remainder = null;
                InventoryChanged?.Invoke();
                return true;
            }
        }

        // 3) Place into empty non-hand equipment slot
        if (TryPlaceIntoEmptyEquipmentSlot(incoming, includeHands: false))
        {
            remainder = null;
            InventoryChanged?.Invoke();
            return true;
        }

        // 4) Place into empty hotbar slot
        if (TryPlaceIntoEmptyHotbarSlot(incoming))
        {
            remainder = null;
            InventoryChanged?.Invoke();
            return true;
        }

        // 5) Auto-insert into equipped containers (backpack, etc.)
        if (TryInsertIntoEquippedContainers(incoming, out ItemInstance equippedContainerRemainder))
        {
            remainder = equippedContainerRemainder;
            InventoryChanged?.Invoke();
            return true;
        }

        // 6) Auto-insert into hotbar container items
        if (TryInsertIntoHotbarContainers(incoming, out ItemInstance hotbarContainerRemainder))
        {
            remainder = hotbarContainerRemainder;
            InventoryChanged?.Invoke();
            return true;
        }

        // 7) Hands dead last
        if (TryStackIntoEquipment(incoming, includeHands: true, handsOnly: true))
        {
            changed = true;

            if (incoming.IsDepleted())
            {
                remainder = null;
                InventoryChanged?.Invoke();
                return true;
            }
        }

        if (TryPlaceIntoEmptyEquipmentSlot(incoming, includeHands: true, handsOnly: true))
        {
            remainder = null;
            InventoryChanged?.Invoke();
            return true;
        }

        if (changed)
        {
            remainder = incoming;
            InventoryChanged?.Invoke();
            return true;
        }

        remainder = incoming;
        return false;
    }

    private bool TryStackIntoEquipment(ItemInstance incoming, bool includeHands, bool handsOnly = false)
    {
        if (equipment == null)
            return false;

        bool changed = false;

        foreach (BottomBarSlotType slotType in GetAutoInsertEquipmentSlots(includeHands, handsOnly))
        {
            ItemInstance existing = equipment.Get(slotType);
            if (existing == null || existing.Definition == null)
                continue;

            if (!incoming.Definition.IsAllowedInParentSlot(slotType))
                continue;

            if (!existing.CanStackWith(incoming))
                continue;

            int moved = existing.AddQuantity(incoming.Quantity);
            if (moved <= 0)
                continue;

            incoming.RemoveQuantity(moved);
            changed = true;

            if (incoming.IsDepleted())
                return true;
        }

        return changed;
    }

    private bool TryStackIntoHotbar(ItemInstance incoming)
    {
        bool changed = false;

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            InventorySlot slot = hotbarSlots[i];
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            BottomBarSlotType slotType = HotbarIndexToSlotType(i);
            if (!incoming.Definition.IsAllowedInParentSlot(slotType))
                continue;

            if (!slot.Instance.CanStackWith(incoming))
                continue;

            int moved = slot.Instance.AddQuantity(incoming.Quantity);
            if (moved <= 0)
                continue;

            incoming.RemoveQuantity(moved);
            changed = true;

            if (incoming.IsDepleted())
                return true;
        }

        return changed;
    }

    private bool TryPlaceIntoEmptyEquipmentSlot(ItemInstance incoming, bool includeHands, bool handsOnly = false)
    {
        if (equipment == null)
            return false;

        foreach (BottomBarSlotType slotType in GetAutoInsertEquipmentSlots(includeHands, handsOnly))
        {
            if (!incoming.Definition.IsAllowedInParentSlot(slotType))
                continue;

            if (equipment.Get(slotType) != null)
                continue;

            if (!equipment.CanEquip(slotType, incoming))
                continue;

            return equipment.TryPlace(slotType, incoming, out _);
        }

        return false;
    }

    private bool TryPlaceIntoEmptyHotbarSlot(ItemInstance incoming)
    {
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            InventorySlot slot = hotbarSlots[i];
            if (slot == null || !slot.IsEmpty)
                continue;

            BottomBarSlotType slotType = HotbarIndexToSlotType(i);
            if (!incoming.Definition.IsAllowedInParentSlot(slotType))
                continue;

            slot.Set(incoming);
            return true;
        }

        return false;
    }

    private bool TryInsertIntoEquippedContainers(ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (equipment == null)
            return false;

        foreach (BottomBarSlotType slotType in GetAutoInsertEquipmentSlots(includeHands: false, handsOnly: false))
        {
            ItemInstance containerItem = equipment.Get(slotType);
            if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
                continue;

            if (ReferenceEquals(containerItem, incoming))
                continue;

            if (ContainerPlacementUtility.TryAutoInsert(containerItem, incoming, out ItemInstance containerRemainder))
            {
                remainder = containerRemainder;
                return true;
            }
        }

        return false;
    }

    private bool TryInsertIntoHotbarContainers(ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            InventorySlot slot = hotbarSlots[i];
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            ItemInstance containerItem = slot.Instance;
            if (!containerItem.IsContainer || containerItem.ContainerState == null)
                continue;

            if (ReferenceEquals(containerItem, incoming))
                continue;

            if (ContainerPlacementUtility.TryAutoInsert(containerItem, incoming, out ItemInstance containerRemainder))
            {
                remainder = containerRemainder;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<BottomBarSlotType> GetAutoInsertEquipmentSlots(bool includeHands, bool handsOnly = false)
    {
        if (handsOnly)
        {
            yield return BottomBarSlotType.Hands;
            yield break;
        }

        yield return BottomBarSlotType.Backpack;
        yield return BottomBarSlotType.Toolbelt;
        yield return BottomBarSlotType.Body;
        yield return BottomBarSlotType.Head;
        yield return BottomBarSlotType.Feet;

        if (includeHands)
            yield return BottomBarSlotType.Hands;
    }
}