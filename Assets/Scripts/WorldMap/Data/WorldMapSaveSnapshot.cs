using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldMapTopographySaveMode
{
    None = 0,
    RawHeightData = 1,
    BakedAssetReference = 2,
    RegenerateFromSeed = 3
}

[Serializable]
public sealed class WorldMapSaveSnapshot
{
    public int version = 3;

    [Header("Generation Identity")]
    public int worldSeed;
    public string createdWithGameVersion;
    public string lastSavedWithGameVersion;

    public WorldMapTopographySaveSnapshot topography;
    public WorldMapGraphSaveSnapshot graph;
    public WorldMapNodeRuntimeStateSetSnapshot nodeRuntime;
    public WorldMapPOISetSaveSnapshot pois;
    public WorldMapEffectStateSaveSnapshot effects;
    public WorldMapPlayerSaveSnapshot player;
    public WorldMapActiveTravelSaveSnapshot activeTravel;
    public WorldMapSimulationSaveSnapshot simulation;
    public WorldMapKnowledgeSaveSnapshot knowledge;
    public WorldMapSaveDiagnosticsSnapshot diagnostics;

    public bool HasPersistedWorld =>
        graph != null &&
        graph.nodes != null &&
        graph.nodes.Count > 0;

    public void EnsureDefaults()
    {
        topography ??= new WorldMapTopographySaveSnapshot();
        graph ??= new WorldMapGraphSaveSnapshot();
        nodeRuntime ??= new WorldMapNodeRuntimeStateSetSnapshot();
        pois ??= new WorldMapPOISetSaveSnapshot();
        effects ??= new WorldMapEffectStateSaveSnapshot();
        player ??= new WorldMapPlayerSaveSnapshot();
        activeTravel ??= new WorldMapActiveTravelSaveSnapshot();
        simulation ??= new WorldMapSimulationSaveSnapshot();
        knowledge ??= new WorldMapKnowledgeSaveSnapshot();
        diagnostics ??= new WorldMapSaveDiagnosticsSnapshot();

        graph.EnsureDefaults();
        nodeRuntime.EnsureDefaults();
        pois.EnsureDefaults();
        effects.EnsureDefaults();
        knowledge.EnsureDefaults();
    }
}

[Serializable]
public sealed class WorldMapTopographySaveSnapshot
{
    public int version = 1;

    public WorldMapTopographySaveMode mode = WorldMapTopographySaveMode.None;

    public int seed;
    public int width;
    public int height;

    public float worldBoundsX;
    public float worldBoundsY;
    public float worldBoundsWidth;
    public float worldBoundsHeight;

    public float minRaw;
    public float maxRaw;
    public float effectiveSeaLevel01;

    public WorldMapTopographyStatsSaveSnapshot stats;

    [Header("Raw Height Storage")]
    [Tooltip("Legacy fallback. Prefer packed ushort Base64 storage. If this list is populated, saves will be comically huge.")]
    public List<float> height01 = new();

    [Tooltip("Packed encoding used for heightU16Base64.")]
    public string heightEncoding;

    [Tooltip("Base64-encoded little-endian ushort height samples. Much smaller than JSON float lists.")]
    public string heightU16Base64;

    public int heightQuantizationBits;
    public int heightSampleCount;

    [Header("Asset / Settings Identity")]
    public string bakedAssetGuid;
    public string bakedAssetResourcesPath;
    public string settingsId;
    public string settingsHash;
    public string generatorVersion;

    public bool HasLegacyFloatHeightData =>
        mode == WorldMapTopographySaveMode.RawHeightData &&
        width > 0 &&
        height > 0 &&
        height01 != null &&
        height01.Count == width * height;

    public bool HasPackedHeightData =>
        mode == WorldMapTopographySaveMode.RawHeightData &&
        width > 0 &&
        height > 0 &&
        heightSampleCount == width * height &&
        !string.IsNullOrWhiteSpace(heightU16Base64);

    public bool HasRawHeightData =>
        HasPackedHeightData || HasLegacyFloatHeightData;

    public void StorePackedHeights(float[] heights)
    {
        int expected = width * height;

        height01 ??= new List<float>();
        height01.Clear();

        if (heights == null || heights.Length != expected)
        {
            heightEncoding = null;
            heightU16Base64 = null;
            heightQuantizationBits = 0;
            heightSampleCount = 0;
            return;
        }

        heightEncoding = WorldMapTopographyHeightCodec.UShortBase64Encoding;
        heightQuantizationBits = 16;
        heightSampleCount = heights.Length;
        heightU16Base64 = WorldMapTopographyHeightCodec.EncodeUShortBase64(heights);
    }

    public float[] CopyHeight01()
    {
        int expected = width * height;

        if (HasPackedHeightData)
            return WorldMapTopographyHeightCodec.DecodeUShortBase64(heightU16Base64, expected);

        if (HasLegacyFloatHeightData)
            return height01.ToArray();

        return null;
    }

