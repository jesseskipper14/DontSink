using UnityEngine;

/// <summary>
/// Phase 2.5 bridge between BoatScene's local piloting/navigation space and the
/// continuous global world-map coordinate space.
///
/// Local BoatScene navigation remains free to evolve. Celestial/world systems only
/// consume WorldNavigationService and therefore do not need to know about piloting.
///
/// Projection model:
/// - local +Y = along the source -> destination world-map direction
/// - local +X = starboard/right of that direction
/// - local scene distance is scaled so BoatScene's nominal trip length maps to the
///   actual source/destination world-map distance
///
/// The result is intentionally not clamped to the route or world bounds. Going off
/// course must remain representable.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoatSceneWorldPositionBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoatPilotingState pilotingState;
    [SerializeField] private BoatSceneController boatSceneController;

    [Header("Fallback")]
    [Tooltip(
        "Used only when BoatSceneController cannot be resolved. Should match the " +
        "BoatScene nominal source-to-target travel distance.")]
    [SerializeField, Min(0.01f)] private float fallbackLocalTravelDistance = 200f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    private TravelPayload _payload;
    private Vector2 _routeForward;
    private Vector2 _routeRight;
    private float _worldUnitsPerLocalUnit;
    private bool _projectionReady;
    private Vector2 _debugWorldOffset;

    public bool ProjectionReady => _projectionReady;
    public float WorldUnitsPerLocalUnit => _worldUnitsPerLocalUnit;
    public BoatPilotingState PilotingState => pilotingState;
    public int DebugWarpRevision { get; private set; }

    public Vector2 ProjectNavigationVector(Vector2 localVector) =>
        (_routeRight * localVector.x + _routeForward * localVector.y) * _worldUnitsPerLocalUnit;

    public Vector2 ProjectNavigationPosition(Vector2 localPosition) =>
        _payload.fromWorldPosition + ProjectNavigationVector(localPosition) + _debugWorldOffset;

    public float GeographicHeadingDegrees
    {
        get
        {
            if (pilotingState == null || !_projectionReady) return 0f;
            float radians = pilotingState.HeadingDegrees * Mathf.Deg2Rad;
            Vector2 direction = ProjectNavigationVector(new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)));
            return Mathf.Repeat(Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg, 360f);
        }
    }

    /// <summary>
    /// Debug-only geographic relocation. The boat Rigidbody, crew, tethers, scene
    /// terrain and physical travel sample remain untouched. Navigation continues
    /// from the new location on the next simulation tick.
    /// </summary>
    public bool TryDebugWarp(Vector2 target, out string reason)
    {
        reason = string.Empty;
        if (!GameplayAuthority.IsAuthoritative)
        { reason = "Only simulation authority can warp."; return false; }
        ResolveReferences();
        RebuildProjection();
        if (!_projectionReady || pilotingState == null || !isActiveAndEnabled)
        { reason = "Warp requires an active BoatScene navigation projection."; return false; }
        if (!IsFinite(target))
        { reason = "Coordinates must be finite numbers."; return false; }

        Vector2 offset = target - (_payload.fromWorldPosition + ProjectNavigationVector(pilotingState.NavigationPosition));
        if (!IsFinite(offset))
        { reason = "Coordinates exceed the navigation projection's numeric range."; return false; }
        if (!WorldNavigationService.TrySetAuthoritativeTrueWorldPosition(target, WorldNavigationPositionSource.Debug))
        { reason = "World navigation state is unavailable."; return false; }
        // Do not jump local navigation: route guidance would generate every skipped
        // control point. This offset is debug session state, not saved voyage state.
        _debugWorldOffset = offset;
        DebugWarpRevision++;
        return true;
    }

    private static bool IsFinite(Vector2 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y);

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        RebuildProjection();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RebuildProjection();
    }

    private void FixedUpdate()
    {
        if (!GameplayAuthority.IsAuthoritative)
            return;

        if (!_projectionReady || !ReferenceEquals(_payload, GameState.I != null ? GameState.I.activeTravel : null))
        {
            RebuildProjection();
            if (!_projectionReady)
                return;
        }

        if (pilotingState == null)
        {
            ResolveReferences();
            if (pilotingState == null)
                return;
        }

        Vector2 local = pilotingState.NavigationPosition;

        Vector2 worldPosition = ProjectNavigationPosition(local);

        WorldNavigationService.TrySetAuthoritativeTrueWorldPosition(
            worldPosition,
            WorldNavigationPositionSource.TravelProjection);
    }

    [ContextMenu("Log World Position Bridge")]
    public void LogBridgeState()
    {
        ResolveReferences();
        RebuildProjection();

        Vector2 local =
            pilotingState != null
                ? pilotingState.NavigationPosition
                : Vector2.zero;

        bool hasWorld =
            WorldNavigationService.TryGetTrueWorldPosition(
                out Vector2 world);

        Debug.Log(
            $"[BoatSceneWorldPositionBridge] " +
            $"Ready={_projectionReady}, " +
            $"Authority={GameplayAuthority.IsAuthoritative}, " +
            $"LocalNav=({local.x:0.000}, {local.y:0.000}), " +
            $"World={(hasWorld ? $"({world.x:0.000}, {world.y:0.000})" : "<none>")}, " +
            $"Scale={_worldUnitsPerLocalUnit:0.000000}, " +
            $"RouteForward=({_routeForward.x:0.000}, {_routeForward.y:0.000}), " +
            $"From={Describe(_payload != null ? _payload.fromWorldPosition : Vector2.zero)}, " +
            $"To={Describe(_payload != null ? _payload.toWorldPosition : Vector2.zero)}",
            this);
    }

    private void ResolveReferences()
    {
        if (pilotingState == null)
            pilotingState = FindAnyObjectByType<BoatPilotingState>();

        if (boatSceneController == null)
            boatSceneController = FindAnyObjectByType<BoatSceneController>();
    }

    private void RebuildProjection()
    {
        _projectionReady = false;
        TravelPayload currentPayload =
            GameState.I != null
                ? GameState.I.activeTravel
                : null;
        if (!ReferenceEquals(_payload, currentPayload))
        {
            _debugWorldOffset = Vector2.zero;
            DebugWarpRevision++;
        }
        _payload = currentPayload;

        if (_payload == null ||
            !_payload.hasWorldRouteCoordinates)
        {
            if (verboseLogging)
            {
                Debug.LogWarning(
                    "[BoatSceneWorldPositionBridge] Active travel does not contain " +
                    "world-route coordinates. This is expected only for an old/debug payload.",
                    this);
            }

            return;
        }

        Vector2 routeDelta =
            WorldTopologyService.Delta(_payload.fromWorldPosition, _payload.toWorldPosition);

        float worldRouteDistance =
            routeDelta.magnitude;

        if (worldRouteDistance <= 0.0001f)
        {
            if (verboseLogging)
            {
                Debug.LogWarning(
                    "[BoatSceneWorldPositionBridge] Source/destination world coordinates " +
                    "are coincident; cannot build route basis.",
                    this);
            }

            return;
        }

        float localTravelDistance =
            ResolveLocalTravelDistance();

        if (localTravelDistance <= 0.0001f)
            return;

        _routeForward =
            routeDelta /
            worldRouteDistance;

        // +X in local navigation space means starboard/right.
        // If routeForward is north (0,1), this correctly becomes east (1,0).
        _routeRight =
            new Vector2(
                _routeForward.y,
                -_routeForward.x);

        _worldUnitsPerLocalUnit =
            worldRouteDistance /
            localTravelDistance;

        _projectionReady = true;

        if (verboseLogging)
        {
            Debug.Log(
                $"[BoatSceneWorldPositionBridge] Projection ready | " +
                $"worldRoute={worldRouteDistance:0.000} " +
                $"localRoute={localTravelDistance:0.000} " +
                $"scale={_worldUnitsPerLocalUnit:0.000000}",
                this);
        }
    }

    private float ResolveLocalTravelDistance()
    {
        if (boatSceneController != null)
        {
            float distance =
                boatSceneController.NominalTravelDistance;

            if (distance > 0.0001f)
                return distance;
        }

        return Mathf.Max(
            0.01f,
            fallbackLocalTravelDistance);
    }

    private static string Describe(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
