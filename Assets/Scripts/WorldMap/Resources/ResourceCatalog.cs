using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Trade/Resource Catalog", fileName = "ResourceCatalog")]
public sealed class ResourceCatalog : ScriptableObject
{
    [SerializeField] private List<ResourceDef> resources = new List<ResourceDef>();

    private readonly Dictionary<string, ResourceDef> _byId =
        new Dictionary<string, ResourceDef>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ResourceDef> Resources => resources;

    public void RebuildIndex()
    {
        _byId.Clear();

        for (int i = 0; i < resources.Count; i++)
        {
            var r = resources[i];
            if (r == null) continue;

            if (string.IsNullOrWhiteSpace(r.itemId))
            {
                Debug.LogWarning($"[ResourceCatalog] Resource '{r.name}' has empty itemId.");
                continue;
            }

            if (_byId.ContainsKey(r.itemId))
            {
                Debug.LogWarning($"[ResourceCatalog] Duplicate itemId '{r.itemId}'. Keeping first.");
                continue;
            }

            _byId.Add(r.itemId, r);
        }
    }

    public bool TryGet(string itemId, out ResourceDef def)
    {
        if (_byId.Count == 0) RebuildIndex();
        return _byId.TryGetValue(itemId, out def);
    }

    public ResourceDef GetOrNull(string itemId)
    {
        return TryGet(itemId, out var d) ? d : null;
    }
}
