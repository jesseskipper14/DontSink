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

    [Header("Debug Open")]
    [SerializeField] private bool debugOpenWithKey = false;
    [SerializeField] private KeyCode debugOpenKey = KeyCode.M;

    public bool IsWorldMapOpen =>
        overlay != null &&
        overlay.IsOpen &&
        overlay.ActiveCartridge is WorldMapCartridge;

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

    public bool ToggleWorldMap()
    {
        AutoWire();

        if (IsWorldMapOpen)
        {
            overlay.Close();
            return true;
        }

        return OpenWorldMap();
    }

    public bool OpenWorldMap()
    {
        AutoWire();

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

        var cart = new WorldMapCartridge(
            generator,
            runtimeBinder,
            player,
            travelRules,
            travelDebug,
            travelLauncher,
            eventManager,
            effectCatalog,
            topographyDebugSource
        );

        var ctx = new MiniGameContext
        {
            targetId = "world_map",
            difficulty = 1f,
            pressure = 0f,
            seed = generator.seed
        };

        overlay.Open(cart, ctx);
        return true;
    }

    public bool CloseWorldMap()
    {
        AutoWire();

        if (!IsWorldMapOpen)
            return false;

        overlay.Close();
        return true;
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
    }
}