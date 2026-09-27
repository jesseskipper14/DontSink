using UnityEngine;

public enum ItemAcquisitionBlockReason
{
    None = 0,
    InvalidItem,
    HandsFull,
    InventoryFull,
    EquipSlotOccupied,
    NoValidDestination
}

[DisallowMultipleComponent]
public sealed class ItemAcquisitionResolver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>(true);

        if (equipment == null)
            equipment = GetComponentInParent<PlayerEquipment>(true);
    }

    public bool CanAcquire(ItemInstance item)
    {
        if (item == null || item.Definition == null)
        {
            Log("CanAcquire FAIL: item or definition null");
            return false;
        }

        Log($"CanAcquire BEGIN | item={DescribeItem(item)}");

        if (CanResolvePreferred(item))
        {
            Log("CanAcquire: TRUE via Preferred");
            return true;
        }

        if (CanResolveHands(item))
        {
            Log("CanAcquire: TRUE via Hands");
            return true;
        }

        if (CanResolveHotbar(item))
        {
            Log("CanAcquire: TRUE via Hotbar");
            return true;
        }

        Log("CanAcquire FAIL: no valid placement path");
        return false;
    }

    /// <summary>
    /// Returns the reason acquisition is currently blocked without mutating item
    /// state and without emitting the normal acquisition debug logs. Prompt/UI
    /// code can use this to explain a failed pickup rather than silently hiding
    /// the action.
    /// </summary>
    public bool TryGetBlockReason(
        ItemInstance item,
        out ItemAcquisitionBlockReason reason)
    {
        reason =
            ItemAcquisitionBlockReason.None;

        if (item == null ||
            item.Definition == null ||
            item.Quantity <= 0)
        {
            reason =
                ItemAcquisitionBlockReason.InvalidItem;

            return true;
        }

        if (CanAcquireSilently(item))
            return false;

        bool handsOccupied =
            equipment != null &&
            equipment.Get(
                BottomBarSlotType.Hands) != null;

        bool hasPotentialPreferredNonHandsPath =
            HasPotentialPreferredNonHandsPath(
                item);

        bool canPotentiallyUseHotbar =
            HasAnyStructurallyAllowedHotbarDestination(
                item);

        // "Hands Full" is intentionally reserved for genuinely hands-only items.
        // A stowable item with a full inventory should not claim the player's hands
        // are the sole problem, and an equippable item with a blocked preferred slot
        // has its own more specific failure.
        if (handsOccupied &&
            !hasPotentialPreferredNonHandsPath &&
            !canPotentiallyUseHotbar)
        {
            reason =
                ItemAcquisitionBlockReason.HandsFull;

            Log(
                "BlockReason: HandsFull | " +
                $"preferredNonHands={hasPotentialPreferredNonHandsPath} " +
                $"allowedHotbarPath={canPotentiallyUseHotbar}");

            return true;
        }

        if (item.Definition.PreferredDisplacedDestination ==
                PreferredDisplacedDestination.MatchingEquipSlot &&
            equipment != null)
        {
            BottomBarSlotType slot =
                item.Definition.EquipSlot;

            if (slot != BottomBarSlotType.None &&
                slot != BottomBarSlotType.Hands &&
                equipment.IsSlotOccupiedOrBlocked(
                    slot))
            {
                reason =
                    ItemAcquisitionBlockReason.EquipSlotOccupied;

                return true;
            }
        }

        if (item.Definition.StowableInInventory &&
            inventory != null &&
            !inventory.CanFullyAdd(item))
        {
            reason =
                ItemAcquisitionBlockReason.InventoryFull;

            return true;
        }

        reason =
            ItemAcquisitionBlockReason.NoValidDestination;

        return true;
    }

    private bool CanAcquireSilently(
        ItemInstance item)
    {
        if (item == null ||
            item.Definition == null ||
            item.Quantity <= 0)
        {
            return false;
        }

        switch (item.Definition.PreferredDisplacedDestination)
        {
            case PreferredDisplacedDestination.MatchingEquipSlot:
                {
                    if (equipment != null)
                    {
                        BottomBarSlotType slot =
                            item.Definition.EquipSlot;

                        if (slot != BottomBarSlotType.None &&
                            slot != BottomBarSlotType.Hands &&
                            equipment.CanEquip(
                                slot,
                                item))
                        {
                            return true;
                        }
                    }

                    break;
                }

            case PreferredDisplacedDestination.AnyHotbar:
                {
                    if (inventory != null &&
                        inventory.CanFullyAdd(
                            item))
                    {
                        return true;
                    }

                    break;
                }
        }

        if (equipment != null &&
            equipment.Get(
                BottomBarSlotType.Hands) == null &&
            equipment.CanEquip(
                BottomBarSlotType.Hands,
                item))
        {
            return true;
        }

        return
            inventory != null &&
            inventory.CanFullyAdd(
                item);
    }

    private bool HasAnyStructurallyAllowedHotbarDestination(
        ItemInstance item)
    {
        if (inventory == null ||
            item == null ||
            item.Definition == null ||
            !item.Definition.StowableInInventory)
        {
            return false;
        }

        for (int i = 0;
             i < inventory.HotbarSlotCount;
             i++)
        {
            BottomBarSlotType slot =
                PlayerInventory.HotbarIndexToSlotType(
                    i);

            if (item.Definition.IsAllowedInParentSlot(
                    slot))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPotentialPreferredNonHandsPath(
        ItemInstance item)
    {
        if (item == null ||
            item.Definition == null)
        {
            return false;
        }

        if (item.Definition.PreferredDisplacedDestination !=
            PreferredDisplacedDestination.MatchingEquipSlot)
        {
            return false;
        }

        BottomBarSlotType slot =
            item.Definition.EquipSlot;

        return
            equipment != null &&
            item.Definition.IsEquippable &&
            slot != BottomBarSlotType.None &&
            slot != BottomBarSlotType.Hands;
    }

    public bool TryAcquire(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        Log($"TryAcquire BEGIN | item={DescribeItem(item)}");

        // 1) Preferred destination
        if (TryPreferred(item))
        {
            Log("Resolved via preferred destination.");
            return true;
        }

        // 2) Hands
        if (TryHands(item))
        {
            Log("Resolved via hands.");
            return true;
        }

        // 3) Hotbar
        if (TryHotbar(item))
        {
            Log("Resolved via hotbar.");
            return true;
        }

        Log("TryAcquire failed.");
        return false;
    }

    private bool CanResolvePreferred(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        switch (item.Definition.PreferredDisplacedDestination)
        {
            case PreferredDisplacedDestination.MatchingEquipSlot:
                {
                    if (equipment == null)
                    {
                        Log("Preferred FAIL: equipment null");
                        return false;
                    }

                    var slot = item.Definition.EquipSlot;

                    if (slot == BottomBarSlotType.None || slot == BottomBarSlotType.Hands)
                    {
                        Log($"Preferred FAIL: invalid slot {slot}");
                        return false;
                    }

                    if (equipment.Get(slot) != null)
                    {
                        Log($"Preferred FAIL: slot {slot} occupied");
                        return false;
                    }

                    bool ok = equipment.CanEquip(slot, item);
                    Log($"Preferred check slot={slot} ok={ok}");
                    return ok;
                }

            case PreferredDisplacedDestination.AnyHotbar:
                {
                    bool ok = inventory != null && inventory.CanFullyAdd(item);
                    Log($"Preferred Hotbar check={ok}");
                    return ok;
                }

            default:
                return false;
        }
    }

    private bool TryPreferred(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        switch (item.Definition.PreferredDisplacedDestination)
        {
            case PreferredDisplacedDestination.MatchingEquipSlot:
                return equipment != null && equipment.TryPlaceIntoPreferredSlotIfEmpty(item);

            case PreferredDisplacedDestination.AnyHotbar:
                return inventory != null && inventory.TryAddInstance(item);

            default:
                return false;
        }
    }

    private bool CanResolveHands(ItemInstance item)
    {
        if (equipment == null || item == null)
        {
            Log("Hands FAIL: equipment or item null");
            return false;
        }

        if (equipment.Get(BottomBarSlotType.Hands) != null)
        {
            Log("Hands FAIL: hands occupied");
            return false;
        }

        bool ok = equipment.CanEquip(BottomBarSlotType.Hands, item);

        Log($"Hands check ok={ok}");

        return ok;
    }

    private bool TryHands(ItemInstance item)
    {
        if (equipment == null || item == null)
            return false;

        if (equipment.Get(BottomBarSlotType.Hands) != null)
            return false;

        return equipment.TryPlace(BottomBarSlotType.Hands, item, out _);
    }

    private bool CanResolveHotbar(ItemInstance item)
    {
        if (inventory == null)
        {
            Log("Hotbar FAIL: inventory null");
            return false;
        }

        bool ok = inventory.CanFullyAdd(item);

        Log($"Hotbar check ok={ok}");

        return ok;
    }

    private bool TryHotbar(ItemInstance item)
    {
        return inventory != null && inventory.TryAddInstance(item);
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
        Debug.Log($"[ItemAcquisitionResolver:{name}] {msg}", this);
    }
}