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

    [Header("Star Chart - Starter Patch")]
    [Tooltip("Prototype/tutorial stand-in. When enabled, a known-good starter celestial patch is created once for the current world and pinned at the StartDock coordinate. Disable for a blank-chart start.")]
    [SerializeField] private bool startWithStarterPatch = true;

    [SerializeField] private CelestialFieldSource celestialFieldSource;

    [Header("Map Table Presentation")]
    [Tooltip("Physical table border thickness in shared world/map units. The table is anchored to the full world-map canvas, not the graph-node extents.")]
    [SerializeField, Min(0f)] private float tableBorderWorldUnits = 18f;

    [Header("Map Table Physical Pieces")]
    [Tooltip("Prototype stand-in for the future physical marker system. Creates one persistent player-boat piece at the StartDock the first time the table is used. The piece does NOT auto-follow true position after creation.")]
    [SerializeField] private bool startWithPlayerBoatPiece = true;

    [Tooltip("How many persistent green blank blocks are created the first time this world needs them. Raising the value later adds missing blocks; lowering it does not delete blocks the player already moved.")]
    [SerializeField, Range(0, 16)] private int greenBlockCount = 4;

    [Tooltip("How many persistent red blank blocks are created the first time this world needs them. Raising the value later adds missing blocks; lowering it does not delete existing blocks.")]
    [SerializeField, Range(0, 16)] private int redBlockCount = 4;

    [Tooltip("How many persistent yellow blank blocks are created the first time this world needs them. Raising the value later adds missing blocks; lowering it does not delete existing blocks.")]
    [SerializeField, Range(0, 16)] private int yellowBlockCount = 4;

    [Tooltip("How many persistent white blank blocks are created the first time this world needs them. Raising the value later adds missing blocks; lowering it does not delete existing blocks.")]
    [SerializeField, Range(0, 16)] private int whiteBlockCount = 4;

    [Tooltip("How many persistent black blank blocks are created the first time this world needs them. Raising the value later adds missing blocks; lowering it does not delete existing blocks.")]
    [SerializeField, Range(0, 16)] private int blackBlockCount = 4;

    [Tooltip("Initial spacing between newly-created blank blocks in shared map/world units.")]
    [SerializeField, Min(1f)] private float physicalBlockSpawnSpacingWorldUnits = 7f;

    [Header("Star Chart - Manual Snap Tuning")]
    [Tooltip("ATTEMPT SNAP only. Maximum screen-space translation correction allowed. Lower values require the player to align fragments more precisely first. Changes take effect the next time the map table is opened.")]
    [SerializeField, Min(0f)] private float snapPositionTolerancePixels = 6f;

    [Tooltip("ATTEMPT SNAP only. Maximum rotation correction allowed. Lower values require closer manual rotation first. Changes take effect the next time the map table is opened.")]
    [SerializeField, Range(0f, 15f)] private float snapRotationToleranceDegrees = 2f;

    [Tooltip("ATTEMPT SNAP only. Maximum average shared-mark residual after the rigid fit. Lower values demand a cleaner overlap. Changes take effect the next time the map table is opened.")]
    [SerializeField, Min(0f)] private float snapResidualTolerancePixels = 1.5f;

    [Tooltip("ATTEMPT SNAP only. Minimum number of identical celestial marks shared between two fragments before the fit is accepted. Changes take effect the next time the map table is opened.")]
    [SerializeField, Range(2, 8)] private int snapMinimumSharedMarks = 2;

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
        requester = ResolveRequester(requester);
        var owner = CameraManager.ForActor(requester);
        if (owner == null || !owner.CanProvideGameplayInput) return false;
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
        requester = ResolveRequester(requester);
        var owner = CameraManager.ForActor(requester);
        if (owner == null || !owner.CanProvideGameplayInput) return false;
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

        if (startWithStarterPatch)
        {
            if (!CelestialStarterChartBootstrap.EnsureStarterPatch(
                    resolvedRequester,
                    generator,
                    celestialFieldSource,
                    out string starterMessage))
            {
                if (!string.IsNullOrWhiteSpace(starterMessage))
                    Debug.LogWarning($"[WorldMapOverlayRunner] {starterMessage}", this);
            }
        }

        if (TryGetStartDockPosition(out Vector2 startDockPosition))
        {
            if (startWithPlayerBoatPiece)
            {
                if (!MapTablePhysicalPieceAuthority.EnsurePlayerBoatPiece(
                        resolvedRequester,
                        startDockPosition,
                        out _,
                        out string pieceMessage) &&
                    !string.IsNullOrWhiteSpace(pieceMessage))
                {
                    Debug.LogWarning($"[WorldMapOverlayRunner] {pieceMessage}", this);
                }
            }

            if (!MapTablePhysicalPieceAuthority.EnsureColorBlockPieces(
                    resolvedRequester,
                    startDockPosition,
                    greenBlockCount,
                    redBlockCount,
                    yellowBlockCount,
                    whiteBlockCount,
                    blackBlockCount,
                    physicalBlockSpawnSpacingWorldUnits,
                    out _,
                    out string blockMessage) &&
                !string.IsNullOrWhiteSpace(blockMessage))
            {
                Debug.LogWarning($"[WorldMapOverlayRunner] {blockMessage}", this);
            }
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
            chartFragmentVisualSettings,
            snapPositionTolerancePixels,
            snapRotationToleranceDegrees,
            snapResidualTolerancePixels,
            snapMinimumSharedMarks,
            celestialFieldSource);

        var table = new MapTableCartridge(
            resolvedRequester,
            worldMap,
            starChart,
            viewport,
            initialPage,
            tableBorderWorldUnits);

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
        var manager = CameraManager.Instance;
        return manager != null && manager.OwnerTarget != null ? manager.OwnerTarget.gameObject : null;
    }

    private bool TryGetStartDockPosition(out Vector2 position)
    {
        position = Vector2.zero;
        if (generator == null || generator.graph == null || generator.graph.nodes == null)
            return false;

        for (int i = 0; i < generator.graph.nodes.Count; i++)
        {
            MapNode node = generator.graph.nodes[i];
            if (node != null && node.kind == NodeKind.StartDock)
            {
                position = node.position;
                return true;
            }
        }

        if (generator.graph.nodes.Count > 0 && generator.graph.nodes[0] != null)
        {
            position = generator.graph.nodes[0].position;
            return true;
        }

        return false;
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

        if (celestialFieldSource == null)
            celestialFieldSource = FindAnyObjectByType<CelestialFieldSource>(FindObjectsInactive.Include);
    }

#if UNITY_EDITOR
    [ContextMenu("Star Chart/DEBUG Ensure Starter Patch")]
    private void DebugEnsureStarterPatch()
    {
        AutoWire();
        GameObject requester = ResolveRequester(null);

        if (CelestialStarterChartBootstrap.EnsureStarterPatch(
                requester,
                generator,
                celestialFieldSource,
                out string message))
        {
            Debug.Log($"[WorldMapOverlayRunner] {message}", this);
        }
        else
        {
            Debug.LogWarning($"[WorldMapOverlayRunner] {message}", this);
        }
    }

    [ContextMenu("Star Chart/DEBUG Remove Starter Patch")]
    private void DebugRemoveStarterPatch()
    {
        AutoWire();
        bool removed = CelestialStarterChartBootstrap.RemoveStarterPatchForDebug(
            generator,
            celestialFieldSource);

        Debug.Log(
            removed
                ? "[WorldMapOverlayRunner] Removed starter celestial patch."
                : "[WorldMapOverlayRunner] No starter celestial patch was removed.",
            this);
    }
#endif
}
