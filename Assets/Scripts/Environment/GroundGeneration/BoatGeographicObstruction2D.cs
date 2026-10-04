using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Authority-owned geographic detector driver. Barrier enforcement can be replaced independently.</summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class BoatGeographicObstruction2D : MonoBehaviour
{
    [SerializeField] private BoatPilotingSimulation simulation;
    [Header("Geography")]
    [Tooltip("Map-unit tolerance using sampled distance from water. Some shore overlap is deliberately allowed.")]
    [SerializeField, Min(0f)] private float coastTolerance = 1.5f;
    [Header("Physical barrier")]
    [SerializeField] private string barrierLayerName = "BoatLandBarrier";
    [SerializeField] private LayerMask hullLayers = 1 << 3;
    [SerializeField, Min(.05f)] private float lookAheadSeconds = .5f;
    [SerializeField, Min(.1f)] private float minimumProbeDistance = 2f;
    [SerializeField, Min(.05f)] private float barrierThickness = 1f;
    [SerializeField, Min(1f)] private float extraBarrierHeight = 50f;
    [SerializeField, Min(.001f)] private float hullClearance = .02f;
    private static readonly Dictionary<int, BoatGeographicObstruction2D> Sources = new();
    private BoatPilotingSimulation _active;
    private BoatSceneWorldPositionBridge _bridge;
    private WorldMapTopographyField _field;
    private float _sea;
    private BoatGeographicObstructionQuery _query;
    private BoatLandBarrierProxy _proxy;
    private Rigidbody2D _body;
    private object _voyage;
    private int _warpRevision, _direction;
    private float _nextResolve;
    private readonly List<Collider2D> _hullCheck = new();
    public string Status { get; private set; } = "waiting for setup";
    public BoatObstructionResult LastResult { get; private set; }
    public bool BarrierActive => _proxy != null && _proxy.Active;
    public float LastNavigationFraction { get; private set; } = 1;
    public static bool TryGet(Scene scene, out BoatGeographicObstruction2D source) =>
        Sources.TryGetValue(scene.handle, out source) && source != null && source.isActiveAndEnabled;

    private void OnEnable()
    {
        if (Sources.TryGetValue(gameObject.scene.handle, out var other) && other != null && other != this)
        { Debug.LogError("Only one geographic boat obstruction driver is allowed per scene.", this); enabled = false; return; }
        Sources[gameObject.scene.handle] = this;
    }

    private void FixedUpdate()
    {
        if (BoatTerrainStreamer2D.TryGet(gameObject.scene, out var terrain) && terrain.CoastalTerrainActive)
        { Release("physical coastal terrain; barrier retired"); return; }
        if (!GameplayAuthority.IsAuthoritative) { Release("non-authoritative"); return; }
        if (Time.unscaledTime >= _nextResolve)
        { Resolve(); _nextResolve = Time.unscaledTime + 1; }
        if (_active == null || !_active.isActiveAndEnabled || _active.Boat == null || _active.Boat.rb == null ||
            !_active.Boat.rb.simulated || _active.Boat.rb.bodyType != RigidbodyType2D.Dynamic ||
            _bridge == null || !_bridge.TryRefreshProjection() || !_active.VoyageStrip.IsActive)
        { Release("boat/projection unavailable"); return; }
        var cache = WorldMapRuntimeCache.I;
        if (cache == null || !cache.HasTopography)
        { Release("topography unavailable"); return; }
        EnsureQuery(cache);
        var voyage = GameState.I != null ? GameState.I.activeTravel : null;
        if (!ReferenceEquals(_voyage, voyage) || _warpRevision != _bridge.DebugWarpRevision || _body != _active.Boat.rb)
        {
            Release("new navigation context"); _proxy?.Dispose(); _proxy = null;
            _body = _active.Boat.rb; _voyage = voyage; _warpRevision = _bridge.DebugWarpRevision;
        }
        if (_query == null || !WorldNavigationService.TryGetTrueWorldPosition(out var world))
        { Release("geography unavailable"); return; }
        int layer = LayerMask.NameToLayer(barrierLayerName);
        if (layer < 0 || hullLayers.value == 0) { Release("assign dedicated barrier layer and hull mask"); return; }
        Vector2 axis = _active.SceneForwardAxis;
        float speed = Vector2.Dot(_body.linearVelocity, axis);
        int direction = Mathf.Abs(speed) > .05f ? (speed > 0 ? 1 : -1) :
            Mathf.Abs(_active.State.Throttle) > .01f ? (_active.State.Throttle > 0 ? 1 : -1) : _direction;
        if (direction == 0) { Release("ready; no attempted travel"); return; }
        float distance = Mathf.Max(minimumProbeDistance, Mathf.Abs(speed) * Mathf.Max(lookAheadSeconds, Time.fixedDeltaTime * 2));
        float angle = _bridge.GeographicHeadingDegrees * Mathf.Deg2Rad;
        Vector2 delta = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * (direction * distance * _bridge.WorldUnitsPerLocalUnit);
        LastResult = _query.Sweep(world, delta, coastTolerance);
        if (!LastResult.HasGeography) { Release("path outside valid geography; boundary pass deferred"); return; }
        if (!LastResult.Blocked) { Release("clear geographic path", false); return; }
        _direction = direction;
        _proxy ??= new BoatLandBarrierProxy(transform, _body, layer, hullLayers);
        float thickness = Mathf.Max(barrierThickness, Mathf.Abs(speed) * Time.fixedDeltaTime * 2 + barrierThickness);
        if (!_proxy.Place(axis, direction, LastResult.AllowedFraction * distance, thickness, extraBarrierHeight, hullClearance))
        { Release("barrier missing hull or overlap filter capacity exceeded", false); return; }
        Status = direction > 0 ? "forward land barrier" : "reverse land barrier";
    }

    private void EnsureQuery(WorldMapRuntimeCache cache)
    {
        if (ReferenceEquals(_field, cache.Field) && _sea == cache.EffectiveSeaLevel01) return;
        Release("rebuilding geography"); _field = cache.Field; _sea = cache.EffectiveSeaLevel01; _query = null;
        try { _query = new BoatGeographicObstructionQuery(_field, _sea); }
        catch (ArgumentException error) { Debug.LogWarning(error.Message, this); }
    }

    /// <summary>Final continuous guard for observed travel and virtual environmental drift.
    /// Does not invent motion or relocate the Rigidbody; the separate proxy supplies the physical stop.</summary>
    public bool ConstrainNavigation(BoatPilotingSimulation source, Vector2 previous, Vector2 proposed, out Vector2 allowed)
    {
        allowed = proposed; LastNavigationFraction = 1;
        if (BoatTerrainStreamer2D.TryGet(gameObject.scene, out var terrain) && terrain.CoastalTerrainActive) return false;
        if (!GameplayAuthority.IsAuthoritative || !isActiveAndEnabled || LayerMask.NameToLayer(barrierLayerName) < 0 || hullLayers.value == 0) return false;
        if (_active == null || Time.unscaledTime >= _nextResolve)
        { Resolve(); _nextResolve = Time.unscaledTime + 1; }
        if (_active != source || _bridge == null || !_bridge.TryRefreshProjection() || source.Boat == null || source.Boat.rb == null ||
            !source.Boat.rb.simulated || source.Boat.rb.bodyType != RigidbodyType2D.Dynamic || !source.VoyageStrip.IsActive) return false;
        bool hasHull = false;
        source.Boat.rb.GetComponentsInChildren(false, _hullCheck);
        foreach (var hull in _hullCheck)
            if (!hull.isTrigger && hull.enabled && hull.attachedRigidbody == source.Boat.rb && (hullLayers.value & (1 << hull.gameObject.layer)) != 0)
            { hasHull = true; break; }
        if (!hasHull) return false;
        var cache = WorldMapRuntimeCache.I;
        if (cache == null || !cache.HasTopography) return false;
        EnsureQuery(cache);
        if (_query == null) return false;
        var result = _query.Sweep(_bridge.ProjectNavigationPosition(previous), _bridge.ProjectNavigationVector(proposed - previous), coastTolerance);
        if (!result.HasGeography || !result.Blocked) return false;
        LastNavigationFraction = result.AllowedFraction;
        allowed = Vector2.LerpUnclamped(previous, proposed, result.AllowedFraction);
        return true;
    }

    private void Resolve()
    {
        _active = null;
        if (simulation != null && simulation.isActiveAndEnabled && simulation.gameObject.scene == gameObject.scene) _active = simulation;
        else foreach (var candidate in FindObjectsByType<BoatPilotingSimulation>(FindObjectsSortMode.None))
        {
            if (!candidate.isActiveAndEnabled || candidate.gameObject.scene != gameObject.scene) continue;
            if (_active != null) { _active = null; break; }
            _active = candidate;
        }
        _bridge = _active != null ? _active.GetComponent<BoatSceneWorldPositionBridge>() : null;
    }
    private void Release(string status, bool clearResult = true)
    { _proxy?.Release(); _direction = 0; Status = status; if (clearResult) LastResult = default; }
    private void OnDisable()
    {
        if (Sources.TryGetValue(gameObject.scene.handle, out var source) && source == this) Sources.Remove(gameObject.scene.handle);
        Release("disabled"); _proxy?.Dispose(); _proxy = null; _nextResolve = 0; _query = null; _field = null;
        LastNavigationFraction = 1;
    }
    private void OnDestroy() { _proxy?.Dispose(); _proxy = null; }
}