    public Rect ToWorldBounds()
    {
        return new Rect(worldBoundsX, worldBoundsY, worldBoundsWidth, worldBoundsHeight);
    }

    public void StoreWorldBounds(Rect bounds)
    {
        worldBoundsX = bounds.x;
        worldBoundsY = bounds.y;
        worldBoundsWidth = bounds.width;
        worldBoundsHeight = bounds.height;
    }
}

[Serializable]
public sealed class WorldMapTopographyStatsSaveSnapshot
{
    public float water01;
    public float land01;

    public float deepOcean01;
    public float openOcean01;
    public float shelfWater01;
    public float shallowWater01;

    public float beach01;
    public float lowland01;
    public float highland01;
    public float mountain01;
}

[Serializable]
public sealed class WorldMapGraphSaveSnapshot
{
    public int version = 1;
    public int seed;

    public List<WorldMapGraphNodeSaveSnapshot> nodes = new();
    public List<WorldMapGraphEdgeSaveSnapshot> edges = new();

    public void EnsureDefaults()
    {
        nodes ??= new List<WorldMapGraphNodeSaveSnapshot>();
        edges ??= new List<WorldMapGraphEdgeSaveSnapshot>();
    }
}

[Serializable]
public sealed class WorldMapGraphNodeSaveSnapshot
{
    public string stableId;
    public string localStableId;

    public int nodeIndex;
    public int clusterId;

    public string displayName;
    public string kind;

    public float positionX;
    public float positionY;

    public bool isPrimary;

    public string biomeId;
    public string primaryResourceId;
    public string secondaryResourceId;
    public string primaryFactionId;
    public string secondaryFactionId;

    public float dockRating;
    public float tradeRating;

    public float population;
    public float minPopulation;
    public float maxPopulation;

    public List<WorldMapNodeStatSaveSnapshot> initialStats = new();
    public List<WorldMapNodeOptionalBuildingSaveSnapshot> optionalBuildings = new();
    public List<string> flags = new();

    public string notes;

    public Vector2 Position => new Vector2(positionX, positionY);

    public void StorePosition(Vector2 position)
    {
        positionX = position.x;
        positionY = position.y;
    }
}

[Serializable]
public sealed class WorldMapGraphEdgeSaveSnapshot
{
    public string stableId;

    public int aIndex;
    public int bIndex;

    public string aStableId;
    public string bStableId;

    public string routeType;
    public float routeLength;
}

[Serializable]
public sealed class WorldMapNodeRuntimeStateSetSnapshot
{
    public int version = 1;
    public List<WorldMapNodeRuntimeStateSaveSnapshot> nodes = new();

    public void EnsureDefaults()
    {
        nodes ??= new List<WorldMapNodeRuntimeStateSaveSnapshot>();
    }
}

[Serializable]
public sealed class WorldMapNodeRuntimeStateSaveSnapshot
{
    public string stableId;
    public int nodeIndex;

    public float population;
    public float minPopulation;
    public float maxPopulation;

    public List<WorldMapNodeStatSaveSnapshot> stats = new();
    public List<WorldMapNodeFactionInfluenceSaveSnapshot> factionInfluence = new();
    public List<string> flags = new();
    public List<WorldMapNodeResourcePressureSaveSnapshot> resourcePressures = new();

    public string clusterAffinityId;
    public string nodeArchetypeId;

    public int randomSeed;
}

[Serializable]
public sealed class WorldMapNodeStatSaveSnapshot
{
    public string statId;
    public float value;
    public float velocity;
    public float equilibrium;
    public float restoreStrength;
    public float minValue;
    public float maxValue;
}

[Serializable]
public sealed class WorldMapNodeFactionInfluenceSaveSnapshot
{
    public string factionId;
    public float value01;
}

[Serializable]
public sealed class WorldMapNodeResourcePressureSaveSnapshot
{
    public string itemId;
    public float baseline;
    public float value;
    public float driftRate;
}

[Serializable]
public sealed class WorldMapNodeOptionalBuildingSaveSnapshot
{
    public string buildingId;
    public bool present;
    public float rating;
}

[Serializable]
public sealed class WorldMapNodeBuildingSaveSnapshot
{
    public string buildingId;
    public float rating;
    public bool enabled = true;
}

[Serializable]
public sealed class WorldMapPOISetSaveSnapshot
{
    public int version = 1;
    public List<WorldMapPOISaveSnapshot> pois = new();

    public void EnsureDefaults()
    {
        pois ??= new List<WorldMapPOISaveSnapshot>();
    }
}

[Serializable]
public sealed class WorldMapPOISaveSnapshot
{
    public string stableId;
    public string poiDefId;
    public string displayName;

    public float positionX;
    public float positionY;

    public float height01;
    public float depth01;
    public float score;

    public bool discovered;
    public bool surveyed;
    public bool explored;
    public bool depleted;

