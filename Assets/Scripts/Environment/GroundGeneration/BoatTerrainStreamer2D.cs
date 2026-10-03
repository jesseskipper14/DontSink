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
            try { _plan = new BoatTerrainPlan(profile, voyage.seed ^ worldSeed); }
            catch (ArgumentException error) { Debug.LogError(error.Message, this); return false; }
            OnBottomYChanged?.Invoke(_plan.BottomY);
        }
        return EnsureCoverage(boatPosition.x, _plan.InterestRadius);
    }

    public bool EnsureCoverage(float x, float radius)
    {
        if (_plan == null || !GameplayAuthority.IsAuthoritative ||
            float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(radius) || float.IsInfinity(radius)) return false;
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
    }

    private void OnDestroy() { Clear(); }

    private static void Dispose(GameObject obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
