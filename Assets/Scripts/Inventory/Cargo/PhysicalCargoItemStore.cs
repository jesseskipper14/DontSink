using System;
using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

[DisallowMultipleComponent]
public sealed class PhysicalCargoItemStore : MonoBehaviour, ITransactionalTradeItemStore
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

    public bool TryPrepareTrade(IReadOnlyList<TradeLine> lines, out IPreparedTradeItems prepared, out string failNote)
    {
        prepared = null;
        failNote = null;
        var plan = new PreparedTrade(this);
        try
        {
            CollectCargoWorldItemsInSellZone(_scratchItems);
            foreach (var line in lines)
            {
                if (line.direction != TradeDirection.SellToNode) continue;
                int remaining = line.quantity;
                foreach (var worldItem in _scratchItems)
                {
                    var instance = worldItem.Instance;
                    if (!TryGetTradeItemId(instance, out string id) ||
                        !string.Equals(id, line.itemId, StringComparison.OrdinalIgnoreCase)) continue;
                    plan.sales.TryGetValue(worldItem, out var sale);
                    if (sale == null)
                        sale = new ReservedSale(worldItem, instance);
                    int take = Mathf.Min(remaining, sale.quantityBefore - sale.amount);
                    if (take <= 0) continue;
                    sale.amount += take;
                    plan.sales[worldItem] = sale;
                    remaining -= take;
                    if (remaining == 0) break;
                }
                if (remaining != 0)
                {
                    failNote = $"Not enough '{line.itemId}' in the sell zone.";
                    plan.Dispose();
                    return false;
                }
            }

            int spawnIndex = 0;
            foreach (var line in lines)
            {
                if (line.direction != TradeDirection.BuyFromNode) continue;
                var definition = cargoItemCatalog != null ? cargoItemCatalog.ResolveCargoItem(line.itemId) : null;
                if (spawnPoint == null || definition == null || definition.WorldPrefab == null)
                {
                    failNote = $"Cannot deliver '{line.itemId}': missing cargo mapping, prefab or spawn point.";
                    plan.Dispose();
                    return false;
                }
                if (plan.staging == null)
                {
                    plan.staging = new GameObject("_PreparedMarketCargo");
                    plan.staging.SetActive(false);
                }
                for (int i = 0; i < line.quantity; i++, spawnIndex++)
                {
                    Vector3 position = spawnPoint.transform.position +
                        new Vector3(spawnIndex * spawnHorizontalStep, spawnIndex * spawnVerticalStep, 0f);
                    // The inactive parent prevents Awake/OnEnable from observing a half-applied trade.
                    var cargo = Instantiate(definition.WorldPrefab, position, Quaternion.identity, plan.staging.transform);
                    plan.purchases.Add(cargo);
                    cargo.Initialize(ItemInstance.Create(definition, 1));
                    var owned = cargo.GetComponent<BoatOwnedItem>();
                    if (owned != null) owned.ClearOwnership();
                }
            }
            prepared = plan;
            return true;
        }
        catch
        {
            plan.Dispose();
            throw;
        }
    }

    private sealed class ReservedSale
    {
        public readonly WorldItem cargo;
        public readonly ItemInstance instance;
        public readonly int quantityBefore;
        public int amount;

        public ReservedSale(WorldItem cargo, ItemInstance instance)
        {
            this.cargo = cargo; this.instance = instance; quantityBefore = instance.Quantity;
        }
    }

    private sealed class PreparedTrade : IPreparedTradeItems
    {
        private readonly PhysicalCargoItemStore owner;
        internal readonly Dictionary<WorldItem, ReservedSale> sales = new();
        internal readonly List<WorldItem> purchases = new();
        internal GameObject staging;
        private bool applied, completed;

        public PreparedTrade(PhysicalCargoItemStore owner) => this.owner = owner;

        public bool Validate(out string failNote)
        {
            failNote = "Cargo changed while preparing trade.";
            if (owner == null) return false;
            owner.CollectCargoWorldItemsInSellZone(owner._scratchItems);
            foreach (var sale in sales.Values)
                if (sale.cargo == null || sale.cargo.Instance != sale.instance || sale.instance.IsDepleted() ||
                    sale.instance.Quantity != sale.quantityBefore || !owner._scratchItems.Contains(sale.cargo)) return false;
            foreach (var cargo in purchases)
                if (cargo == null || cargo.Instance == null || cargo.Instance.IsDepleted()) return false;
            failNote = null;
            return true;
        }

        public void Apply()
        {
            applied = true;
            foreach (var sale in sales.Values) sale.instance.RemoveQuantityForTransaction(sale.amount);
        }

        public void Complete() => completed = true;

        public void Publish()
        {
            foreach (var sale in sales.Values)
            {
                if (sale.instance.IsDepleted())
                {
                    sale.cargo.gameObject.SetActive(false);
                    Destroy(sale.cargo.gameObject);
                }
                else sale.instance.PublishTransactionChange();
            }
            foreach (var cargo in purchases)
            {
                cargo.transform.SetParent(null, true);
                cargo.gameObject.SetActive(true);
            }
        }

        public void Dispose()
        {
            if (applied && !completed)
                foreach (var sale in sales.Values) sale.instance.RestoreQuantityForTransaction(sale.quantityBefore);
            if (staging != null) Destroy(staging);
        }
    }

    private bool TryGetTradeItemId(ItemInstance instance, out string tradeItemId)
    {
        tradeItemId = null;

        if (instance == null || instance.Definition == null || instance.IsDepleted())
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
            if (inst == null || inst.Definition == null || inst.IsDepleted())
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
