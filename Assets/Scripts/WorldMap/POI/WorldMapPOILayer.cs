using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WorldMapPOIInstance
{
    [Header("Stable Identity")]
    public string stableId;

    [Tooltip("Stable ID of the WorldMapPOIDef asset used to generate this POI.")]
    public string poiDefId;

    public string displayName;
    public Vector2 position;

    [Header("Generation Data")]
    [Range(0f, 1f)] public float height01;
    [Range(0f, 1f)] public float depth01;
    public float score;

    [Header("Runtime State")]
    public bool discovered;
    public bool surveyed;
    public bool depleted;
}

[Serializable]
public sealed class WorldMapPOILayer
{
    public int seed;
    public Rect worldBounds;
    public List<WorldMapPOIInstance> pois = new();

    public bool IsValid => pois != null;

    public WorldMapPOIInstance GetByStableId(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId) || pois == null)
            return null;

        for (int i = 0; i < pois.Count; i++)
        {
            WorldMapPOIInstance poi = pois[i];
            if (poi != null && poi.stableId == stableId)
                return poi;
        }

        return null;
    }
}
