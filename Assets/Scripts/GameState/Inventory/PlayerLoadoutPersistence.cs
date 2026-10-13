using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerLoadoutPersistence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;
    public ItemDefinitionCatalog ItemCatalog => itemCatalog;

    [Header("Player Persistence")]
    [Tooltip(
        "Optional explicit persistence key. A sole active player binds the local key before use. " +
        "Multiple players require distinct keys assigned before restore; duplicate keys are rejected.")]
    [SerializeField] private string playerPersistenceKey = "";

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    public string PersistenceKey
    {
        get => TryBindPersistenceIdentity(out string key) ? key : null;
    }

    // The local-key compatibility path belongs here, before any capture/restore.
    // Multiple actors must be assigned explicit, distinct keys by their bootstrap.
    public bool TryBindPersistenceIdentity(out string key)
    {
        key = null;
        var players = Object.FindObjectsByType<PlayerLoadoutPersistence>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (string.IsNullOrWhiteSpace(playerPersistenceKey))
        {
            if (GameState.I == null || players.Length != 1 || players[0] != this)
                return false;
            playerPersistenceKey = GameState.I.LocalPlayerPersistenceKey;
        }

        string candidateKey = GameState.NormalizePlayerPersistenceKey(playerPersistenceKey);
        foreach (var player in players)
        {
            if (player == this) continue;
            // An unbound actor must not implicitly share this actor's local key.
            string otherKey = string.IsNullOrWhiteSpace(player.playerPersistenceKey)
                ? GameState.I?.LocalPlayerPersistenceKey
                : GameState.NormalizePlayerPersistenceKey(player.playerPersistenceKey);
            if (candidateKey == otherKey) return false;
        }
        key = candidateKey;
        return true;
    }

    public void SetPersistenceKey(
        string playerKey)
    {
        if (string.IsNullOrWhiteSpace(playerKey))
        {
            LogWarning("Cannot bind a blank player persistence key.");
            return;
        }
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

    private void PrepareTransientInventoryStateForCapture()
    {
        InventoryDragController[] dragControllers =
            Object.FindObjectsByType<InventoryDragController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        if (dragControllers == null ||
            dragControllers.Length == 0)
        {
            return;
        }

        InventoryDragController exactActiveMatch =
            null;

        int exactActiveMatchCount =
            0;

        int activeDragCount =
            0;

        for (int i = 0;
             i < dragControllers.Length;
             i++)
        {
            InventoryDragController dragController =
                dragControllers[i];

            if (dragController == null)
                continue;

            bool hasActiveDrag =
                dragController.IsDragging ||
                dragController.DraggedItem != null;

            if (!hasActiveDrag)
                continue;

            activeDragCount++;

            if (inventory != null &&
                dragController.BoundInventory == inventory)
            {
                exactActiveMatch =
                    dragController;

                exactActiveMatchCount++;
            }

            Log(
                $"PrepareTransientInventoryStateForCapture | candidate='{dragController.name}' " +
                $"active={hasActiveDrag} " +
                $"boundInventory={(dragController.BoundInventory != null ? dragController.BoundInventory.name : "NULL")} " +
                $"boundInventoryId={(dragController.BoundInventory != null ? dragController.BoundInventory.GetInstanceID() : 0)} " +
                $"persistenceInventory={(inventory != null ? inventory.name : "NULL")} " +
                $"persistenceInventoryId={(inventory != null ? inventory.GetInstanceID() : 0)}");
        }

        InventoryDragController chosen =
            null;

        string resolutionReason =
            null;

        if (exactActiveMatchCount == 1)
        {
            chosen =
                exactActiveMatch;

            resolutionReason =
                "exact PlayerInventory ownership";
        }
        else if (exactActiveMatchCount > 1)
        {
            LogError(
                $"PrepareTransientInventoryStateForCapture found {exactActiveMatchCount} active drag controllers " +
                "bound to the same PlayerInventory. Refusing ambiguous persistence reconciliation.");
            return;
        }
        else if (activeDragCount > 0)
        {
            LogError(
                $"PrepareTransientInventoryStateForCapture found {activeDragCount} active drag controllers " +
                "and none matched this PlayerInventory exactly. Refusing to guess.");
            return;
        }
        else
        {
            return;
        }

        bool resolved =
            chosen.PrepareForPersistenceCapture();

        if (!resolved)
        {
            LogError(
                $"PrepareTransientInventoryStateForCapture could not reconcile active drag via " +
                $"{resolutionReason}. The dragged item remains authoritative in InventoryDragController " +
                "and was NOT world-dropped.");
            return;
        }

        Log(
            $"PrepareTransientInventoryStateForCapture | active drag reconciled via {resolutionReason} before snapshot.");
    }

    public PlayerLoadoutSnapshot CaptureSnapshot()
    {
        PrepareTransientInventoryStateForCapture();

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
        if (!TryBindPersistenceIdentity(out _))
        {
            LogWarning("Skipped restore: bind a distinct player persistence key first.");
            return;
        }
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

        if (!TryBindPersistenceIdentity(out string key))
        {
            LogWarning("Skipped loadout operation: bind a distinct player persistence key first.");
            return;
        }

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

        if (!TryBindPersistenceIdentity(out string key))
        {
            LogWarning("Skipped loadout operation: bind a distinct player persistence key first.");
            return;
        }

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
