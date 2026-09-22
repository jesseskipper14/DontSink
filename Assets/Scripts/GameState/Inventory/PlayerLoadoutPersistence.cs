using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerLoadoutPersistence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    [Header("Player Persistence")]
    [Tooltip(
        "Optional explicit persistence key for this player. Blank means use " +
        "GameState.LocalPlayerPersistenceKey, which preserves current single-player behavior. " +
        "Future multiplayer bootstrap should assign each player a stable authenticated key.")]
    [SerializeField] private string playerPersistenceKey = "";

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    public string PersistenceKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(
                    playerPersistenceKey))
            {
                return
                    GameState.NormalizePlayerPersistenceKey(
                        playerPersistenceKey);
            }

            return
                GameState.I != null
                    ? GameState.I.LocalPlayerPersistenceKey
                    : GameState.DefaultPlayerPersistenceKey;
        }
    }

    public void SetPersistenceKey(
        string playerKey)
    {
        playerPersistenceKey =
            GameState.NormalizePlayerPersistenceKey(
                playerKey);
    }

    public PlayerBoardingState ResolveBoardingState()
    {
        return
            GetComponent<PlayerBoardingState>() ??
            GetComponentInParent<PlayerBoardingState>() ??
            GetComponentInChildren<PlayerBoardingState>(true);
    }

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (equipment == null)
            equipment = GetComponent<PlayerEquipment>();

        Log($"Awake | inventory={(inventory != null ? inventory.name : "NULL")} | equipment={(equipment != null ? equipment.name : "NULL")} | catalog={(itemCatalog != null ? itemCatalog.name : "NULL")}");
    }

    public PlayerLoadoutSnapshot CaptureSnapshot()
    {
        InventorySnapshot inv = inventory != null ? inventory.CaptureSnapshot() : null;
        EquipmentSnapshot eq = equipment != null ? equipment.CaptureSnapshot() : null;

        var snapshot = new PlayerLoadoutSnapshot
        {
            version = 1,
            inventory = inv,
            equipment = eq
        };

        Log(
            $"CaptureSnapshot | inventoryRef={(inventory != null ? "OK" : "NULL")} " +
            $"| equipmentRef={(equipment != null ? "OK" : "NULL")} " +
            $"| hotbar={DescribeInventory(inv)} " +
            $"| equip={DescribeEquipment(eq)}");

        return snapshot;
    }

    public void RestoreSnapshot(PlayerLoadoutSnapshot snapshot)
    {
        Log(
            $"RestoreSnapshot BEGIN | snapshot={(snapshot != null ? "OK" : "NULL")} " +
            $"| inventoryRef={(inventory != null ? "OK" : "NULL")} " +
            $"| equipmentRef={(equipment != null ? "OK" : "NULL")} " +
            $"| catalog={(itemCatalog != null ? itemCatalog.name : "NULL")} " +
            $"| hotbar={DescribeInventory(snapshot?.inventory)} " +
            $"| equip={DescribeEquipment(snapshot?.equipment)}");

        if (snapshot == null)
        {
            LogWarning("RestoreSnapshot called with NULL snapshot.");
        }

        if (itemCatalog == null)
        {
            LogError("RestoreSnapshot cannot resolve items because itemCatalog is NULL.");
        }

        // Restore equipment first so any equipped containers exist before UI starts sniffing around.
        if (equipment != null)
            equipment.RestoreSnapshot(snapshot?.equipment, itemCatalog);
        else
            LogWarning("RestoreSnapshot skipped equipment because equipment ref is NULL.");

        if (inventory != null)
            inventory.RestoreSnapshot(snapshot?.inventory, itemCatalog);
        else
            LogWarning("RestoreSnapshot skipped inventory because inventory ref is NULL.");

        Log(
            $"RestoreSnapshot END | inventoryNow={(inventory != null ? DescribeInventory(inventory.CaptureSnapshot()) : "NULL")} " +
            $"| equipmentNow={(equipment != null ? DescribeEquipment(equipment.CaptureSnapshot()) : "NULL")}");
    }

    public void SaveToGameState()
    {
        if (GameState.I == null)
        {
            LogError("SaveToGameState failed because GameState.I is NULL.");
            return;
        }

        string key =
            PersistenceKey;

        PlayerLoadoutSnapshot snapshot =
            CaptureSnapshot();

        GameState.I.SetPlayerLoadout(
            key,
            snapshot,
            $"PlayerLoadoutPersistence '{name}'");

        Log(
            $"SaveToGameState | key='{key}' gs={(GameState.I != null ? GameState.I.name : "NULL")} " +
            $"| storedHotbar={DescribeInventory(snapshot?.inventory)} " +
            $"| storedEquip={DescribeEquipment(snapshot?.equipment)}");
    }

    public void RestoreFromGameState()
    {
        if (GameState.I == null)
        {
            LogError("RestoreFromGameState failed because GameState.I is NULL.");
            return;
        }

        string key =
            PersistenceKey;

        PlayerLoadoutSnapshot snapshot =
            GameState.I.GetPlayerLoadout(
                key);

        Log(
            $"RestoreFromGameState | key='{key}' gs={(GameState.I != null ? GameState.I.name : "NULL")} " +
            $"| storedHotbar={DescribeInventory(snapshot?.inventory)} " +
            $"| storedEquip={DescribeEquipment(snapshot?.equipment)}");

        RestoreSnapshot(
            snapshot);
    }

    private string DescribeInventory(InventorySnapshot snapshot)
    {
        if (snapshot == null)
            return "NULL";

        int occupied = 0;
        int totalQuantity = 0;

        if (snapshot.hotbarSlots != null)
        {
            for (int i = 0; i < snapshot.hotbarSlots.Count; i++)
            {
                var s = snapshot.hotbarSlots[i];
                if (s == null || string.IsNullOrWhiteSpace(s.itemId))
                    continue;

                occupied++;
                totalQuantity += Mathf.Max(0, s.quantity);
            }
        }

        return $"slots={snapshot.hotbarSlotCount}, occupied={occupied}, qty={totalQuantity}, selected={snapshot.selectedSlot}";
    }

    private string DescribeEquipment(EquipmentSnapshot snapshot)
    {
        if (snapshot == null)
            return "NULL";

        int occupied = 0;
        if (HasItem(snapshot.hands)) occupied++;
        if (HasItem(snapshot.head)) occupied++;
        if (HasItem(snapshot.feet)) occupied++;
        if (HasItem(snapshot.toolbelt)) occupied++;
        if (HasItem(snapshot.backpack)) occupied++;
        if (HasItem(snapshot.body)) occupied++;

        return $"occupied={occupied}" +
               $" [hands={DescribeItem(snapshot.hands)}" +
               $", head={DescribeItem(snapshot.head)}" +
               $", feet={DescribeItem(snapshot.feet)}" +
               $", toolbelt={DescribeItem(snapshot.toolbelt)}" +
               $", backpack={DescribeItem(snapshot.backpack)}" +
               $", body={DescribeItem(snapshot.body)}]";
    }

    private bool HasItem(ItemInstanceSnapshot snapshot)
    {
        return snapshot != null && !string.IsNullOrWhiteSpace(snapshot.itemId);
    }

    private string DescribeItem(ItemInstanceSnapshot snapshot)
    {
        if (snapshot == null)
            return "empty";

        return $"{snapshot.itemId} x{snapshot.quantity} id={snapshot.instanceId}";
    }

    private void Log(string msg)
    {
        if (!verboseLogging) return;
        Debug.Log($"[PlayerLoadoutPersistence:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging) return;
        Debug.LogWarning($"[PlayerLoadoutPersistence:{name}] {msg}", this);
    }

    private void LogError(string msg)
    {
        Debug.LogError($"[PlayerLoadoutPersistence:{name}] {msg}", this);
    }
}