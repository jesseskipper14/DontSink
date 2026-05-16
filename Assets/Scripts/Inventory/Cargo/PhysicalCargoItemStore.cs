using System;
using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

[DisallowMultipleComponent]
public sealed class PhysicalCargoItemStore : MonoBehaviour, IItemStore
{
    [Header("Refs")]
    [SerializeField] private CargoItemCatalog cargoItemCatalog;
    [SerializeField] private MarketCargoSpawnPoint spawnPoint;
    [SerializeField] private MarketSellZone sellZone;

    [Header("Spawn")]
    [Min(0f)]
    [SerializeField] private float spawnHorizontalStep = 0.45f;

    [Min(0f)]
    [SerializeField] private float spawnVerticalStep = 0.05f;

    [Header("Sell Zone Scan")]
    [SerializeField] private int maxOverlapHits = 256;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private readonly List<WorldItem> _scratchItems = new();
    private readonly HashSet<WorldItem> _scratchUnique = new();

    public int GetCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        int total = 0;
        CollectCargoWorldItemsInSellZone(_scratchItems);

        for (int i = 0; i < _scratchItems.Count; i++)
        {
            WorldItem worldItem = _scratchItems[i];
            ItemInstance inst = worldItem != null ? worldItem.Instance : null;

            if (!TryGetTradeItemId(inst, out string tradeId))
                continue;

            if (!string.Equals(tradeId, itemId, StringComparison.OrdinalIgnoreCase))
                continue;

            // Cargo is intended to be maxStack 1, but this makes the store tolerant.
            total += Mathf.Max(0, inst.Quantity);
        }

        return total;
    }

    public IEnumerable<KeyValuePair<string, int>> Enumerate()
    {
        Dictionary<string, int> sums = new(StringComparer.OrdinalIgnoreCase);

        CollectCargoWorldItemsInSellZone(_scratchItems);

        for (int i = 0; i < _scratchItems.Count; i++)
        {
            WorldItem worldItem = _scratchItems[i];
            ItemInstance inst = worldItem != null ? worldItem.Instance : null;

            if (!TryGetTradeItemId(inst, out string tradeId))
                continue;

            int qty = Mathf.Max(0, inst.Quantity);
            if (qty <= 0)
                continue;

            if (sums.TryGetValue(tradeId, out int current))
                sums[tradeId] = current + qty;
            else
                sums.Add(tradeId, qty);
        }

        foreach (KeyValuePair<string, int> kvp in sums)
            yield return kvp;
    }

    public void Add(string itemId, int amount)
    {
        if (amount <= 0)
            return;

        if (cargoItemCatalog == null)
        {
            Debug.LogError("[PhysicalCargoItemStore] Missing CargoItemCatalog.", this);
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogError("[PhysicalCargoItemStore] Missing MarketCargoSpawnPoint.", this);
            return;
        }

        ItemDefinition cargoDef = cargoItemCatalog.ResolveCargoItem(itemId);
        if (cargoDef == null)
        {
            Debug.LogError($"[PhysicalCargoItemStore] No cargo ItemDefinition mapped for trade itemId='{itemId}'.", this);
            return;
        }

        if (cargoDef.WorldPrefab == null)
        {
            Debug.LogError($"[PhysicalCargoItemStore] Cargo item '{cargoDef.DisplayName}' has no WorldPrefab.", cargoDef);
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            Vector3 basePos = spawnPoint.transform.position;
            Vector3 pos = basePos + new Vector3(i * spawnHorizontalStep, i * spawnVerticalStep, 0f);

            ItemInstance instance = ItemInstance.Create(cargoDef, 1);

            WorldItem worldItem = Instantiate(
                cargoDef.WorldPrefab,
                pos,
                Quaternion.identity);

            worldItem.Initialize(instance);

            // Bought cargo starts as world/node cargo, not boat-owned.
            BoatOwnedItem owned = worldItem.GetComponent<BoatOwnedItem>();
            if (owned != null)
                owned.ClearOwnership();

            Log($"Spawned cargo item '{cargoDef.DisplayName}' for trade itemId='{itemId}' at {pos}.");
        }
    }

    public bool Remove(string itemId, int amount)
    {
        if (amount <= 0)
            return true;

        int have = GetCount(itemId);
        if (have < amount)
            return false;

        CollectCargoWorldItemsInSellZone(_scratchItems);

        // Smallest quantities first, in case we ever allow quantity > 1.
        _scratchItems.Sort((a, b) =>
        {
            int aq = a != null && a.Instance != null ? a.Instance.Quantity : 0;
            int bq = b != null && b.Instance != null ? b.Instance.Quantity : 0;
            return aq.CompareTo(bq);
        });

        int remaining = amount;

        for (int i = 0; i < _scratchItems.Count && remaining > 0; i++)
        {
            WorldItem worldItem = _scratchItems[i];
            if (worldItem == null)
                continue;

            ItemInstance inst = worldItem.Instance;
            if (inst == null || inst.Definition == null)
                continue;

            if (!TryGetTradeItemId(inst, out string tradeId))
                continue;

            if (!string.Equals(tradeId, itemId, StringComparison.OrdinalIgnoreCase))
                continue;

            int available = Mathf.Max(0, inst.Quantity);
            int take = Mathf.Min(available, remaining);

            if (take <= 0)
                continue;

            inst.RemoveQuantity(take);
            remaining -= take;

            if (inst.IsDepleted())
                Destroy(worldItem.gameObject);
        }

        return remaining <= 0;
    }

    private bool TryGetTradeItemId(ItemInstance instance, out string tradeItemId)
    {
        tradeItemId = null;

        if (instance == null || instance.Definition == null)
            return false;

        if (cargoItemCatalog == null)
            return false;

        return cargoItemCatalog.TryGetTradeItemId(instance.Definition, out tradeItemId);
    }

    private void CollectCargoWorldItemsInSellZone(List<WorldItem> results)
    {
        results.Clear();
        _scratchUnique.Clear();

        if (sellZone == null)
            return;

        Collider2D zoneCollider = sellZone.GetComponent<Collider2D>();
        if (zoneCollider == null)
        {
            LogWarning("Sell zone has no Collider2D.");
            return;
        }

        Collider2D[] hits = new Collider2D[Mathf.Max(8, maxOverlapHits)];

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        int count = zoneCollider.Overlap(filter, hits);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            WorldItem worldItem =
                hit.GetComponentInParent<WorldItem>() ??
                hit.GetComponentInChildren<WorldItem>(true);

            if (worldItem == null)
                continue;

            ItemInstance inst = worldItem.Instance;
            if (inst == null || inst.Definition == null)
                continue;

            if (!cargoItemCatalog.IsKnownCargoItem(inst.Definition))
                continue;

            if (_scratchUnique.Add(worldItem))
                results.Add(worldItem);
        }
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[PhysicalCargoItemStore:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[PhysicalCargoItemStore:{name}] {msg}", this);
    }
}