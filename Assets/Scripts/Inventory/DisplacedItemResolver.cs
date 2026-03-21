using UnityEngine;

[DisallowMultipleComponent]
public sealed class DisplacedItemResolver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private Transform worldDropOrigin;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>(true);

        if (equipment == null)
            equipment = GetComponentInParent<PlayerEquipment>(true);

        if (worldDropOrigin == null)
            worldDropOrigin = inventory != null ? inventory.transform : transform;
    }

    public bool TryResolve(ItemInstance displaced, InventorySlotUI originalSourceSlot)
    {
        if (displaced == null)
            return true;

        Log($"TryResolve BEGIN | item={DescribeItem(displaced)}");

        // 1) Original source slot
        if (originalSourceSlot != null)
        {
            if (originalSourceSlot.TryPlaceItem(displaced, out ItemInstance returned))
            {
                if (returned == null || returned.IsDepleted())
                {
                    Log("Resolved via original source slot.");
                    return true;
                }

                displaced = returned;
                Log($"Original source slot partially resolved item. Remainder={DescribeItem(displaced)}");
            }
        }

        // 2) Preferred destination
        if (TryResolvePreferred(displaced))
        {
            Log("Resolved via preferred destination.");
            return true;
        }

        // 3) Drop to world
        if (TryDropToWorld(displaced))
        {
            Log("Resolved by dropping to world.");
            return true;
        }

        LogWarning("Failed to resolve displaced item.");
        return false;
    }

    private bool TryResolvePreferred(ItemInstance item)
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

    private bool TryDropToWorld(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        if (!item.Definition.Droppable || item.Definition.WorldPrefab == null)
            return false;

        Vector3 dropPos = worldDropOrigin != null ? worldDropOrigin.position : Vector3.zero;
        WorldItem dropped = Instantiate(item.Definition.WorldPrefab, dropPos, Quaternion.identity);
        dropped.Initialize(item);
        return true;
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
        Debug.Log($"[DisplacedItemResolver:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging) return;
        Debug.LogWarning($"[DisplacedItemResolver:{name}] {msg}", this);
    }
}