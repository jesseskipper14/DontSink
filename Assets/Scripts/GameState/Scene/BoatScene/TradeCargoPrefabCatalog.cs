using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Trade/Trade Cargo Prefab Catalog", fileName = "TradeCargoPrefabCatalog")]
public sealed class TradeCargoPrefabCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string itemId;
        public GameObject cratePrefab;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    // itemId -> prefab (trade spawn)
    private readonly Dictionary<string, GameObject> _byItemId =
        new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    // typeGuid -> prefab (manifest restore)
    private readonly Dictionary<string, GameObject> _byTypeGuid =
        new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    private void OnEnable() => Rebuild();

    public void Rebuild()
    {
        _byItemId.Clear();
        _byTypeGuid.Clear();

        foreach (var e in entries)
        {
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.itemId)) continue;
            if (e.cratePrefab == null) continue;

            // ---- itemId map
            if (!_byItemId.ContainsKey(e.itemId))
                _byItemId.Add(e.itemId, e.cratePrefab);

            // ---- typeGuid map (optional but required for restore)
            var type = e.cratePrefab.GetComponent<CargoTypeIdentity>();
            if (type != null && !string.IsNullOrWhiteSpace(type.TypeGuid))
            {
                // If duplicates exist, first wins (consistent with itemId behavior).
                if (!_byTypeGuid.ContainsKey(type.TypeGuid))
                    _byTypeGuid.Add(type.TypeGuid, e.cratePrefab);
            }
        }
    }

    /// <summary>
    /// Resolves either:
    /// - itemId (e.g., "grain") for trade spawning
    /// - typeGuid (e.g., "3cadd8...") for manifest restore
    /// </summary>
    public GameObject Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        if (_byItemId.Count == 0 && _byTypeGuid.Count == 0)
            Rebuild();

        // First try itemId (existing behavior)
        if (_byItemId.TryGetValue(key, out var p))
            return p;

        // Then try typeGuid (new behavior)
        if (_byTypeGuid.TryGetValue(key, out p))
            return p;

        return null;
    }
}