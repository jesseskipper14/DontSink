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

        // 3) Drop to world through shared utility so boat ownership/sorting hooks apply.
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

        Vector3 dropPos = worldDropOrigin != null ? worldDropOrigin.position : Vector3.zero;

        GameObject actor = null;

        if (inventory != null)
            actor = inventory.gameObject;
        else if (equipment != null)
            actor = equipment.gameObject;
        else
            actor = gameObject;

        bool ok = WorldItemDropUtility.TryDrop(item, dropPos, actor, out WorldItem dropped);

        Log(
            $"TryDropToWorld | item={DescribeItem(item)} | pos={dropPos} " +
            $"| actor={(actor != null ? actor.name : "NULL")} " +
            $"| dropped={(dropped != null ? dropped.name : "NULL")} | ok={ok}");

        return ok;
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