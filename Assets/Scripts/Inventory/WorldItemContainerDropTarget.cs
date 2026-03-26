using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItemContainerDropTarget : MonoBehaviour, IWorldItemDropTarget
{
    [SerializeField] private WorldItem worldItem;

    [Header("Range")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float maxDepositDistance = 2.25f;

    private void Awake()
    {
        if (worldItem == null)
            worldItem = GetComponent<WorldItem>();

        if (playerTransform == null)
        {
            PlayerInventory playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (playerInventory != null)
                playerTransform = playerInventory.transform;
        }
    }

    public bool CanAcceptWorldDrop(ItemInstance incoming)
    {
        if (!IsInRange())
            return false;

        if (incoming == null)
            return false;

        if (worldItem == null || worldItem.Instance == null)
            return false;

        if (ReferenceEquals(incoming, worldItem.Instance))
            return false;

        return CanAcceptLikeExternalSlot(worldItem.Instance, incoming);
    }

    public bool TryAcceptWorldDrop(ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (!IsInRange())
            return false;

        if (incoming == null)
            return false;

        if (worldItem == null || worldItem.Instance == null)
            return false;

        if (ReferenceEquals(incoming, worldItem.Instance))
            return false;

        return TryAcceptLikeExternalSlot(worldItem.Instance, incoming, out remainder);
    }

    private bool IsInRange()
    {
        if (playerTransform == null)
            return false;

        float dist = Vector2.Distance(playerTransform.position, transform.position);
        return dist <= maxDepositDistance;
    }

    private static bool CanAcceptLikeExternalSlot(ItemInstance containerItem, ItemInstance incoming)
    {
        if (containerItem == null || incoming == null)
            return false;

        if (!containerItem.IsContainer || containerItem.ContainerState == null)
            return false;

        // Match external inventory slot behavior:
        // allow direct placement into an empty chest slot,
        // and allow insertion into a contained container item if applicable.
        return true;
    }

    private static bool TryAcceptLikeExternalSlot(ItemInstance containerItem, ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (containerItem == null || incoming == null)
            return false;

        if (!containerItem.IsContainer || containerItem.ContainerState == null)
            return false;

        ItemContainerState state = containerItem.ContainerState;

        // 1) Stack into compatible stacks
        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            if (!slot.Instance.CanStackWith(incoming))
                continue;

            int moved = slot.Instance.AddQuantity(incoming.Quantity);
            if (moved > 0)
                incoming.RemoveQuantity(moved);

            if (incoming.IsDepleted())
            {
                remainder = null;
                state.NotifyChanged();
                return true;
            }
        }

        // 2) Insert into container item already in chest, if applicable
        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            ItemInstance slotted = slot.Instance;
            if (!slotted.IsContainer)
                continue;

            if (!slotted.TryInsertIntoContainer(incoming, out ItemInstance nestedRemainder))
                continue;

            remainder = nestedRemainder;
            state.NotifyChanged();
            return true;
        }

        // 3) Put into first empty slot directly
        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || !slot.IsEmpty)
                continue;

            slot.Set(incoming);
            remainder = null;
            state.NotifyChanged();
            return true;
        }

        return false;
    }
}