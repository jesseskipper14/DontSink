using System;
using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

[DisallowMultipleComponent]
public sealed class PhysicalCrateItemStore : MonoBehaviour, IItemStore
{
    [Header("Refs")]
    public TradeCargoPrefabCatalog cargoPrefabs;
    public MarketCargoSpawnPoint spawnPoint;
    public MarketSellZone sellZone;

    [Header("Spawn")]
    [Min(1)] public int maxUnitsPerCrate = 10;
    [Min(0f)] public float spawnHorizontalStep = 0.45f;
    [Min(0f)] public float spawnVerticalStep = 0.05f;

    [Header("Visual Policy")]
    public CargoSortingPolicy sortingPolicy;
    [SerializeField] private string groundSortingLayer = "BoatCargo";
    [SerializeField] private int groundSortingOrder = 0;
    [SerializeField] private string heldSortingLayer = "BoatHeldCargo";
    [SerializeField] private int heldSortingOrder = 10;

    private void OnEnable()
    {
        var crates = FindObjectsByType<CargoCrate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < crates.Length; i++)
            ApplyPolicy(crates[i]);
    }

    public int GetCount(string itemId)
    {
        if (sellZone == null || string.IsNullOrWhiteSpace(itemId)) return 0;

        int total = 0;
        foreach (var c in sellZone.CratesInside)
        {
            if (c == null) continue;
            if (!string.Equals(c.itemId, itemId, StringComparison.OrdinalIgnoreCase)) continue;
            total += Mathf.Max(0, c.quantity);
        }
        return total;
    }

    public IEnumerable<KeyValuePair<string, int>> Enumerate()
    {
        if (sellZone == null) yield break;

        var sums = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in sellZone.CratesInside)
        {
            if (c == null) continue;
            if (string.IsNullOrWhiteSpace(c.itemId)) continue;

            int q = Mathf.Max(0, c.quantity);
            if (q <= 0) continue;

            if (sums.TryGetValue(c.itemId, out var cur)) sums[c.itemId] = cur + q;
            else sums[c.itemId] = q;
        }

        foreach (var kvp in sums)
            yield return kvp;
    }

    public void Add(string itemId, int amount)
    {
        Debug.Log($"[CrateStore] Add itemId={itemId} qty={amount} spawnPoint={(spawnPoint ? spawnPoint.name : "<null>")} catalog={(cargoPrefabs ? cargoPrefabs.name : "<null>")}");
        if (amount <= 0) return;
        if (cargoPrefabs == null || spawnPoint == null)
        {
            Debug.LogError("[PhysicalCrateItemStore] Missing cargoPrefabs or spawnPoint.");
            return;
        }

        var prefab = cargoPrefabs.Resolve(itemId);
        if (prefab == null)
        {
            Debug.LogError($"[PhysicalCrateItemStore] No crate prefab mapped for itemId='{itemId}'.");
            return;
        }
        Debug.Log($"[CrateStore] Resolved prefab '{prefab.name}' for itemId='{itemId}'");

        int remaining = amount;
        int spawned = 0;

        while (remaining > 0)
        {
            int chunk = Mathf.Clamp(remaining, 1, Mathf.Max(1, maxUnitsPerCrate));
            remaining -= chunk;

            Vector3 basePos = spawnPoint.transform.position;
            Vector3 pos = basePos + new Vector3(spawned * spawnHorizontalStep, spawned * spawnVerticalStep, 0f);

            var go = Instantiate(prefab, pos, Quaternion.identity);
            var crate = go.GetComponent<CargoCrate>();
            if (crate == null) crate = go.AddComponent<CargoCrate>();

            crate.itemId = itemId;
            crate.quantity = chunk;

            ApplyPolicy(crate);
            crate.ApplyGroundSorting();

            spawned++;
        }
    }

    public bool Remove(string itemId, int amount)
    {
        if (amount <= 0) return true;
        if (sellZone == null) return false;

        int have = GetCount(itemId);
        if (have < amount) return false;

        int remaining = amount;
        var crates = sellZone.SnapshotList();

        // Consume from smallest stacks first (helps reduce partial leftovers)
        crates.Sort((a, b) => (a?.quantity ?? 0).CompareTo(b?.quantity ?? 0));

        for (int i = 0; i < crates.Count && remaining > 0; i++)
        {
            var c = crates[i];
            if (c == null) continue;
            if (!string.Equals(c.itemId, itemId, StringComparison.OrdinalIgnoreCase)) continue;

            int take = Mathf.Min(Mathf.Max(0, c.quantity), remaining);
            if (take <= 0) continue;

            c.quantity -= take;
            remaining -= take;

            if (c.quantity <= 0)
            {
                Destroy(c.gameObject);
            }
        }

        return remaining <= 0;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        ApplySortingToAllCratesInScene();
    }
#endif

    private void ApplySortingToAllCratesInScene()
    {
        var crates = FindObjectsByType<CargoCrate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < crates.Length; i++)
        {
            crates[i].ConfigureSorting(groundSortingLayer, groundSortingOrder, heldSortingLayer, heldSortingOrder);
        }
    }

    private bool TryGetPolicy(out CargoSortingPolicy p)
    {
        p = sortingPolicy;
        if (p != null) return true;

        Debug.LogError("[PhysicalCrateItemStore] Missing sortingPolicy.");
        return false;
    }

    private void ApplyPolicy(CargoCrate crate)
    {
        if (crate == null) return;
        if (!TryGetPolicy(out var p)) return;

        crate.ConfigureSorting(
            p.GroundSortingLayer, p.GroundSortingOrder,
            p.HeldSortingLayer, p.HeldSortingOrder
        );
    }
}
