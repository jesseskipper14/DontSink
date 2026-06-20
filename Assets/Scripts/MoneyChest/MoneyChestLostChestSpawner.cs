using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MoneyChestLostChestSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    [Tooltip("Optional spawn anchor. If assigned, V1 spawns near this point instead of trusting old scene coordinates.")]
    [SerializeField] private Transform spawnAnchor;

    [Header("Fallback Item")]
    [SerializeField] private string fallbackMoneyChestItemId = "item_money_chest";

    [Header("Route Matching")]
    [Tooltip("If true, lost chest snapshots must match the current GameState.activeTravel route.")]
    [SerializeField] private bool requireCurrentRouteMatch = true;

    [Tooltip("If true, a chest lost on A→B can respawn while traveling B→A.")]
    [SerializeField] private bool allowReverseRouteMatch = true;

    [Tooltip("Debug convenience. Allows lost snapshots with no saved route to spawn in this scene.")]
    [SerializeField] private bool spawnRouteLessLostChestsForDebug = false;

    [Header("Spawn Placement")]
    [SerializeField] private bool preferAnchorOverSavedPosition = true;

    [SerializeField, Min(0f)] private float randomSpawnRadius = 1.5f;

    [Tooltip("If there is no anchor and the saved world position is near zero, use this fallback offset from this spawner.")]
    [SerializeField] private Vector2 fallbackLocalOffset = new Vector2(0f, -2f);

    [Header("Spawn Rules")]
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
            Log(
                $"Skipping lost chest spawn because live chest already exists. " +
                $"id='{snapshot.chestInstanceId}', reason='{reason}'");

            return false;
        }

        if (!MatchesCurrentRoute(snapshot))
        {
            Log(
                $"Skipping lost chest spawn because route does not match. " +
                $"id='{snapshot.chestInstanceId}', savedRoute='{snapshot.routeFromNodeId}'→'{snapshot.routeToNodeId}', " +
                $"currentRoute={DescribeCurrentRoute()}, reason='{reason}'");

            return false;
        }

        return true;
    }

    private bool TrySpawnLostChest(MoneyChestSnapshot snapshot, out MoneyChestState chest)
    {
        chest = null;

        if (snapshot == null)
            return false;

        string itemId = !string.IsNullOrWhiteSpace(snapshot.itemId)
            ? snapshot.itemId
            : fallbackMoneyChestItemId;

        if (itemCatalog == null)
        {
            LogWarning($"Cannot spawn lost chest '{snapshot.chestInstanceId}'. itemCatalog is NULL.");
            return false;
        }

        ItemDefinition definition = itemCatalog.Resolve(itemId);
        if (definition == null)
        {
            LogWarning(
                $"Cannot spawn lost chest '{snapshot.chestInstanceId}'. " +
                $"Could not resolve itemId='{itemId}'.");

            return false;
        }

        WorldItem prefab = definition.WorldPrefab;
        if (prefab == null)
        {
            LogWarning(
                $"Cannot spawn lost chest '{snapshot.chestInstanceId}'. " +
                $"ItemDefinition '{itemId}' has no WorldPrefab.");

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
            LogWarning(
                $"Cannot spawn lost chest '{snapshot.chestInstanceId}'. " +
                $"ItemInstance.FromSnapshot failed for itemId='{itemId}'.");

            return false;
        }

        Vector3 spawnPosition = ResolveSpawnPosition(snapshot);
        Quaternion spawnRotation = Quaternion.identity;

        WorldItem spawned = Instantiate(prefab, spawnPosition, spawnRotation);
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

        EnsureMoneyChestInteractable(spawned, chest);

        if (clearBoatOwnershipOnSpawn)
            ClearBoatOwnership(spawned);

        spawnedChestIds.Add(snapshot.chestInstanceId);

        Log(
            $"Spawned lost money chest. id='{snapshot.chestInstanceId}', itemId='{itemId}', " +
            $"balance={snapshot.balance}, pos={spawnPosition}, savedRoute='{snapshot.routeFromNodeId}'→'{snapshot.routeToNodeId}'");

        return true;
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

    private bool MatchesCurrentRoute(MoneyChestSnapshot snapshot)
    {
        if (!requireCurrentRouteMatch)
            return true;

        if (snapshot == null)
            return false;

        bool snapshotHasRoute =
            !string.IsNullOrWhiteSpace(snapshot.routeFromNodeId) ||
            !string.IsNullOrWhiteSpace(snapshot.routeToNodeId);

        if (!snapshotHasRoute)
            return spawnRouteLessLostChestsForDebug;

        GameState gs = GameState.I;
        TravelPayload travel = gs != null ? gs.activeTravel : null;

        if (travel == null)
            return false;

        bool direct =
            string.Equals(snapshot.routeFromNodeId, travel.fromNodeStableId, System.StringComparison.Ordinal) &&
            string.Equals(snapshot.routeToNodeId, travel.toNodeStableId, System.StringComparison.Ordinal);

        if (direct)
            return true;

        if (!allowReverseRouteMatch)
            return false;

        bool reverse =
            string.Equals(snapshot.routeFromNodeId, travel.toNodeStableId, System.StringComparison.Ordinal) &&
            string.Equals(snapshot.routeToNodeId, travel.fromNodeStableId, System.StringComparison.Ordinal);

        return reverse;
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

    private void EnsureMoneyChestInteractable(WorldItem spawned, MoneyChestState chest)
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

    private string DescribeCurrentRoute()
    {
        GameState gs = GameState.I;
        TravelPayload travel = gs != null ? gs.activeTravel : null;

        if (travel == null)
            return "NULL";

        return $"'{travel.fromNodeStableId}'→'{travel.toNodeStableId}'";
    }

    private static bool IsBasicallyZero(Vector2 value)
    {
        return Mathf.Abs(value.x) < 0.001f && Mathf.Abs(value.y) < 0.001f;
    }

    private void Log(string msg)
    {
        if (!logDebugMessages)
            return;

        Debug.Log($"[MoneyChestLostChestSpawner:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        Debug.LogWarning($"[MoneyChestLostChestSpawner:{name}] {msg}", this);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Spawn Matching Lost Chests Now")]
    private void DebugSpawnMatchingLostChestsNow()
    {
        int count = SpawnMatchingLostChests("Debug context menu");
        Debug.Log($"Spawned {count} lost money chest(es).", this);
    }

    [ContextMenu("Debug/Clear Spawned Id Cache")]
    private void DebugClearSpawnedIdCache()
    {
        spawnedChestIds.Clear();
        Debug.Log("Cleared spawned lost chest id cache.", this);
    }
#endif
}