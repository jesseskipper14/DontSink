using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventoryInput : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Vector3 dropOffset = new(0.75f, 0f, 0f);

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>(true);
    }

    private void Update()
    {
        if (inventory == null)
            return;

        HandleSelection();
        HandleDrop();
    }

    private void HandleSelection()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.01f)
            inventory.CycleSelection(-1);
        else if (scroll < -0.01f)
            inventory.CycleSelection(1);

        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectHotbar(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectHotbar(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectHotbar(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectHotbar(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SelectHotbar(4);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SelectHotbar(5);
        if (Input.GetKeyDown(KeyCode.Alpha7)) SelectHotbar(6);
        if (Input.GetKeyDown(KeyCode.Alpha8)) SelectHotbar(7);
    }

    private void SelectHotbar(int index)
    {
        if (index < 0 || index >= inventory.HotbarSlotCount)
            return;

        inventory.SetSelectedSlot(PlayerInventory.HotbarIndexToSlotType(index));
    }

    private void HandleDrop()
    {
        if (!Input.GetKeyDown(KeyCode.Q))
            return;

        inventory.TryDropSelected(GetDropWorldPositionForUI());
    }

    public Vector3 GetDropWorldPositionForUI()
    {
        if (dropPoint != null)
            return dropPoint.position;

        return transform.position + dropOffset;
    }
}