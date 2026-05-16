using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CargoItemCatalog",
    menuName = "Trade/Cargo Item Catalog")]
public sealed class CargoItemCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("Market/world-map commodity id, e.g. grain, fuel, wood.")]
        public string tradeItemId;

        [Tooltip("Inventory item definition, e.g. cargo_grain_crate.")]
        public ItemDefinition cargoItemDefinition;
    }

    [SerializeField] private List<Entry> entries = new();

    private readonly Dictionary<string, ItemDefinition> _byTradeId =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _tradeIdByCargoItemId =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _built;

    private void OnEnable()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        _byTradeId.Clear();
        _tradeIdByCargoItemId.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e == null)
                continue;

            if (string.IsNullOrWhiteSpace(e.tradeItemId))
                continue;

            if (e.cargoItemDefinition == null)
                continue;

            string tradeId = e.tradeItemId.Trim();
            ItemDefinition def = e.cargoItemDefinition;

            if (!_byTradeId.ContainsKey(tradeId))
                _byTradeId.Add(tradeId, def);

            if (!string.IsNullOrWhiteSpace(def.ItemId) &&
                !_tradeIdByCargoItemId.ContainsKey(def.ItemId))
            {
                _tradeIdByCargoItemId.Add(def.ItemId, tradeId);
            }
        }

        _built = true;
    }

    public ItemDefinition ResolveCargoItem(string tradeItemId)
    {
        if (!_built)
            Rebuild();

        if (string.IsNullOrWhiteSpace(tradeItemId))
            return null;

        _byTradeId.TryGetValue(tradeItemId, out ItemDefinition def);
        return def;
    }

    public bool TryGetTradeItemId(ItemDefinition cargoItemDefinition, out string tradeItemId)
    {
        tradeItemId = null;

        if (!_built)
            Rebuild();

        if (cargoItemDefinition == null || string.IsNullOrWhiteSpace(cargoItemDefinition.ItemId))
            return false;

        return _tradeIdByCargoItemId.TryGetValue(cargoItemDefinition.ItemId, out tradeItemId);
    }

    public bool IsKnownCargoItem(ItemDefinition definition)
    {
        return TryGetTradeItemId(definition, out _);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _built = false;
    }
#endif
}