using NUnit.Framework.Internal.Execution;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventory : MonoBehaviour
{
    [Header("Capacity")]
    [Min(1)]
    [SerializeField] private int baseSlotCount = 4;

    [Min(1)]
    [SerializeField] private int baseHotbarSlotCount = 4;

    [Header("Refs")]
    [SerializeField] private PlayerEquipment equipment;

    [Header("Debug View")]
    [SerializeField] private List<InventorySlot> slots = new();

    [SerializeField] private int selectedHotbarIndex;

    public event Action InventoryChanged;
    public event Action<int> SelectedHotbarIndexChanged;

    public IReadOnlyList<InventorySlot> Slots => slots;
    public int BaseSlotCount => Mathf.Max(1, baseSlotCount);

    public int TotalSlotCount
    {
        get
        {
            int bonus = equipment != null ? equipment.BonusInventorySlots : 0;
            return Mathf.Max(1, BaseSlotCount + bonus);
        }
    }

    public int HotbarSlotCount => Mathf.Clamp(baseHotbarSlotCount, 1, TotalSlotCount);
    public int SelectedHotbarIndex => selectedHotbarIndex;

    private void Awake()
    {
        if (equipment == null)
            equipment = GetComponent<PlayerEquipment>();

        RebuildSlotsPreservingContents();
        ClampSelectedHotbarIndex(notify: false);
    }

    private void OnEnable()
    {
        if (equipment != null)
            equipment.EquipmentChanged += HandleEquipmentChanged;
    }

    private void OnDisable()
    {
        if (equipment != null)
            equipment.EquipmentChanged -= HandleEquipmentChanged;
    }

    public InventorySlot GetSlot(int index)
    {
        if (!IsValidSlotIndex(index))
            return null;

        return slots[index];
    }

    public bool IsValidSlotIndex(int index)
    {
        return index >= 0 && index < slots.Count;
    }

    public bool TryGetSelectedSlot(out InventorySlot slot)
    {
        slot = null;

        if (!IsValidSlotIndex(selectedHotbarIndex))
            return false;

        slot = slots[selectedHotbarIndex];
        return slot != null && !slot.IsEmpty;
    }

    public void SetSelectedHotbarIndex(int index)
    {
        int clamped = Wrap(index, HotbarSlotCount);
        if (selectedHotbarIndex == clamped)
            return;

        selectedHotbarIndex = clamped;
        SelectedHotbarIndexChanged?.Invoke(selectedHotbarIndex);
    }

    public void CycleSelectedHotbar(int delta)
    {
        if (HotbarSlotCount <= 0)
            return;

        SetSelectedHotbarIndex(selectedHotbarIndex + delta);
    }

    public bool TryAdd(ItemDefinition item, int quantity)
    {
        if (item == null || quantity <= 0)
            return false;

        if (!item.StowableInInventory)
            return false;

        int remaining = quantity;

        // Fill existing stacks first.
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty) continue;
            if (slot.Item != item) continue;

            int added = slot.Add(item, remaining);
            if (added > 0)
            {
                remaining -= added;
                if (remaining <= 0)
                {
                    NotifyInventoryChanged();
                    return true;
                }
            }
        }

        // Then use empty slots.
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (!slot.IsEmpty) continue;

            int added = slot.Add(item, remaining);
            if (added > 0)
            {
                remaining -= added;
                if (remaining <= 0)
                {
                    NotifyInventoryChanged();
                    return true;
                }
            }
        }

        bool fullyAdded = remaining <= 0;
        if (quantity != remaining)
            NotifyInventoryChanged();

        return fullyAdded;
    }

    public int GetTotalQuantity(ItemDefinition item)
    {
        if (item == null) return 0;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (!slot.IsEmpty && slot.Item == item)
                total += slot.Quantity;
        }

        return total;
    }

    public bool TryRemove(ItemDefinition item, int quantity)
    {
        if (item == null || quantity <= 0)
            return false;

        if (GetTotalQuantity(item) < quantity)
            return false;

        int remaining = quantity;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty || slot.Item != item) continue;

            int removed = slot.Remove(remaining);
            remaining -= removed;

            if (remaining <= 0)
            {
                NotifyInventoryChanged();
                return true;
            }
        }

        NotifyInventoryChanged();
        return remaining <= 0;
    }

    public bool MoveOrMergeSlot(int fromIndex, int toIndex)
    {
        if (!IsValidSlotIndex(fromIndex) || !IsValidSlotIndex(toIndex))
            return false;

        if (fromIndex == toIndex)
            return false;

        InventorySlot from = slots[fromIndex];
        InventorySlot to = slots[toIndex];

        if (from.IsEmpty)
            return false;

        // Merge if same item.
        if (!to.IsEmpty && to.Item == from.Item)
        {
            int moved = to.Add(from.Item, from.Quantity);
            if (moved <= 0)
                return false;

            from.Remove(moved);
            NotifyInventoryChanged();
            return true;
        }

        // Otherwise swap.
        SwapSlotContents(from, to);
        NotifyInventoryChanged();
        return true;
    }

    public bool TryDropSelected(int quantity, Vector3 worldPosition)
    {
        return TryDropFromSlot(selectedHotbarIndex, quantity, worldPosition);
    }

    public bool TryDropFromSlot(int slotIndex, int quantity, Vector3 worldPosition)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        InventorySlot slot = slots[slotIndex];
        if (slot.IsEmpty)
            return false;

        ItemDefinition item = slot.Item;
        if (item == null || !item.Droppable || item.WorldPrefab == null)
            return false;

        int dropAmount = Mathf.Clamp(quantity, 1, slot.Quantity);

        WorldItem dropped = Instantiate(item.WorldPrefab, worldPosition, Quaternion.identity);
        dropped.Initialize(item, dropAmount);

        slot.Remove(dropAmount);
        NotifyInventoryChanged();
        return true;
    }

    private void HandleEquipmentChanged()
    {
        RebuildSlotsPreservingContents();
        ClampSelectedHotbarIndex(notify: true);
    }

    private void RebuildSlotsPreservingContents()
    {
        List<(ItemDefinition item, int qty)> contents = new();

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.IsEmpty) continue;

            contents.Add((slot.Item, slot.Quantity));
        }

        slots.Clear();

        int targetCount = TotalSlotCount;
        for (int i = 0; i < targetCount; i++)
            slots.Add(new InventorySlot());

        // Re-add in order. If we somehow lose capacity later,
        // overflow will simply not re-add. We can handle harder rules later.
        for (int i = 0; i < contents.Count; i++)
        {
            TryAddInternal(contents[i].item, contents[i].qty);
        }

        NotifyInventoryChanged();
    }

    private bool TryAddInternal(ItemDefinition item, int quantity)
    {
        if (item == null || quantity <= 0)
            return false;

        int remaining = quantity;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty) continue;
            if (slot.Item != item) continue;

            int added = slot.Add(item, remaining);
            remaining -= added;
            if (remaining <= 0)
                return true;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (!slot.IsEmpty) continue;

            int added = slot.Add(item, remaining);
            remaining -= added;
            if (remaining <= 0)
                return true;
        }

        return remaining <= 0;
    }

    private void SwapSlotContents(InventorySlot a, InventorySlot b)
    {
        ItemDefinition itemA = a.Item;
        int qtyA = a.Quantity;

        a.Set(b.Item, b.Quantity);
        b.Set(itemA, qtyA);
    }

    private void ClampSelectedHotbarIndex(bool notify)
    {
        int clamped = Mathf.Clamp(selectedHotbarIndex, 0, Mathf.Max(0, HotbarSlotCount - 1));
        if (selectedHotbarIndex == clamped)
            return;

        selectedHotbarIndex = clamped;

        if (notify)
            SelectedHotbarIndexChanged?.Invoke(selectedHotbarIndex);
    }

    private void NotifyInventoryChanged()
    {
        InventoryChanged?.Invoke();
    }

    private static int Wrap(int index, int count)
    {
        if (count <= 0) return 0;
        int wrapped = index % count;
        if (wrapped < 0) wrapped += count;
        return wrapped;
    }
}