    public Vector2 Position => new Vector2(positionX, positionY);

    public void StorePosition(Vector2 position)
    {
        positionX = position.x;
        positionY = position.y;
    }
}

[Serializable]
public sealed class WorldMapEffectStateSaveSnapshot
{
    public int version = 1;
    public List<WorldMapEventSaveSnapshot> events = new();
    public List<WorldMapBuffSaveSnapshot> buffs = new();
    public List<WorldMapResolvedOutcomeSaveSnapshot> resolvedOutcomes = new();

    public void EnsureDefaults()
    {
        events ??= new List<WorldMapEventSaveSnapshot>();
        buffs ??= new List<WorldMapBuffSaveSnapshot>();
        resolvedOutcomes ??= new List<WorldMapResolvedOutcomeSaveSnapshot>();
    }
}

[Serializable]
public sealed class WorldMapEventSaveSnapshot
{
    public string instanceId;
    public string eventId;

    public string sourceNodeStableId;
    public string targetNodeStableId;

    public float elapsedHours;
    public float durationHours;
    public float remainingHours;

    public int seed;

    public bool isResolved;
    public bool isVisibleToPlayer;
    public bool discovered;

    public string selectedOutcomeId;
    public string stateJson;
}

[Serializable]
public sealed class WorldMapBuffSaveSnapshot
{
    public string instanceId;
    public string buffId;

    public string nodeStableId;

    public float elapsedHours;
    public float durationHours;
    public float remainingHours;

    public int stacks;

    public string sourceEventInstanceId;
    public string sourceOutcomeId;
}

[Serializable]
public sealed class WorldMapResolvedOutcomeSaveSnapshot
{
    public string eventInstanceId;
    public string eventId;
    public string outcomeId;
    public string nodeStableId;
    public float resolvedWorldHour;
}

[Serializable]
public sealed class WorldMapPlayerSaveSnapshot
{
    public int version = 1;

    public string currentNodeStableId;
    public string lockedSourceNodeStableId;
    public string lockedDestinationNodeStableId;

    public string lastVisitedNodeStableId;
}

[Serializable]
public sealed class WorldMapActiveTravelSaveSnapshot
{
    public int version = 1;

    public bool isTraveling;

    public string fromNodeStableId;
    public string toNodeStableId;

    public int seed;
    public float routeLength;

    public float startWorldHour;
    public float durationHours;
    public float progress01;

    public string boatInstanceId;
    public string boatPrefabGuid;
}

[Serializable]
public sealed class WorldMapSimulationSaveSnapshot
{
    public int version = 1;

    public float worldHour;
    public float lastNodeTickWorldHour;
    public float tickAccumulatorHours;
}

[Serializable]
public sealed class WorldMapKnowledgeSaveSnapshot
{
    public int version = 2;

    [Header("Knowledge Grid")]
    public int gridWidth;
    public int gridHeight;

    public float worldBoundsX;
    public float worldBoundsY;
    public float worldBoundsWidth;
    public float worldBoundsHeight;

    [Header("Surface / Civ-style World Shroud")]
    public string surfaceEncoding;
    public string surfaceBitsBase64;
    public int surfaceRevealedCount;

    [Header("Underwater Survey Shroud")]
    public string underwaterEncoding;
    public string underwaterBitsBase64;
    public int underwaterSurveyedCount;

    [Header("Legacy / Semantic Knowledge")]
    public List<string> knownNodeStableIds = new();
    public List<string> discoveredNodeStableIds = new();
    public List<string> knownRouteStableIds = new();
    public List<string> partialRouteStableIds = new();
    public List<string> rumoredRouteStableIds = new();

    public List<string> ownedChartIds = new();
    public List<string> surveyedRegionIds = new();

    public bool HasKnowledgeGrid =>
        gridWidth > 0 &&
        gridHeight > 0 &&
        worldBoundsWidth > 0f &&
        worldBoundsHeight > 0f &&
        !string.IsNullOrWhiteSpace(surfaceBitsBase64);

    public void EnsureDefaults()
    {
        knownNodeStableIds ??= new List<string>();
        discoveredNodeStableIds ??= new List<string>();
        knownRouteStableIds ??= new List<string>();
        partialRouteStableIds ??= new List<string>();
        rumoredRouteStableIds ??= new List<string>();
        ownedChartIds ??= new List<string>();
        surveyedRegionIds ??= new List<string>();
    }
}

[Serializable]
public sealed class WorldMapSaveDiagnosticsSnapshot
{
    public int version = 1;

    public string mapGenerationVersion;
    public string topographySettingsHash;
    public string nodeGenerationSettingsHash;
    public string poiGenerationSettingsHash;
    public string biomeGenerationSettingsHash;

    public int topographySampleCount;
    public int graphNodeCount;
    public int graphEdgeCount;
    public int runtimeNodeCount;
    public int poiCount;
    public int activeEventCount;
    public int activeBuffCount;
}
