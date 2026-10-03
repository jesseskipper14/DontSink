using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime handle for deterministic celestial world truth.
/// It owns no mutable player knowledge and stores no generated-star cache by design.
/// </summary>
public sealed class CelestialField
{
    private CelestialConstellationCatalog _constellationCatalog;
    public CelestialConstellationGenerationConfig ConstellationConfig { get; private set; } = new();

    public void ConfigureConstellations(CelestialConstellationGenerationConfig config)
    {
        config = (config ?? new CelestialConstellationGenerationConfig()).Clone();
        if (config.Fingerprint == ConstellationConfig.Fingerprint) return;
        ConstellationConfig = config;
        _constellationCatalog = null;
    }
    public int WorldSeed { get; }
    public Rect WorldBounds { get; }
    public CelestialGenerationConfig Config { get; }
    public CelestialGenerationIdentity Identity { get; }
    public int ConstellationTopologyVersion { get; }

    public float CellSizeWorld => Config != null ? Config.cellSizeWorld : 0f;

    /// <summary>
    /// Deterministic constellation truth derived lazily from landmark stars. Names are truth metadata;
    /// player-facing code should gate them through CelestialConstellationKnowledgeState.
    /// </summary>
    public CelestialConstellationCatalog Constellations =>
        _constellationCatalog ??= CelestialConstellationGenerator.Build(this);

    public int CellCountX =>
        IsValid ? Mathf.Max(1, Mathf.CeilToInt(WorldBounds.width / CellSizeWorld)) : 0;

    public int CellCountY =>
        IsValid ? Mathf.Max(1, Mathf.CeilToInt(WorldBounds.height / CellSizeWorld)) : 0;

    public bool IsValid =>
        Config != null &&
        Identity != null &&
        Identity.IsValid &&
        WorldMapCoordinateSpace.IsValidBounds(WorldBounds) &&
        CellSizeWorld > 0f;

    internal CelestialField(
        int worldSeed,
        Rect worldBounds,
        CelestialGenerationConfig config,
        CelestialGenerationIdentity identity)
    {
        WorldSeed = worldSeed;
        WorldBounds = worldBounds;
        Config = config;
        Identity = identity;
        var saved = GameState.I != null ? GameState.I.worldMapSnapshot : null;
        ConstellationTopologyVersion = saved != null && saved.HasPersistedWorld &&
            (saved.topography == null || saved.topography.generatorVersion != "topography_v3_wrapped_x") ? 1 : 2;
    }

    public Rect GetCellWorldRect(int cellX, int cellY)
    {
        if (!IsValid || cellX < 0 || cellY < 0 || cellX >= CellCountX || cellY >= CellCountY)
            return default;

        float xMin = WorldBounds.xMin + cellX * CellSizeWorld;
        float yMin = WorldBounds.yMin + cellY * CellSizeWorld;
        float xMax = Mathf.Min(xMin + CellSizeWorld, WorldBounds.xMax);
        float yMax = Mathf.Min(yMin + CellSizeWorld, WorldBounds.yMax);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    public void GetCellObjects(int cellX, int cellY, List<CelestialObject> results, bool clearResults = true)
    {
        CelestialFieldGenerator.GenerateCell(this, cellX, cellY, results, clearResults);
    }

    public void Query(Rect worldArea, List<CelestialObject> results, bool clearResults = true)
    {
        CelestialFieldQuery.Query(this, worldArea, results, clearResults);
    }

    public void QueryAll(List<CelestialObject> results, bool clearResults = true)
    {
        CelestialFieldQuery.Query(this, WorldBounds, results, clearResults);
    }

    public bool TryResolveConstellation(string stableId, out CelestialConstellation constellation)
    {
        constellation = null;
        return !string.IsNullOrWhiteSpace(stableId) && Constellations.TryGetById(stableId, out constellation);
    }

    public bool TryResolveObject(string stableId, out CelestialObject celestialObject)
    {
        celestialObject = null;

        if (!IsValid || !CelestialStableId.TryParse(stableId, out CelestialStableIdParts parts))
            return false;

        if (parts.GeneratorVersion != Identity.generatorVersion || parts.WorldSeed != WorldSeed)
            return false;

        if (parts.CellX < 0 || parts.CellX >= CellCountX || parts.CellY < 0 || parts.CellY >= CellCountY)
            return false;

        var cellObjects = new List<CelestialObject>();
        GetCellObjects(parts.CellX, parts.CellY, cellObjects);

        for (int i = 0; i < cellObjects.Count; i++)
        {
            CelestialObject candidate = cellObjects[i];
            if (candidate != null && candidate.StableId == stableId)
            {
                celestialObject = candidate;
                return true;
            }
        }

        return false;
    }
}
