using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/POIs/POI Catalog",
    fileName = "WorldMapPOICatalog")]
public sealed class WorldMapPOICatalog : ScriptableObject
{
    [Header("POI Definitions")]
    public List<WorldMapPOIDef> poiDefs = new();

    [Tooltip("Used only if an instance references a missing POI definition.")]
    public WorldMapPOIDef fallbackDef;

    public int Count => poiDefs != null ? poiDefs.Count : 0;

    public WorldMapPOIDef GetAt(int index)
    {
        if (poiDefs == null || index < 0 || index >= poiDefs.Count)
            return fallbackDef;

        return poiDefs[index] != null ? poiDefs[index] : fallbackDef;
    }

    public WorldMapPOIDef GetById(string poiId)
    {
        if (string.IsNullOrWhiteSpace(poiId) || poiDefs == null)
            return fallbackDef;

        for (int i = 0; i < poiDefs.Count; i++)
        {
            WorldMapPOIDef def = poiDefs[i];
            if (def != null && def.poiId == poiId)
                return def;
        }

        return fallbackDef;
    }

    public int IndexOfId(string poiId)
    {
        if (string.IsNullOrWhiteSpace(poiId) || poiDefs == null)
            return -1;

        for (int i = 0; i < poiDefs.Count; i++)
        {
            WorldMapPOIDef def = poiDefs[i];
            if (def != null && def.poiId == poiId)
                return i;
        }

        return -1;
    }
}
