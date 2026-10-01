using UnityEngine;
using MiniGames;

[DisallowMultipleComponent]
public sealed class WorldMapOverlayRunner : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private MiniGameOverlayHost overlay;

    [Header("World Map Refs")]
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapPlayerRef player;
    [SerializeField] private WorldMapTravelRulesConfig travelRules;
    [SerializeField] private WorldMapTravelDebugController travelDebug;
    [SerializeField] private NodeTravelController travelLauncher;
    [SerializeField] private WorldMapEventManager eventManager;

    [Header("World Map Effects")]
    [SerializeField] private WorldMapEffectCatalog effectCatalog;

    [Header("Topography")]
    [SerializeField] private WorldMapTopographyDebugSource topographyDebugSource;

    [Header("POIs")]
    [SerializeField] private WorldMapPOISource poiSource;

    [Header("Celestial Map")]
    [SerializeField] private CelestialMapOverlaySource celestialOverlaySource;

    [Header("Star Chart")]
    [Tooltip("Optional. If blank, the deterministic Phase-5B built-in fragment visual defaults are used.")]
    [SerializeField] private CelestialChartFragmentVisualSettings chartFragmentVisualSettings;

    [Header("Debug Open")]
    [SerializeField] private bool debugOpenWithKey = false;
    [SerializeField] private KeyCode debugOpenKey = KeyCode.M;

    public bool IsMapTableOpen =>
        overlay != null &&
        overlay.IsOpen &&
        overlay.ActiveCartridge is MapTableCartridge;

    // Compatibility: callers that only care about the old world-map page still work.
    public bool IsWorldMapOpen
    {
        get
        {
            if (overlay == null || !overlay.IsOpen)
                return false;

            if (overlay.ActiveCartridge is WorldMapCartridge)
                return true;

            return overlay.ActiveCartridge is MapTableCartridge table &&
                   table.ActivePage == MapTablePage.WorldMap;
        }
    }

    public bool IsStarChartOpen =>
        overlay != null &&
        overlay.IsOpen &&
        overlay.ActiveCartridge is MapTableCartridge table &&
        table.ActivePage == MapTablePage.StarChart;

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();
    }

    private void Update()
    {
        if (!debugOpenWithKey)
            return;

        if (Input.GetKeyDown(debugOpenKey))
            ToggleWorldMap();
    }

    /// <summary>
    /// Legacy compatibility entry point. The physical map table now opens a MapTableCartridge,
    /// initially on the World Map page.
    /// </summary>
    public bool ToggleWorldMap()
    {
        return ToggleMapTable(null, MapTablePage.WorldMap);
    }

    public bool ToggleMapTable(
        GameObject requester,
        MapTablePage initialPage = MapTablePage.WorldMap)
    {
        AutoWire();

        if (IsMapTableOpen || IsLegacyWorldMapOpen())
        {
            overlay.Close();
            return true;
        }

        return OpenMapTable(requester, initialPage);
    }

    public bool OpenWorldMap()
    {
        return OpenMapTable(null, MapTablePage.WorldMap);
    }

    public bool OpenWorldMap(GameObject requester)
    {
        return OpenMapTable(requester, MapTablePage.WorldMap);
    }

    public bool OpenStarChart(GameObject requester = null)
    {
        return OpenMapTable(requester, MapTablePage.StarChart);
    }

    public bool OpenMapTable(
        GameObject requester,
        MapTablePage initialPage = MapTablePage.WorldMap)
    {
        AutoWire();

        if (overlay != null && overlay.IsOpen && overlay.ActiveCartridge is MapTableCartridge existingTable)
        {
            existingTable.SetActivePage(initialPage);
            return true;
        }

        if (overlay == null)
        {
            Debug.LogError("[WorldMapOverlayRunner] Missing MiniGameOverlayHost.", this);
            return false;
        }

        if (generator == null || generator.graph == null)
        {
            Debug.LogError("[WorldMapOverlayRunner] Missing WorldMapGraphGenerator or graph.", this);
            return false;
        }

        if (runtimeBinder == null || !runtimeBinder.IsBuilt)
        {
            Debug.LogError("[WorldMapOverlayRunner] Runtime binder is missing or not built.", this);
            return false;
        }

        GameObject resolvedRequester = ResolveRequester(requester);
        if (resolvedRequester == null)
        {
            Debug.LogWarning(
                "[WorldMapOverlayRunner] No unique map-table requester could be resolved. " +
                "The table can still be viewed, but shared star-chart mutations will be rejected.",
                this);
        }

        var viewport = new MapTableViewportState();

        var worldMap = new WorldMapCartridge(
            generator,
            runtimeBinder,
            player,
            travelRules,
            travelDebug,
            travelLauncher,
            eventManager,
            effectCatalog,
            topographyDebugSource,
            poiSource,
            celestialOverlaySource,
            viewport,
            embeddedInMapTable: true);

        var starChart = new CelestialChartTableCartridge(
            resolvedRequester,
            viewport,
            worldMap,
            chartFragmentVisualSettings);

        var table = new MapTableCartridge(
            resolvedRequester,
            worldMap,
            starChart,
            viewport,
            initialPage);

        var ctx = new MiniGameContext
        {
            targetId = "map_table",
            difficulty = 1f,
            pressure = 0f,
            seed = generator.seed
        };

        overlay.Open(table, ctx);
        return true;
    }

    public bool CloseWorldMap()
    {
        return CloseMapTable();
    }

    public bool CloseMapTable()
    {
        AutoWire();

        if (!IsMapTableOpen && !IsLegacyWorldMapOpen())
            return false;

        overlay.Close();
        return true;
    }

    private bool IsLegacyWorldMapOpen()
    {
        return overlay != null &&
               overlay.IsOpen &&
               overlay.ActiveCartridge is WorldMapCartridge;
    }

    private GameObject ResolveRequester(GameObject explicitRequester)
    {
        if (explicitRequester != null)
            return explicitRequester;

        PlayerLoadoutPersistence[] players =
            FindObjectsByType<PlayerLoadoutPersistence>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        if (players != null && players.Length == 1 && players[0] != null)
            return players[0].gameObject;

        if (players != null && players.Length > 1 && GameState.I != null)
        {
            string localKey = GameState.I.LocalPlayerPersistenceKey;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerLoadoutPersistence candidate = players[i];
                if (candidate != null && candidate.PersistenceKey == localKey)
                    return candidate.gameObject;
            }
        }

        return null;
    }

    private void AutoWire()
    {
        if (overlay == null)
            overlay = FindAnyObjectByType<MiniGameOverlayHost>(FindObjectsInactive.Include);

        if (generator == null)
            generator = FindAnyObjectByType<WorldMapGraphGenerator>(FindObjectsInactive.Include);

        if (runtimeBinder == null)
            runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>(FindObjectsInactive.Include);

        if (player == null)
            player = FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);

        if (travelDebug == null)
            travelDebug = FindAnyObjectByType<WorldMapTravelDebugController>(FindObjectsInactive.Include);

        if (travelLauncher == null)
            travelLauncher = FindAnyObjectByType<NodeTravelController>(FindObjectsInactive.Include);

        // ScriptableObjects usually need inspector assignment.
        // This lookup probably won't find asset-only configs, but it is harmless as a fallback.
        if (travelRules == null)
            travelRules = FindAnyObjectByType<WorldMapTravelRulesConfig>(FindObjectsInactive.Include);

        if (eventManager == null)
            eventManager = FindAnyObjectByType<WorldMapEventManager>(FindObjectsInactive.Include);

        if (effectCatalog == null && eventManager != null)
            effectCatalog = eventManager.EffectCatalog;

        if (topographyDebugSource == null)
            topographyDebugSource = FindAnyObjectByType<WorldMapTopographyDebugSource>(FindObjectsInactive.Include);

        if (poiSource == null)
            poiSource = FindAnyObjectByType<WorldMapPOISource>(FindObjectsInactive.Include);

        if (celestialOverlaySource == null)
            celestialOverlaySource = FindAnyObjectByType<CelestialMapOverlaySource>(FindObjectsInactive.Include);
    }
}
