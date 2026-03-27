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

        ItemInstance containerItem = worldItem != null ? worldItem.Instance : null;
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
            return false;

        if (ReferenceEquals(incoming, containerItem))
            return false;

        return ContainerPlacementUtility.CanAutoInsert(containerItem, incoming);
    }

    public bool TryAcceptWorldDrop(ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (!IsInRange())
            return false;

        if (incoming == null)
            return false;

        ItemInstance containerItem = worldItem != null ? worldItem.Instance : null;
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
            return false;

        if (ReferenceEquals(incoming, containerItem))
            return false;

        return ContainerPlacementUtility.TryAutoInsert(containerItem, incoming, out remainder);
    }

    private bool IsInRange()
    {
        if (playerTransform == null)
            return false;

        float dist = Vector2.Distance(playerTransform.position, transform.position);
        return dist <= maxDepositDistance;
    }
}