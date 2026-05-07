using UnityEngine;

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