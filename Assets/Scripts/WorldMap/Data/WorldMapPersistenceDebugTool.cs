using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldMapPersistenceDebugTool : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapTopographyDebugSource topographySource;
    [SerializeField] private WorldMapGraphGenerator graphGenerator;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapEventManager eventManager;
    [SerializeField] private WorldMapPOISource poiSource;
    [SerializeField] private WorldMapKnowledgeSource knowledgeSource;
    [SerializeField] private WorldMapPlayerRef playerRef;

    [Header("Debug")]
    [SerializeField] private bool autoWireOnAwake = true;
    [SerializeField] private bool logAfterActions = true;

    private void Awake()
    {
        if (autoWireOnAwake)
            AutoWire();
    }

    [ContextMenu("WorldMap Persistence/Auto Wire")]
    public void AutoWire()
    {
        if (topographySource == null)
            topographySource = FindAnyObjectByType<WorldMapTopographyDebugSource>(FindObjectsInactive.Include);

        if (graphGenerator == null)
            graphGenerator = FindAnyObjectByType<WorldMapGraphGenerator>(FindObjectsInactive.Include);

        if (runtimeBinder == null)
            runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>(FindObjectsInactive.Include);

        if (eventManager == null)
            eventManager = FindAnyObjectByType<WorldMapEventManager>(FindObjectsInactive.Include);

        if (poiSource == null)
            poiSource = FindAnyObjectByType<WorldMapPOISource>(FindObjectsInactive.Include);

        if (knowledgeSource == null)
            knowledgeSource = FindAnyObjectByType<WorldMapKnowledgeSource>(FindObjectsInactive.Include);

        if (playerRef == null)
            playerRef = FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);
    }

    [ContextMenu("WorldMap Persistence/Capture Snapshot Into GameState")]
    public void CaptureSnapshotIntoGameState()
    {
        AutoWire();

        WorldMapSaveSnapshot snapshot = WorldMapSaveBuilder.Capture(
            topographySource,
            graphGenerator,
            runtimeBinder,
            eventManager,
            poiSource,
            playerRef,
            GameState.I,
            knowledgeSource
        );

        if (GameState.I != null)
            GameState.I.SetWorldMapSnapshot(snapshot, "WorldMapPersistenceDebugTool.CaptureSnapshotIntoGameState");

        if (logAfterActions)
        {
            WorldMapSaveDebugUtility.LogSummary(snapshot, "World Map Capture Debug", this);
            WorldMapSaveDebugUtility.LogValidation(snapshot, "World Map Capture Validation", this);
        }
    }

    [ContextMenu("WorldMap Persistence/Restore Snapshot From GameState")]
    public void RestoreSnapshotFromGameState()
    {
        AutoWire();

        GameState gs = GameState.I;
        if (gs == null || gs.worldMapSnapshot == null)
        {
            Debug.LogWarning("[WorldMapPersistenceDebugTool] Cannot restore: GameState/worldMapSnapshot missing.", this);
            return;
        }

        bool topoOk = WorldMapSaveRestorer.TryRestoreTopographyToSource(topographySource, gs);
        bool graphOk = WorldMapSaveRestorer.TryRestoreGraphToGenerator(graphGenerator, gs);

        WorldMapSaveRestorer.RestoreNodeRuntimeStateToGameState(gs);

        if (runtimeBinder != null)
            runtimeBinder.Rebuild();

        bool poiOk = WorldMapSaveRestorer.TryRestorePOIsToSource(poiSource, gs);
        bool knowledgeOk = WorldMapSaveRestorer.TryRestoreKnowledgeToSource(knowledgeSource, gs);

        bool effectsOk = WorldMapSaveRestorer.TryRestoreEffectsToEventManager(
            eventManager,
            runtimeBinder,
            graphGenerator,
            gs
        );

        Debug.Log(
            "[WorldMapPersistenceDebugTool] Restore requested.\n" +
            $"  Topography: {(topoOk ? "OK" : "SKIPPED/FAILED")}\n" +
            $"  Graph: {(graphOk ? "OK" : "SKIPPED/FAILED")}\n" +
            $"  Runtime Rebuild: {(runtimeBinder != null && runtimeBinder.IsBuilt ? "OK" : "SKIPPED/FAILED")}\n" +
            $"  POIs: {(poiOk ? "OK" : "SKIPPED/FAILED")}\n" +
            $"  Knowledge: {(knowledgeOk ? "OK" : "SKIPPED/FAILED")}\n" +
            $"  Effects: {(effectsOk ? "OK" : "SKIPPED/FAILED")}",
            this
        );

        if (logAfterActions)
        {
            WorldMapSaveDebugUtility.LogSummary(gs.worldMapSnapshot, "World Map Restore Source Snapshot", this);
            WorldMapSaveDebugUtility.LogValidation(gs.worldMapSnapshot, "World Map Restore Source Validation", this);
        }
    }

    [ContextMenu("WorldMap Persistence/Log Snapshot Summary")]
    public void LogSnapshotSummary()
    {
        WorldMapSaveDebugUtility.LogSummary(
            GameState.I != null ? GameState.I.worldMapSnapshot : null,
            "World Map Snapshot",
            this
        );
    }

    [ContextMenu("WorldMap Persistence/Validate Snapshot")]
    public void ValidateSnapshot()
    {
        WorldMapSaveDebugUtility.LogValidation(
            GameState.I != null ? GameState.I.worldMapSnapshot : null,
            "World Map Snapshot Validation",
            this
        );
    }

    [ContextMenu("WorldMap Persistence/Clear Snapshot")]
    public void ClearSnapshot()
    {
        if (GameState.I == null)
        {
            Debug.LogWarning("[WorldMapPersistenceDebugTool] Cannot clear snapshot: GameState.I missing.", this);
            return;
        }

        GameState.I.SetWorldMapSnapshot(new WorldMapSaveSnapshot(), "WorldMapPersistenceDebugTool.ClearSnapshot");

        if (logAfterActions)
            Debug.Log("[WorldMapPersistenceDebugTool] Cleared GameState world map snapshot.", this);
    }
}
