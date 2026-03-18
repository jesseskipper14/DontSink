using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventoryInput : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Drop")]
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Vector3 dropOffset = new(0.75f, 0f, 0f);
    [SerializeField] private int dropQuantity = 1;

    [Header("Input")]
    [SerializeField] private bool enableScrollWheel = true;
    [SerializeField] private bool enableNumberKeys = true;
    [SerializeField] private bool enableDrop = true;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();

        if (inventory == null)
            inventory = GetComponentInChildren<PlayerInventory>();

        if (inventory == null)
            Debug.LogError($"{name}: PlayerInventory not found in hierarchy.", this);
    }

    private void Update()
    {
        if (inventory == null)
            return;

        HandleHotbarSelection();
        HandleDrop();
    }

    private void HandleHotbarSelection()
    {
        if (enableScrollWheel)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0.01f)
            {
                inventory.CycleSelectedHotbar(-1);
            }
            else if (scroll < -0.01f)
            {
                inventory.CycleSelectedHotbar(1);
            }
        }

        if (!enableNumberKeys)
            return;

        int hotbarCount = inventory.HotbarSlotCount;

        if (hotbarCount >= 1 && Input.GetKeyDown(KeyCode.Alpha1)) inventory.SetSelectedHotbarIndex(0);
        if (hotbarCount >= 2 && Input.GetKeyDown(KeyCode.Alpha2)) inventory.SetSelectedHotbarIndex(1);
        if (hotbarCount >= 3 && Input.GetKeyDown(KeyCode.Alpha3)) inventory.SetSelectedHotbarIndex(2);
        if (hotbarCount >= 4 && Input.GetKeyDown(KeyCode.Alpha4)) inventory.SetSelectedHotbarIndex(3);
        if (hotbarCount >= 5 && Input.GetKeyDown(KeyCode.Alpha5)) inventory.SetSelectedHotbarIndex(4);
        if (hotbarCount >= 6 && Input.GetKeyDown(KeyCode.Alpha6)) inventory.SetSelectedHotbarIndex(5);
        if (hotbarCount >= 7 && Input.GetKeyDown(KeyCode.Alpha7)) inventory.SetSelectedHotbarIndex(6);
        if (hotbarCount >= 8 && Input.GetKeyDown(KeyCode.Alpha8)) inventory.SetSelectedHotbarIndex(7);
        if (hotbarCount >= 9 && Input.GetKeyDown(KeyCode.Alpha9)) inventory.SetSelectedHotbarIndex(8);
    }

    private void HandleDrop()
    {
        if (!enableDrop)
            return;

        if (!Input.GetKeyDown(KeyCode.Q))
            return;

        Vector3 spawnPosition = GetDropWorldPosition();
        inventory.TryDropSelected(dropQuantity, spawnPosition);
    }

    private Vector3 GetDropWorldPosition()
    {
        if (dropPoint != null)
            return dropPoint.position;

        return transform.position + dropOffset;
    }
}