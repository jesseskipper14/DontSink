using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerLoadoutPersistence : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (equipment == null)
            equipment = GetComponent<PlayerEquipment>();
    }

    public PlayerLoadoutSnapshot CaptureSnapshot()
    {
        return new PlayerLoadoutSnapshot
        {
            version = 1,
            inventory = inventory != null ? inventory.CaptureSnapshot() : null,
            equipment = equipment != null ? equipment.CaptureSnapshot() : null
        };
    }

    public void RestoreSnapshot(PlayerLoadoutSnapshot snapshot)
    {
        if (inventory != null)
            inventory.RestoreSnapshot(snapshot?.inventory, itemCatalog);

        if (equipment != null)
            equipment.RestoreSnapshot(snapshot?.equipment, itemCatalog);
    }

    public void SaveToGameState()
    {
        if (GameState.I == null)
            return;

        GameState.I.playerLoadout = CaptureSnapshot();
    }

    public void RestoreFromGameState()
    {
        if (GameState.I == null)
            return;

        RestoreSnapshot(GameState.I.playerLoadout);
    }
}