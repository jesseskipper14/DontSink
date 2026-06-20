using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MoneyChestNodeLostChestSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    [Tooltip("Optional spawn anchor for this node/dock. If assigned, lost node chests spawn near it.")]
    [SerializeField] private Transform spawnAnchor;

    [Header("Fallback Item")]
    [SerializeField] private string fallbackMoneyChestItemId = "item_money_chest";

    [Header("Spawn Matching")]
    [SerializeField] private bool requireCurrentNodeMatch = true;

    [Tooltip("Debug convenience. Allows lost node chests with no nodeStableId to spawn here.")]
    [SerializeField] private bool spawnNodeLessLostChestsForDebug = false;

    [Header("Placement")]
    [SerializeField] private bool preferAnchorOverSavedPosition = true;
    [SerializeField, Min(0f)] private float randomSpawnRadius = 1.25f;
    [SerializeField] private Vector2 fallbackLocalOffset = new Vector2(0f, 1f);

    [Header("Rules")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool skipIfLiveChestAlreadyExists = true;
    [SerializeField] private bool clearBoatOwnershipOnSpawn = true;

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages = true;

    private readonly HashSet<string> spawnedChestIds = new();

    private void Start()
    {
        if (spawnOnStart)
            SpawnMatchingLostChests("Start");
    }

    public int SpawnMatchingLostChests(string reason = "")
    {
        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury == null)
        {
            LogWarning($"Spawn skipped: no MoneyChestTreasuryService exists. reason='{reason}'");
            return 0;
        }

        IReadOnlyList<MoneyChestSnapshot> snapshots = treasury.GetChestSnapshots();
        if (snapshots == null || snapshots.Count == 0)
        {
            Log($"Spawn skipped: no money chest snapshots. reason='{reason}'");
            return 0;
        }

        int spawned = 0;

        for (int i = 0; i < snapshots.Count; i++)
        {
            MoneyChestSnapshot snapshot = snapshots[i];

            if (!ShouldSpawnSnapshot(snapshot, reason))
                continue;

            if (TrySpawnLostChest(snapshot, out MoneyChestState chest))
            {
                spawned++;

                if (chest != null)
                    treasury.RegisterChest(chest);
            }
        }

        Log($"SpawnMatchingLostChests complete. spawned={spawned}, reason='{reason}'");
        return spawned;
    }

    private bool ShouldSpawnSnapshot(MoneyChestSnapshot snapshot, string reason)
    {
        if (snapshot == null)
            return false;

        if (!snapshot.IsLost)
            return false;

        if (string.IsNullOrWhiteSpace(snapshot.chestInstanceId))
            return false;

        if (spawnedChestIds.Contains(snapshot.chestInstanceId))
            return false;

        if (skipIfLiveChestAlreadyExists && LiveChestExists(snapshot.chestInstanceId))
        {
            Log($"Skipping lost node chest because live chest already exists. id='{snapshot.chestInstanceId}', reason='{reason}'");
            return false;
        }

        if (!MatchesCurrentNode(snapshot))
        {
            Log(
                $"Skipping lost node chest because node does not match. " +
                $"id='{snapshot.chestInstanceId}', savedNode='{snapshot.nodeStableId}', " +
                $"currentNode='{CurrentNodeId}', reason='{reason}'");

            return false;
        }

        return true;
    }

    private bool TrySpawnLostChest(MoneyChestSnapshot snapshot, out MoneyChestState chest)
    {
        chest = null;

        string itemId = !string.IsNullOrWhiteSpace(snapshot.itemId)
            ? snapshot.itemId
            : fallbackMoneyChestItemId;

        if (itemCatalog == null)
        {
            LogWarning($"Cannot spawn lost node chest '{snapshot.chestInstanceId}'. itemCatalog is NULL.");
            return false;
        }

        ItemDefinition definition = itemCatalog.Resolve(itemId);
        if (definition == null)
        {
            LogWarning($"Cannot spawn lost node chest '{snapshot.chestInstanceId}'. Could not resolve itemId='{itemId}'.");
            return false;
        }

        WorldItem prefab = definition.WorldPrefab;
        if (prefab == null)
        {
            LogWarning($"Cannot spawn lost node chest '{snapshot.chestInstanceId}'. ItemDefinition '{itemId}' has no WorldPrefab.");
            return false;
        }

        ItemInstanceSnapshot itemSnapshot = new ItemInstanceSnapshot
        {
            version = 1,
            instanceId = snapshot.chestInstanceId,
            itemId = itemId,
            quantity = 1,
            currentCharges = 0,
            container = null
        };

        ItemInstance itemInstance = ItemInstance.FromSnapshot(itemSnapshot, itemCatalog);
        if (itemInstance == null)
        {
            LogWarning($"Cannot spawn lost node chest '{snapshot.chestInstanceId}'. ItemInstance.FromSnapshot failed.");
            return false;
        }

        Vector3 spawnPosition = ResolveSpawnPosition(snapshot);
        WorldItem spawned = Instantiate(prefab, spawnPosition, Quaternion.identity);
        spawned.Initialize(itemInstance);

        chest = spawned.GetComponent<MoneyChestState>();
        if (chest == null)
            chest = spawned.GetComponentInChildren<MoneyChestState>(true);

        if (chest == null)
            chest = spawned.gameObject.AddComponent<MoneyChestState>();

        chest.RestoreState(
            snapshot.chestInstanceId,
            snapshot.balance,
            MoneyChestLifecycleState.Lost);

        EnsureMoneyChestInteractable(spawned);

        if (clearBoatOwnershipOnSpawn)
            ClearBoatOwnership(spawned);

        spawnedChestIds.Add(snapshot.chestInstanceId);

        Log(
            $"Spawned lost node money chest. id='{snapshot.chestInstanceId}', itemId='{itemId}', " +
            $"balance={snapshot.balance}, node='{snapshot.nodeStableId}', pos={spawnPosition}");

        return true;
    }

    private bool MatchesCurrentNode(MoneyChestSnapshot snapshot)
    {
        if (!requireCurrentNodeMatch)
            return true;

        string currentNode = CurrentNodeId;

        bool snapshotHasNode = !string.IsNullOrWhiteSpace(snapshot.nodeStableId);
        if (!snapshotHasNode)
            return spawnNodeLessLostChestsForDebug;

        if (string.IsNullOrWhiteSpace(currentNode))
            return false;

        return string.Equals(snapshot.nodeStableId, currentNode, System.StringComparison.Ordinal);
    }

    private string CurrentNodeId
    {
        get
        {
            GameState gs = GameState.I;
            return gs != null && gs.player != null ? gs.player.currentNodeId : null;
        }
    }

    private Vector3 ResolveSpawnPosition(MoneyChestSnapshot snapshot)
    {
        Vector3 basePosition;

        if (preferAnchorOverSavedPosition && spawnAnchor != null)
        {
            basePosition = spawnAnchor.position;
        }
        else if (!IsBasicallyZero(snapshot.lastWorldPosition))
        {
            basePosition = snapshot.lastWorldPosition;
        }
        else if (spawnAnchor != null)
        {
            basePosition = spawnAnchor.position;
        }
        else
        {
            basePosition = transform.position + (Vector3)fallbackLocalOffset;
        }

        if (randomSpawnRadius <= 0f)
            return basePosition;

        Vector2 random = Random.insideUnitCircle * randomSpawnRadius;
        return basePosition + new Vector3(random.x, random.y, 0f);
    }

    private bool LiveChestExists(string chestInstanceId)
    {
        if (string.IsNullOrWhiteSpace(chestInstanceId))
            return false;

        MoneyChestState[] liveChests = FindObjectsByType<MoneyChestState>(FindObjectsSortMode.None);

        for (int i = 0; i < liveChests.Length; i++)
        {
            MoneyChestState chest = liveChests[i];
            if (chest == null)
                continue;

            chest.SyncInstanceIdFromWorldItem();

            if (chest.ChestInstanceId == chestInstanceId)
                return true;
        }

        return false;
    }

    private void EnsureMoneyChestInteractable(WorldItem spawned)
    {
        if (spawned == null)
            return;

        MoneyChestInteractable interactable = spawned.GetComponent<MoneyChestInteractable>();
        if (interactable == null)
            interactable = spawned.GetComponentInChildren<MoneyChestInteractable>(true);

        if (interactable == null)
            spawned.gameObject.AddComponent<MoneyChestInteractable>();
    }

    private void ClearBoatOwnership(WorldItem spawned)
    {
        if (spawned == null)
            return;

        BoatOwnedItem owned = spawned.GetComponent<BoatOwnedItem>();
        if (owned != null)
            owned.ClearOwnership();
    }

    private static bool IsBasicallyZero(Vector2 value)
    {
        return Mathf.Abs(value.x) < 0.001f && Mathf.Abs(value.y) < 0.001f;
    }

    private void Log(string msg)
    {
        if (!logDebugMessages)
            return;

        Debug.Log($"[MoneyChestNodeLostChestSpawner:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        Debug.LogWarning($"[MoneyChestNodeLostChestSpawner:{name}] {msg}", this);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Spawn Matching Lost Node Chests Now")]
    private void DebugSpawnMatchingLostChestsNow()
    {
        int count = SpawnMatchingLostChests("Debug context menu");
        Debug.Log($"Spawned {count} lost node money chest(es).", this);
    }

    [ContextMenu("Debug/Clear Spawned Id Cache")]
    private void DebugClearSpawnedIdCache()
    {
        spawnedChestIds.Clear();
        Debug.Log("Cleared spawned lost node chest id cache.", this);
    }
#endif
}