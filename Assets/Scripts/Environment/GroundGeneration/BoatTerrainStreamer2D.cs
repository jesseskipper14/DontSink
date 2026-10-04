using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>BoatScene-only opt-in ground authority. No camera or legacy OnGenerated event.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class BoatTerrainStreamer2D : MonoBehaviour, IStreamedGroundSource2D, IGroundFillBottomSource
{
    [SerializeField] private BoatTerrainProfile profile;
    [SerializeField] private Material groundMaterial;
    [SerializeField] private string groundLayerName = "Ground";
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder;
    [Header("Coastal grounding prototype")]
    [SerializeField] private bool usePhysicalCoasts = true;
    [SerializeField, Min(1)] private float maximumLandHeight = 40;
    [SerializeField, Range(.01f, .5f)] private float coastalDepthBand = .12f;
    [SerializeField, Min(1)] private float coastRiseSpeed = 30;
    [SerializeField, Min(1)] private float coastFallSpeed = 120;
    [SerializeField, Min(.01f)] private float bodyClearance = .1f;
    private Dictionary<long, float> _coastalHeights = new();
    private Dictionary<long, float> _nextCoastalHeights = new();
    private readonly Collider2D[] _coastalBodies = new Collider2D[2048];
    private readonly List<Bounds> _coastalBounds = new();
    public string CoastalClearanceStatus { get; private set; } = "not sampled";
    public bool CoastalTerrainActive => usePhysicalCoasts && GeographicDepthActive;
    private static readonly Dictionary<int, BoatTerrainStreamer2D> Sources = new();
    private readonly Dictionary<long, BoatTerrainChunk2D> _loaded = new();
    private readonly HashSet<long> _committed = new();
    private readonly HashSet<long> _required = new();
    private readonly List<long> _retire = new();
    private readonly List<Transform> _interests = new();
    private readonly List<WorldItem> _loose = new();
    private readonly Dictionary<long, List<GameObject>> _suspended = new();
    private readonly Dictionary<long, float> _leases = new();
    private BoatTerrainPlan _plan;
    private TravelPayload _voyage;
    private float _originX, _nextDiscovery;
    private int _layer;
    private WorldMapTopographyField _depthField;
    private AnimationCurve _depthCurve;
    private float _seaLevel, _fallbackDepth, _forecastX, _forecastScale;
    private Vector2 _forecastWorld, _forecastDirection;
    private BoatSceneWorldPositionBridge _depthBridge;
    private BoatGeographicLandQuery _landQuery;
    private WorldMapTopographyField _landField;
    private float _landSeaLevel, _nextLandQuery;
    private BoatLandEncounter _landEncounter;
    public BoatLandEncounter LandEncounter => isActiveAndEnabled && GameplayAuthority.IsAuthoritative &&
        GameState.I != null && ReferenceEquals(_voyage, GameState.I.activeTravel) ? _landEncounter : default;
    public float LandQuerySampleSpacing => _landQuery != null ? _landQuery.SampleSpacing : 0;
    public int LandContextRevision { get; private set; }
    public int LandWorldSeed => _landField != null ? _landField.Seed : 0;
    public float WaterLevelY => profile != null ? profile.waterLevelY : 0;
    public bool GeographicDepthActive => _depthField != null && _depthBridge != null && _depthBridge.isActiveAndEnabled && _depthBridge.ProjectionReady;
    public float GeographicTargetDepth
    {
        get
        {
            if (!CoastalTerrainActive || _plan == null) return DesiredDepth(_forecastX - _originX);
            return WaterLevelY - BoatCoastalSurface.Target(_depthField.Sample01World(_forecastWorld), _seaLevel,
                WaterLevelY, _plan.Height(_forecastX - _originX), _depthCurve,
                Mathf.Max(1, maximumLandHeight), coastalDepthBand);
        }
    }
    public int FeatureCount => _plan != null ? _plan.FeatureCount : 0;
    public bool IsReady => _plan != null && _loaded.Count > 0;
    public int LoadedCount => _loaded.Count;
    public int CommittedCount => _committed.Count;
    public int InterestCount => _interests.Count;
    public float LastUsedBottomY => _plan != null ? _plan.BottomY : 0;
    public event Action<float> OnBottomYChanged;
    public event Action<long> ChunkCommitted;
    public event Action<long> ChunkLoaded;
    public event Action<long> ChunkUnloaded;

    public static bool TryGet(Scene scene, out BoatTerrainStreamer2D source)
    {
        return Sources.TryGetValue(scene.handle, out source) && source != null && source.isActiveAndEnabled;
    }

    private void OnEnable()
    {
        int key = gameObject.scene.handle;
        if (Sources.TryGetValue(key, out var existing) && existing != null && existing != this)
        {
            Debug.LogError("Only one BoatTerrainStreamer2D is allowed per scene.", this);
            enabled = false;
            return;
        }
        Sources[key] = this;
    }

    private void OnDisable()
    {
        int key = gameObject.scene.handle;
        if (Sources.TryGetValue(key, out var source) && source == this) Sources.Remove(key);
        // Retain existing physical chunks during a component pause.
        _landEncounter = default;
        _nextLandQuery = 0;
    }

    public bool Prepare(Vector2 boatPosition)
    {
        if (!GameplayAuthority.IsAuthoritative) return false;
        var voyage = GameState.I != null ? GameState.I.activeTravel : null;
        if (voyage == null || profile == null || groundMaterial == null) return false;
        var sampler = GetComponent<GeneratedGroundSampler2D>();
        if (sampler == null || sampler.StreamedSource != this)
        {
            Debug.LogError("Assign this streamer to the existing scene GeneratedGroundSampler2D Streamed Source.", this);
            return false;
        }
        var legacy = GetComponent<BoatSeaFloorGenerator2D>();
        var edge = GetComponent<EdgeCollider2D>();
        if (legacy != null && legacy.enabled || edge != null && edge.enabled)
        {
            Debug.LogError("Disable the legacy BoatScene ground generator and original edge before enabling streamed terrain.", this);
            return false;
        }
        if (_plan == null || !ReferenceEquals(_voyage, voyage))
        {
            Clear();
            _voyage = voyage;
            _originX = boatPosition.x;
            _layer = LayerMask.NameToLayer(groundLayerName);
            if (_layer < 0) { Debug.LogError("Streamed terrain Ground layer is missing.", this); return false; }
            int worldSeed = WorldMapRuntimeCache.I != null && WorldMapRuntimeCache.I.HasTopography
                ? WorldMapRuntimeCache.I.Field.Seed : 0;
            var cache = WorldMapRuntimeCache.I;
            _depthField = profile.useGeographicDepth && cache != null && cache.HasTopography ? cache.Field : null;
            _seaLevel = cache != null ? cache.EffectiveSeaLevel01 : 0f;
            if (!WorldTopology.IsFinite(_seaLevel) || _seaLevel <= 0f) _depthField = null;
            _fallbackDepth = profile.baseDepth;
            _depthCurve = profile.geographicDepth != null && profile.geographicDepth.length > 0
                ? new AnimationCurve(profile.geographicDepth.keys) : AnimationCurve.Linear(0, 15, 1, profile.maximumDepth);
            RefreshDepthForecast();
            try
            {
                BoatTerrainFeatureDirective[] features = Array.Empty<BoatTerrainFeatureDirective>();
                if (Debug.isDebugBuild && profile.enableDebugFeature)
                    features = new[] { new BoatTerrainFeatureDirective("debug-depth-proof", profile.debugFeatureCenter,
                        Mathf.Max(profile.debugFeatureHalfWidth, Mathf.Clamp(profile.chunkWidth, 8f, 1024f)), profile.debugFeatureDepthDelta,
                        profile.debugFeatureEdgeFraction, profile.debugFeatureMaximumSlopeDegrees) };
                _plan = new BoatTerrainPlan(profile, voyage.seed ^ worldSeed, _depthField != null ? DesiredDepth : null, features);
            }
            catch (ArgumentException error) { Debug.LogError(error.Message, this); return false; }
            OnBottomYChanged?.Invoke(_plan.BottomY);
            // Establish the starting floor at the boat's geographic depth before
            // slope constraints extend history towards either preload edge.
            Load(_plan.ChunkIndex(boatPosition.x - _originX));
        }
        RefreshLandEncounter();
        bool ready = EnsureCoverage(boatPosition.x, _plan.InterestRadius);
        if (ready) UpdateCoastalTerrain();
        return ready;
    }

    private void RefreshLandEncounter()
    {
        var cache = WorldMapRuntimeCache.I;
        if (_plan == null || GameState.I == null || !ReferenceEquals(_voyage, GameState.I.activeTravel) ||
            cache == null || !cache.HasTopography || !WorldNavigationService.TryGetTrueWorldPosition(out var world))
        { _landEncounter = default; return; }
        if (!ReferenceEquals(_landField, cache.Field) || _landSeaLevel != cache.EffectiveSeaLevel01)
        {
            _landEncounter = default; _landQuery = null;
            _landField = cache.Field; _landSeaLevel = cache.EffectiveSeaLevel01;
            LandContextRevision++;
            try { _landQuery = new BoatGeographicLandQuery(_landField, _landSeaLevel); }
            catch (ArgumentException error) { Debug.LogWarning(error.Message, this); }
        }
        _landEncounter = _landQuery != null ? _landQuery.Query(world, profile.landDetectionBaseRange,
            profile.landDetectionMaximumRange, profile.landDetectionSizeFactor,
            _landEncounter.LandmassId, profile.landEncounterSwitchMargin) : default;
    }

    public bool EnsureCoverage(float x, float radius)
    {
        if (_plan == null || !GameplayAuthority.IsAuthoritative ||
            float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(radius) || float.IsInfinity(radius)) return false;
        RefreshDepthForecast();
        radius = Mathf.Max(0, radius);
        long first = _plan.ChunkIndex(x - radius - _originX);
        long last = _plan.ChunkIndex(x + radius - _originX);
        // Reject a corrupt/unbounded request rather than allocating the whole world.
        if (first < int.MinValue || last > int.MaxValue || last < first || last - first > 128) return false;
        for (long i = first; i <= last; i++)
        {
            Load(i);
            _leases[i] = Time.unscaledTime + 2f;
        }
        return true;
    }

    /// <summary>Authoritative planning seam. Replaces future requests, never existing ground.</summary>
    public bool TrySetForecastFeatures(IReadOnlyList<BoatTerrainFeatureDirective> features, out string reason)
    {
        reason = string.Empty;
        if (!GameplayAuthority.IsAuthoritative || _plan == null)
        { reason = "Feature planning requires an authoritative prepared terrain stream."; return false; }
        try { _plan.SetForecastFeatures(features); return true; }
        catch (ArgumentException error) { reason = error.Message; return false; }
    }

    private void RefreshDepthForecast()
    {
        if (_depthField == null) return;
        if (_depthBridge != null && (!_depthBridge.isActiveAndEnabled || _depthBridge.gameObject.scene != gameObject.scene))
            _depthBridge = null;
        if (_depthBridge == null)
        {
            foreach (var bridge in FindObjectsByType<BoatSceneWorldPositionBridge>(FindObjectsSortMode.None))
            {
                if (bridge.gameObject.scene != gameObject.scene || !bridge.TryRefreshProjection()) continue;
                if (_depthBridge != null) { _depthBridge = null; return; }
                _depthBridge = bridge;
            }
        }
        if (_depthBridge == null || !_depthBridge.TryRefreshProjection()) return;
        _forecastX = _depthBridge.PilotingState.transform.position.x;
        _forecastScale = _depthBridge.WorldUnitsPerLocalUnit;
        if (!WorldNavigationService.TryGetTrueWorldPosition(out _forecastWorld))
            _forecastWorld = _depthBridge.ProjectNavigationPosition(_depthBridge.PilotingState.NavigationPosition);
        float angle = _depthBridge.GeographicHeadingDegrees * Mathf.Deg2Rad;
        _forecastDirection = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
    }

    private float DesiredDepth(double strip)
    {
        if (!GeographicDepthActive || _forecastScale <= 0f) return _fallbackDepth;
        Vector2 predicted = _forecastWorld + _forecastDirection * (float)((_originX + strip - _forecastX) * _forecastScale);
        float normalizedDepth = Mathf.Clamp01((_seaLevel - _depthField.Sample01World(predicted)) / Mathf.Max(.0001f, _seaLevel));
        float depth = _depthCurve.Evaluate(normalizedDepth);
        return WorldTopology.IsFinite(depth) ? depth : _fallbackDepth;
    }

    private void Load(long index)
    {
        if (_loaded.ContainsKey(index)) return;
        var obj = new GameObject($"Seabed_{index}");
        obj.transform.SetParent(transform, false);
        var chunk = obj.AddComponent<BoatTerrainChunk2D>();
        chunk.Build(_plan, index, _originX, groundMaterial, _layer,
            SortingLayer.NameToID(sortingLayerName), sortingOrder);
        _loaded.Add(index, chunk);
        if (_committed.Add(index)) ChunkCommitted?.Invoke(index);
        if (_suspended.TryGetValue(index, out var items))
        {
            foreach (var item in items) if (item != null) item.SetActive(true);
            _suspended.Remove(index);
        }
        ChunkLoaded?.Invoke(index);
    }

    private void FixedUpdate()
    {
        if (!GameplayAuthority.IsAuthoritative) return;
        if (Time.unscaledTime >= _nextLandQuery)
        {
            RefreshLandEncounter();
            _nextLandQuery = Time.unscaledTime + .25f;
        }
        if (Time.unscaledTime >= _nextDiscovery)
        {
            DiscoverInterests();
            _nextDiscovery = Time.unscaledTime + 1f;
        }
        if (_plan == null) return; // BoatSpawner establishes the origin before restore.
        _required.Clear();
        foreach (var actor in _interests)
        {
            if (actor == null || !actor.gameObject.activeInHierarchy) continue;
            float x = actor.position.x;
            if (!EnsureCoverage(x, _plan.InterestRadius)) continue;
            long first = _plan.ChunkIndex(x - _plan.InterestRadius - _plan.UnloadBuffer - _originX);
            long last = _plan.ChunkIndex(x + _plan.InterestRadius + _plan.UnloadBuffer - _originX);
            for (long i = first; i <= last; i++) _required.Add(i);
        }
        // Fail closed while discovery/bootstrap has no physical actor to keep ground alive.
        if (_interests.Count == 0) return;
        _retire.Clear();
        foreach (var pair in _loaded)
            if (!_required.Contains(pair.Key) && (!_leases.TryGetValue(pair.Key, out float until) || until < Time.unscaledTime))
                _retire.Add(pair.Key);
        foreach (long index in _retire)
        {
            SuspendLooseItems(index);
            _loaded[index].gameObject.SetActive(false); // Disable collision before deferred Destroy.
            Dispose(_loaded[index].gameObject);
            _loaded.Remove(index); _leases.Remove(index);
            ChunkUnloaded?.Invoke(index);
        }
        UpdateCoastalTerrain();
    }

    private void UpdateCoastalTerrain()
    {
        if (!CoastalTerrainActive) return;
        RefreshDepthForecast();
        Physics2D.SyncTransforms();
        if (!TryGetWorldSpan(out float minX, out float maxX)) return;
        float top = WaterLevelY + Mathf.Max(1, maximumLandHeight) + 2;
        float bottom = _plan.BottomY - 2;
        // Query every step so freshly dropped/spawned bodies are protected too.
        int bodyCount = Physics2D.OverlapBox(new Vector2((minX + maxX) * .5f, (top + bottom) * .5f),
            new Vector2(maxX - minX + (top - bottom) * 2, top - bottom), 0,
            new ContactFilter2D().NoFilter(), _coastalBodies);
        if (bodyCount == _coastalBodies.Length)
        { CoastalClearanceStatus = "collider query full; terrain updates paused"; return; }
        _coastalBounds.Clear();
        Collider2D limitingCollider = null;
        float boatCeiling = float.PositiveInfinity;
        for (int i = 0; i < bodyCount; i++)
        {
            var collider = _coastalBodies[i];
            if (collider != null && collider.enabled && !collider.isTrigger && collider.gameObject.scene == gameObject.scene &&
                collider.GetComponent<BoatTerrainChunk2D>() == null &&
                collider.attachedRigidbody != null && collider.attachedRigidbody.simulated &&
                collider.attachedRigidbody.bodyType != RigidbodyType2D.Static &&
                !Physics2D.GetIgnoreLayerCollision(_layer, collider.gameObject.layer))
            {
                Bounds bounds = collider.bounds;
                _coastalBounds.Add(bounds);
                float ceiling = BoatCoastalSurface.BodyCeiling(_forecastX, bounds, bodyClearance, 1.73205f);
                if (ceiling < boatCeiling) { boatCeiling = ceiling; limitingCollider = collider; }
            }
        }
        if (limitingCollider != null)
        {
            string owner = limitingCollider.attachedRigidbody.name;
            float targetY = WaterLevelY - GeographicTargetDepth;
            CoastalClearanceStatus = $"{owner}/{limitingCollider.name} ({limitingCollider.GetType().Name}); " +
                $"bottom {limitingCollider.bounds.min.y:0.00}; ceiling at boat {boatCeiling:0.00}; " +
                $"target Y {targetY:0.00}{(targetY > boatCeiling ? "; uplift limited" : "; target clear")}";
        }
        else CoastalClearanceStatus = "no body clearance limit";
        _nextCoastalHeights.Clear();
        foreach (var pair in _loaded)
        {
            long firstSample = pair.Key * _plan.Segments;
            pair.Value.UpdateSurface(i =>
            {
                long key = firstSample + i;
                // Shared global sample keys give adjacent chunks identical endpoints,
                // regardless of dictionary iteration order or an unloading neighbour.
                if (_nextCoastalHeights.TryGetValue(key, out float shared)) return shared;
                double strip = key * (double)_plan.Step;
                float x = (float)(_originX + strip);
                float baseY = _plan.Height(strip);
                Vector2 world = _forecastWorld + _forecastDirection * ((x - _forecastX) * _forecastScale);
                float target = BoatCoastalSurface.Target(_depthField.Sample01World(world), _seaLevel,
                    WaterLevelY, baseY, _depthCurve, Mathf.Max(1, maximumLandHeight), coastalDepthBand);
                bool established = _coastalHeights.TryGetValue(key, out float prior);
                float current = established ? prior : baseY;
                float ceiling = float.PositiveInfinity;
                // A 60 degree clearance envelope prevents an abrupt vertical wall
                // forming directly beside a protected body's footprint.
                foreach (var bounds in _coastalBounds)
                    ceiling = Mathf.Min(ceiling, BoatCoastalSurface.BodyCeiling(x, bounds, bodyClearance, 1.73205f));
                // New coverage starts at its coastal target immediately. Do not
                // let the boat sail through a newly loaded beach while it grows.
                float y = established ? BoatCoastalSurface.Advance(current, target, ceiling, Time.fixedDeltaTime,
                    Mathf.Max(1, coastRiseSpeed), Mathf.Max(1, coastFallSpeed)) : Mathf.Min(target, Mathf.Max(baseY, ceiling));
                _nextCoastalHeights.Add(key, y);
                return y;
            });
        }
        (_coastalHeights, _nextCoastalHeights) = (_nextCoastalHeights, _coastalHeights);
    }

    private void DiscoverInterests()
    {
        _interests.Clear(); _loose.Clear();
        foreach (var boat in FindObjectsByType<Boat>(FindObjectsSortMode.None))
            if (boat.gameObject.scene == gameObject.scene) _interests.Add(boat.transform);
        foreach (var player in FindObjectsByType<CharacterPlayer>(FindObjectsSortMode.None))
        {
            if (player.gameObject.scene != gameObject.scene) continue;
            var death = player.GetComponent<Survival.Death.PlayerDeathSystem>();
            if (death == null || !death.IsDead) _interests.Add(player.transform);
        }
        foreach (var item in FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
        {
            if (item.gameObject.scene != gameObject.scene) continue;
            var payload = item.GetComponent<TetherPayload>();
            var owner = item.GetComponent<BoatOwnedItem>();
            if (payload != null && payload.ActiveDock == null ||
                owner != null && owner.IsOwnedByBoat ||
                item.Item != null && item.Item.WorldPersistence == WorldItemPersistencePolicy.PersistentWorld)
                _interests.Add(item.transform);
            else _loose.Add(item);
        }
    }

    private void SuspendLooseItems(long index)
    {
        foreach (var item in _loose)
        {
            if (item == null || !item.gameObject.activeInHierarchy || item.GetComponentInParent<Boat>() != null) continue;
            var owned = item.GetComponent<BoatOwnedItem>();
            if (owned != null && owned.IsOwnedByBoat) continue;
            if (_plan.ChunkIndex(item.transform.position.x - _originX) != index) continue;
            // Ordinary discarded items do not pin ground forever. Preserve their instances in stasis.
            if (!_suspended.TryGetValue(index, out var items)) _suspended[index] = items = new List<GameObject>();
            items.Add(item.gameObject);
            item.gameObject.SetActive(false);
        }
    }

    public bool TrySampleGround(float x, out float y, out float slope)
    {
        y = slope = 0;
        if (_plan == null || float.IsNaN(x) || float.IsInfinity(x)) return false;
        long index = _plan.ChunkIndex(x - _originX);
        return _loaded.TryGetValue(index, out var chunk) && chunk.TrySample(x, out y, out slope);
    }

    public bool EnsureAndCorrectBody(Rigidbody2D body)
    {
        if (body == null || !GameplayAuthority.IsAuthoritative) return false;
        var colliders = body.GetComponentsInChildren<Collider2D>();
        Physics2D.SyncTransforms();
        float correction = 0;
        foreach (var collider in colliders)
        {
            if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != body) continue;
            Bounds bounds = collider.bounds;
            if (!EnsureCoverage(bounds.center.x, bounds.extents.x + 2f)) return false;
            // Sample the whole footprint at the same density as the physical surface.
            int samples = Mathf.Clamp(Mathf.CeilToInt(bounds.size.x / _plan.Step), 1, 2048);
            for (int i = 0; i <= samples; i++)
            {
                float x = Mathf.Lerp(bounds.min.x, bounds.max.x, i / (float)samples);
                if (!TrySampleGround(x, out float y, out _)) return false;
                correction = Mathf.Max(correction, y + .04f - bounds.min.y);
            }
        }
        if (correction > 0)
        {
            body.position += Vector2.up * correction;
            Vector2 velocity = body.linearVelocity;
            velocity.y = Mathf.Max(0, velocity.y); body.linearVelocity = velocity;
            Physics2D.SyncTransforms();
        }
        return true;
    }

    public void CorrectRestoredExternalItems()
    {
        foreach (var item in FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
        {
            if (item.gameObject.scene != gameObject.scene || item.GetComponentInParent<Boat>() != null) continue;
            var bell = item.GetComponentInParent<DivingBellOccupancy>();
            if (bell != null && bell.gameObject != item.gameObject) continue;
            var payload = item.GetComponent<TetherPayload>();
            var owned = item.GetComponent<BoatOwnedItem>();
            if (payload != null && payload.ActiveDock != null ||
                payload == null && owned != null && owned.IsOwnedByBoat) continue;
            var body = item.GetComponent<Rigidbody2D>();
            if (body != null && !EnsureAndCorrectBody(body))
                Debug.LogError($"Cannot establish safe streamed ground for restored item {item.name}.", item);
        }
        DiscoverInterests();
    }

    public bool TryGetWorldSpan(out float minX, out float maxX)
    {
        minX = maxX = 0;
        if (_loaded.Count == 0) return false;
        long min = long.MaxValue, max = long.MinValue;
        foreach (long index in _loaded.Keys) { min = Math.Min(min, index); max = Math.Max(max, index); }
        minX = (float)(_originX + min * (double)_plan.Width);
        maxX = (float)(_originX + (max + 1d) * _plan.Width);
        return true; // Span is an envelope; TrySample still rejects unloaded gaps.
    }

    private void Clear()
    {
        foreach (var chunk in _loaded.Values) if (chunk != null) { chunk.gameObject.SetActive(false); Dispose(chunk.gameObject); }
        foreach (var items in _suspended.Values) foreach (var item in items) if (item != null) item.SetActive(true);
        _loaded.Clear(); _committed.Clear(); _leases.Clear(); _suspended.Clear();
        _plan = null;
        _coastalHeights.Clear(); _nextCoastalHeights.Clear();
        Array.Clear(_coastalBodies, 0, _coastalBodies.Length); _coastalBounds.Clear();
        CoastalClearanceStatus = "not sampled";
        _landQuery = null; _landField = null; _landEncounter = default; _nextLandQuery = 0;
    }

    private void OnDestroy() { Clear(); }

    private static void Dispose(GameObject obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
