using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldMapKnowledgeSource : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapTopographyDebugSource topographySource;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapPlayerRef playerRef;

    [Header("Grid")]
    [Range(16, 512)][SerializeField] private int gridWidth = 128;
    [Range(16, 512)][SerializeField] private int gridHeight = 128;

    [Header("Reveal Radii")]
    [Min(0.1f)][SerializeField] private float currentNodeSurfaceRevealRadius = 28f;
    [Min(0.1f)][SerializeField] private float currentNodeUnderwaterSurveyRadius = 12f;
    [Min(0.1f)][SerializeField] private float travelDestinationSurfaceRevealRadius = 28f;

    [Header("Travel Reveal")]
    [Min(0.1f)][SerializeField] private float travelCorridorSurfaceRevealRadius = 18f;
    [Range(2, 96)][SerializeField] private int travelCorridorRevealSteps = 18;

    [Header("Startup")]
    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private bool restoreFromGameStateOnAwake = true;
    [SerializeField] private bool revealCurrentNodeOnAwake = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    public WorldMapKnowledgeState State { get; private set; } = new WorldMapKnowledgeState();

    public bool HasState => State != null && State.IsValid;
    public float SurfaceReveal01 => HasState ? State.SurfaceReveal01 : 0f;
    public float UnderwaterSurvey01 => HasState ? State.UnderwaterSurvey01 : 0f;

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();

        if (!initializeOnAwake)
            return;

        if (restoreFromGameStateOnAwake && TryRestoreFromGameState())
            return;

        EnsureInitialized();

        if (revealCurrentNodeOnAwake)
            RevealSurfaceAroundCurrentNode();
    }

    private void OnEnable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt += HandleRuntimeBuilt;
    }

    private void OnDisable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt -= HandleRuntimeBuilt;
    }

    private void Start()
    {
        AutoWire();

        if (!HasState)
            EnsureInitialized();

        if (revealCurrentNodeOnAwake)
            RevealSurfaceAroundCurrentNode();
    }

    private void HandleRuntimeBuilt()
    {
        if (!HasState)
            EnsureInitialized();

        if (revealCurrentNodeOnAwake)
            RevealSurfaceAroundCurrentNode();
    }

    public void EnsureInitialized()
    {
        if (HasState)
            return;

        AutoWire();

        Rect bounds = ResolveWorldBounds();

        if (bounds.width <= 0f || bounds.height <= 0f)
        {
            bounds = new Rect(-100f, -100f, 200f, 200f);
            Debug.LogWarning("[WorldMapKnowledgeSource] Falling back to default knowledge bounds.", this);
        }

        State = new WorldMapKnowledgeState();
        State.Initialize(gridWidth, gridHeight, bounds);

        if (verboseLogging)
        {
            Debug.Log(
                $"[WorldMapKnowledgeSource] Initialized knowledge grid {gridWidth}x{gridHeight}, bounds={bounds}.",
                this
            );
        }
    }

    public bool IsSurfaceRevealed(Vector2 worldPosition)
    {
        return HasState && State.IsRevealed(WorldMapKnowledgeLayer.Surface, worldPosition);
    }

    public bool IsUnderwaterSurveyed(Vector2 worldPosition)
    {
        return HasState && State.IsRevealed(WorldMapKnowledgeLayer.UnderwaterSurvey, worldPosition);
    }

    public bool IsPOIVisible(WorldMapPOIInstance poi)
    {
        if (poi == null)
            return false;

        if (poi.discovered || poi.surveyed)
            return IsSurfaceRevealed(poi.position);

        return IsSurfaceRevealed(poi.position) && IsUnderwaterSurveyed(poi.position);
    }

    [ContextMenu("Reveal Surface Around Current Node")]
    public void RevealSurfaceAroundCurrentNode()
    {
        if (TryGetCurrentNodePosition(out Vector2 pos))
            RevealSurfaceCircle(pos, currentNodeSurfaceRevealRadius);
    }

    [ContextMenu("Survey Underwater Around Current Node")]
    public void SurveyUnderwaterAroundCurrentNode()
    {
        if (TryGetCurrentNodePosition(out Vector2 pos))
            SurveyUnderwaterCircle(pos, currentNodeUnderwaterSurveyRadius);
    }

    public void RevealTravelDestinationNow()
    {
        GameState gs = GameState.I;
        string destination = gs != null && gs.activeTravel != null
            ? gs.activeTravel.toNodeStableId
            : playerRef != null && playerRef.State != null
                ? playerRef.State.currentNodeId
                : null;

        if (TryGetNodePosition(destination, out Vector2 pos))
            RevealSurfaceCircle(pos, travelDestinationSurfaceRevealRadius);
    }

    public void RevealSurfaceAlongRouteByNodeIds(string fromNodeId, string toNodeId)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId))
            return;

        if (!TryGetNodePosition(fromNodeId, out Vector2 from))
            return;

        if (!TryGetNodePosition(toNodeId, out Vector2 to))
            return;

        RevealSurfaceAlongRoute(from, to, travelCorridorSurfaceRevealRadius, travelCorridorRevealSteps);
    }

    public void RevealSurfaceAlongRoute(Vector2 from, Vector2 to, float radius, int steps)
    {
        EnsureInitialized();

        radius = Mathf.Max(0.1f, radius);
        steps = Mathf.Max(2, steps);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = Vector2.Lerp(from, to, t);
            State.RevealCircleWorld(WorldMapKnowledgeLayer.Surface, p, radius);
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[WorldMapKnowledgeSource] Surface route reveal {from} -> {to} " +
                $"r={radius:0.0}, steps={steps}, now={SurfaceReveal01:P1}",
                this
            );
        }
    }

    public void RevealSurfaceCircle(Vector2 worldPos, float radius)
    {
        EnsureInitialized();
        State.RevealCircleWorld(WorldMapKnowledgeLayer.Surface, worldPos, radius);

        if (verboseLogging)
            Debug.Log($"[WorldMapKnowledgeSource] Surface reveal {worldPos} r={radius:0.0} now={SurfaceReveal01:P1}", this);
    }

    public void SurveyUnderwaterCircle(Vector2 worldPos, float radius)
    {
        EnsureInitialized();
        State.RevealCircleWorld(WorldMapKnowledgeLayer.UnderwaterSurvey, worldPos, radius, surfaceRevealImplied: true);

        if (verboseLogging)
            Debug.Log($"[WorldMapKnowledgeSource] Underwater survey {worldPos} r={radius:0.0} now={UnderwaterSurvey01:P1}", this);
    }

    [ContextMenu("Reveal All Surface")]
    public void RevealAllSurface()
    {
        EnsureInitialized();
        State.RevealAll(WorldMapKnowledgeLayer.Surface);
    }

    [ContextMenu("Reveal All Underwater")]
    public void RevealAllUnderwater()
    {
        EnsureInitialized();
        State.RevealAll(WorldMapKnowledgeLayer.UnderwaterSurvey);
        State.RevealAll(WorldMapKnowledgeLayer.Surface);
    }

    [ContextMenu("Clear Surface")]
    public void ClearSurface()
    {
        EnsureInitialized();
        State.ClearLayer(WorldMapKnowledgeLayer.Surface);
    }

    [ContextMenu("Clear Underwater")]
    public void ClearUnderwater()
    {
        EnsureInitialized();
        State.ClearLayer(WorldMapKnowledgeLayer.UnderwaterSurvey);
    }

    [ContextMenu("Clear All Knowledge")]
    public void ClearAll()
    {
        EnsureInitialized();
        State.ClearAll();
    }

    public WorldMapKnowledgeSaveSnapshot CaptureSnapshot()
    {
        EnsureInitialized();

        var snapshot = new WorldMapKnowledgeSaveSnapshot();
        snapshot.EnsureDefaults();

        State.CopyToSnapshot(snapshot);

        return snapshot;
    }

    public bool TryRestoreFromSnapshot(WorldMapKnowledgeSaveSnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        var restored = new WorldMapKnowledgeState();
        if (!restored.TryRestoreFromSnapshot(snapshot))
            return false;

        State = restored;

        if (verboseLogging)
        {
            Debug.Log(
                $"[WorldMapKnowledgeSource] Restored knowledge. " +
                $"Surface={SurfaceReveal01:P1}, Underwater={UnderwaterSurvey01:P1}",
                this
            );
        }

        return true;
    }

    public bool TryRestoreFromGameState()
    {
        GameState gs = GameState.I;
        WorldMapKnowledgeSaveSnapshot snapshot =
            gs != null && gs.worldMapSnapshot != null
                ? gs.worldMapSnapshot.knowledge
                : null;

        return TryRestoreFromSnapshot(snapshot);
    }

    public bool TryGetCurrentNodePosition(out Vector2 pos)
    {
        pos = default;

        WorldMapPlayerState player = playerRef != null ? playerRef.State : GameState.I != null ? GameState.I.player : null;
        if (player == null || string.IsNullOrWhiteSpace(player.currentNodeId))
            return false;

        return TryGetNodePosition(player.currentNodeId, out pos);
    }

    public bool TryGetNodePosition(string stableId, out Vector2 pos)
    {
        pos = default;

        if (string.IsNullOrWhiteSpace(stableId))
            return false;

        AutoWire();

        if (runtimeBinder != null &&
            runtimeBinder.IsBuilt &&
            runtimeBinder.Registry.TryGetByStableId(stableId, out MapNodeRuntime rt) &&
            rt != null)
        {
            pos = rt.transform.position;
            return true;
        }

        WorldMapGraphGenerator generator =
            FindAnyObjectByType<WorldMapGraphGenerator>(FindObjectsInactive.Include);

        MapGraph g = generator != null ? generator.graph : null;
        if (g == null || g.nodes == null)
            return false;

        for (int i = 0; i < g.nodes.Count; i++)
        {
            string nodeStableId = WorldMapStableIdUtility.BuildNodeStableId(g.seed, g.nodes[i]);
            if (nodeStableId != stableId)
                continue;

            pos = g.nodes[i].position;
            return true;
        }

        return false;
    }

    public Rect ResolveWorldBounds()
    {
        AutoWire();

        if (topographySource != null && topographySource.Field != null && topographySource.Field.IsValid)
            return topographySource.Field.WorldBounds;

        WorldMapRuntimeCache cache = WorldMapRuntimeCache.I;
        if (cache != null && cache.HasTopography)
            return cache.Field.WorldBounds;

        WorldMapGraphGenerator generator =
            FindAnyObjectByType<WorldMapGraphGenerator>(FindObjectsInactive.Include);

        MapGraph g = generator != null ? generator.graph : null;
        if (g != null && g.nodes != null && g.nodes.Count > 0)
        {
            Vector2 min = g.nodes[0].position;
            Vector2 max = g.nodes[0].position;

            for (int i = 1; i < g.nodes.Count; i++)
            {
                min = Vector2.Min(min, g.nodes[i].position);
                max = Vector2.Max(max, g.nodes[i].position);
            }

            Vector2 pad = new Vector2(20f, 20f);
            return Rect.MinMaxRect(min.x - pad.x, min.y - pad.y, max.x + pad.x, max.y + pad.y);
        }

        return default;
    }

    private void AutoWire()
    {
        if (topographySource == null)
            topographySource = FindAnyObjectByType<WorldMapTopographyDebugSource>(FindObjectsInactive.Include);

        if (runtimeBinder == null)
            runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>(FindObjectsInactive.Include);

        if (playerRef == null)
            playerRef = FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);
    }
}
