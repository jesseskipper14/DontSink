using UnityEngine;

/// <summary>
/// Authoritative scene-level piloting simulation.
///
/// Players/controllers submit ephemeral BoatControlIntent through a helm.
/// Persistent throttle/rudder/navigation state belongs to BoatPilotingState.
///
/// The physical side-view Boat Rigidbody is authoritative for DISTANCE TRAVELED.
/// Virtual navigation owns only the geographic heading / projection of that travel.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoatPilotingState))]
public sealed class BoatPilotingSimulation : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoatPilotingState state;
    [SerializeField] private Boat boat;
    [SerializeField] private ThrottleForce throttleForce;

    [Header("Physical Scene Axis")]
    [Tooltip("The side-view BoatScene travel axis. Current BoatScenes use world +X as forward.")]
    [SerializeField] private Vector2 sceneForwardAxis = Vector2.right;

    [Header("Control Travel")]
    [Tooltip("How quickly W/S physically moves the signed throttle lever through reverse, neutral, and forward.")]
    [SerializeField, Min(0f)] private float throttleTravelPerSecond = 0.45f;

    [Tooltip("Maximum rudder angle in either direction.")]
    [SerializeField, Min(0.1f)] private float maxRudderDegrees = 35f;

    [Tooltip("How quickly A/D physically moves the rudder. Releasing the key leaves it where it is.")]
    [SerializeField, Min(0f)] private float rudderTravelDegreesPerSecond = 55f;

    [Header("Virtual Heading Prototype")]
    [SerializeField, Min(0f)] private float rudderAngularAcceleration = 34f;
    [SerializeField, Min(0f)] private float angularWaterDrag = 1.9f;
    [SerializeField, Min(0f)] private float maxAngularVelocityDegrees = 42f;

    [Tooltip("Real BoatScene forward speed at which the rudder reaches full prototype authority.")]
    [SerializeField, Min(0.01f)] private float fullRudderAuthoritySpeed = 2.5f;

    [Header("Route Guidance Prototype")]
    [Tooltip("Temporary deterministic seed for the recommended route. Later this should come from route/world state.")]
    [SerializeField] private int routePrototypeSeed = 17357;

    [Tooltip("Distance between generated route control points.")]
    [SerializeField, Min(2f)] private float routePointSpacing = 10f;

    [Tooltip("Maximum recommended-route heading away from straight navigation-forward.")]
    [SerializeField, Range(1f, 45f)] private float routeMaxHeadingDegrees = 15f;

    [Tooltip("Route progress only advances while the boat remains within this broader distance of the recommended route. " +
             "This is NOT the adherence-scoring width.")]
    [SerializeField, Min(1f)] private float routeProgressCorridor = 30f;

    [Tooltip("Full world-space width of the visible wake/path. This same width is used for adherence scoring.")]
    [SerializeField, Min(0.5f)] private float routeAdherencePathWidth = 9.75f;

    [Tooltip("Distance-based memory for Recent Course Quality. Larger values make recent performance change more slowly.")]
    [SerializeField, Min(1f)] private float recentCourseQualityDistance = 40f;

    [Tooltip("Recent course quality above this becomes Handling Efficiency. Below this becomes Course Instability. " +
             "Phase 1 only measures both values; it does not apply buffs or penalties yet.")]
    [SerializeField, Range(0.05f, 0.95f)] private float courseNeutralQuality = 0.65f;

    [Tooltip("How far ahead in navigation-world Y the simulation keeps route geometry generated.")]
    [SerializeField, Min(100f)] private float routeCoverageAhead = 1200f;

    private object _controlOwner;
    private BoatControlIntent _currentIntent;

    private int _installedPropulsionSources;
    private int _activePropulsionSources;

    private float _physicalForwardSpeed;
    private float _physicalTravelDelta;

    private Vector2 _lastPhysicalPosition;
    private bool _hasLastPhysicalPosition;

    private BoatPilotingRouteState _routeGuidance;

    public BoatPilotingState State => state;
    public BoatPilotingRouteState RouteGuidance => _routeGuidance;
    public BoatControlIntent CurrentIntent => _currentIntent;

    public bool HasControlOwner => _controlOwner != null;
    public float MaxRudderDegrees => maxRudderDegrees;

    public int InstalledPropulsionSources => _installedPropulsionSources;
    public int ActivePropulsionSources => _activePropulsionSources;
    public bool HasAvailablePropulsion => _activePropulsionSources > 0;

    /// <summary>
    /// Signed speed of the actual Boat Rigidbody along the BoatScene's travel axis.
    /// Positive = scene-forward, negative = scene-reverse.
    /// </summary>
    public float PhysicalForwardSpeed => _physicalForwardSpeed;

    /// <summary>
    /// Signed physical distance actually traveled along the BoatScene axis
    /// since the previous simulation tick.
    /// </summary>
    public float PhysicalTravelDelta => _physicalTravelDelta;

    private void Reset()
    {
        ResolveRefs();
    }

    private void Awake()
    {
        ResolveRefs();
        ResetPhysicalPositionSample();
        InitializeRouteGuidanceIfNeeded();

        if (state != null && throttleForce != null)
            throttleForce.SetThrottle(state.Throttle);
    }

    private void OnEnable()
    {
        ResetPhysicalPositionSample();
        InitializeRouteGuidanceIfNeeded();
    }

    private void OnValidate()
    {
        throttleTravelPerSecond = Mathf.Max(0f, throttleTravelPerSecond);
        maxRudderDegrees = Mathf.Max(0.1f, maxRudderDegrees);
        rudderTravelDegreesPerSecond = Mathf.Max(0f, rudderTravelDegreesPerSecond);

        rudderAngularAcceleration = Mathf.Max(0f, rudderAngularAcceleration);
        angularWaterDrag = Mathf.Max(0f, angularWaterDrag);
        maxAngularVelocityDegrees = Mathf.Max(0f, maxAngularVelocityDegrees);
        fullRudderAuthoritySpeed = Mathf.Max(0.01f, fullRudderAuthoritySpeed);

        routePointSpacing = Mathf.Max(2f, routePointSpacing);
        routeMaxHeadingDegrees = Mathf.Clamp(routeMaxHeadingDegrees, 1f, 45f);
        routeProgressCorridor = Mathf.Max(1f, routeProgressCorridor);
        routeAdherencePathWidth = Mathf.Max(0.5f, routeAdherencePathWidth);
        recentCourseQualityDistance = Mathf.Max(1f, recentCourseQualityDistance);
        courseNeutralQuality = Mathf.Clamp(courseNeutralQuality, 0.05f, 0.95f);
        routeCoverageAhead = Mathf.Max(100f, routeCoverageAhead);

        if (sceneForwardAxis.sqrMagnitude <= 0.000001f)
            sceneForwardAxis = Vector2.right;
    }

    public bool CanClaimControl(object owner)
    {
        if (owner == null)
            return false;

        return _controlOwner == null || ReferenceEquals(_controlOwner, owner);
    }

    public bool TryClaimControl(object owner)
    {
        if (!CanClaimControl(owner))
            return false;

        _controlOwner = owner;
        _currentIntent = BoatControlIntent.Neutral;
        return true;
    }

    public bool SubmitIntent(object owner, BoatControlIntent intent)
    {
        if (owner == null || !ReferenceEquals(_controlOwner, owner))
            return false;

        _currentIntent = intent;
        return true;
    }

    /// <summary>
    /// Removes input authority only. Persistent boat state deliberately survives.
    /// </summary>
    public void ReleaseControl(object owner)
    {
        if (owner == null || !ReferenceEquals(_controlOwner, owner))
            return;

        _currentIntent = BoatControlIntent.Neutral;
        _controlOwner = null;
    }

    public bool HasControlAuthority(object owner)
    {
        return owner != null && ReferenceEquals(_controlOwner, owner);
    }

    private void FixedUpdate()
    {
        ResolveRefs();

        if (state == null)
            return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f)
            return;

        AdvancePhysicalControls(dt);

        if (throttleForce != null)
        {
            throttleForce.SetThrottle(state.Throttle);
            throttleForce.GetPropulsionStatus(
                out _installedPropulsionSources,
                out _activePropulsionSources);
        }
        else
        {
            _installedPropulsionSources = 0;
            _activePropulsionSources = 0;
        }

        SampleActualBoatMotion(dt);

        // Crucial rule:
        // the navigation simulation cannot invent forward/reverse distance.
        // It may only project distance the real Boat Rigidbody actually traveled.
        AdvanceVirtualNavigation(
            dt,
            _physicalForwardSpeed,
            _physicalTravelDelta);

        AdvanceRouteGuidance();
    }

    private void InitializeRouteGuidanceIfNeeded()
    {
        if (_routeGuidance != null &&
            _routeGuidance.IsInitialized)
        {
            return;
        }

        ResolveRefs();

        if (state == null)
            return;

        _routeGuidance =
            new BoatPilotingRouteState();

        _routeGuidance.Initialize(
            state.NavigationPosition,
            routePrototypeSeed,
            routePointSpacing,
            routeMaxHeadingDegrees,
            routeProgressCorridor,
            routeAdherencePathWidth,
            recentCourseQualityDistance,
            courseNeutralQuality,
            routeCoverageAhead);
    }

    private void AdvanceRouteGuidance()
    {
        InitializeRouteGuidanceIfNeeded();

        if (_routeGuidance == null ||
            state == null)
        {
            return;
        }

        _routeGuidance.EnsureCoverageToWorldY(
            state.NavigationPosition.y +
            routeCoverageAhead);

        _routeGuidance.UpdateProgress(
            state.NavigationPosition);

        _routeGuidance.RecordPhysicalTravel(
            _physicalTravelDelta);
    }

    private void AdvancePhysicalControls(float dt)
    {
        float throttle =
            state.Throttle +
            _currentIntent.ThrottleAdjust *
            throttleTravelPerSecond *
            dt;

        throttle = Mathf.Clamp(throttle, -1f, 1f);

        float rudder =
            state.RudderDegrees +
            _currentIntent.RudderAdjust *
            rudderTravelDegreesPerSecond *
            dt;

        rudder =
            Mathf.Clamp(
                rudder,
                -maxRudderDegrees,
                maxRudderDegrees);

        state.SetControlPositions(
            throttle,
            rudder);
    }

    private void SampleActualBoatMotion(float dt)
    {
        _physicalForwardSpeed = 0f;
        _physicalTravelDelta = 0f;

        if (boat == null || boat.rb == null)
        {
            _hasLastPhysicalPosition = false;
            return;
        }

        Vector2 axis = GetSceneForwardAxis();

        // This is the current signed physical scene speed.
        // It is intentionally WORLD/SCENE-axis based rather than hull-relative,
        // because wave pitch should not redefine forward vs reverse travel.
        // Rigidbody velocity can be stale or temporarily clamped relative to the
        // displacement that actually occurred between physics steps. Navigation
        // therefore treats POSITION DELTA as the authoritative travel record.
        Vector2 currentPosition =
            boat.rb.position;

        if (_hasLastPhysicalPosition)
        {
            Vector2 physicalDelta =
                currentPosition -
                _lastPhysicalPosition;

            _physicalTravelDelta =
                Vector2.Dot(
                    physicalDelta,
                    axis);

            _physicalForwardSpeed =
                dt > 0.000001f
                    ? _physicalTravelDelta / dt
                    : 0f;
        }
        else
        {
            _physicalTravelDelta = 0f;
            _physicalForwardSpeed = 0f;
        }

        _lastPhysicalPosition =
            currentPosition;

        _hasLastPhysicalPosition = true;
    }

    private void AdvanceVirtualNavigation(
        float dt,
        float physicalForwardSpeed,
        float physicalTravelDelta)
    {
        Vector2 headingForward =
            HeadingToForward(
                state.HeadingDegrees);

        float speedMagnitude =
            Mathf.Abs(
                physicalForwardSpeed);

        float rudderNormalized =
            Mathf.Clamp(
                state.RudderDegrees /
                maxRudderDegrees,
                -1f,
                1f);

        float rudderAuthority01 =
            Mathf.Clamp01(
                speedMagnitude /
                fullRudderAuthoritySpeed);

        // Reverse actual scene travel reverses rudder yaw.
        // Near zero speed the rudder has no steering authority.
        float travelSign =
            speedMagnitude > 0.0001f
                ? Mathf.Sign(physicalForwardSpeed)
                : 0f;

        float angularVelocity =
            state.AngularVelocityDegrees;

        float angularAcceleration =
            rudderNormalized *
            rudderAngularAcceleration *
            rudderAuthority01 *
            travelSign;

        angularVelocity +=
            angularAcceleration *
            dt;

        angularVelocity *=
            Mathf.Exp(
                -angularWaterDrag *
                dt);

        angularVelocity =
            Mathf.Clamp(
                angularVelocity,
                -maxAngularVelocityDegrees,
                maxAngularVelocityDegrees);

        float heading =
            state.HeadingDegrees +
            angularVelocity *
            dt;

        // No directional blending, no fake lateral speed, no throttle-derived travel.
        // Positive physical delta moves along the bow heading.
        // Negative physical delta moves backward along that same heading.
        Vector2 navigationPosition =
            state.NavigationPosition +
            headingForward *
            physicalTravelDelta;

        // Diagnostic/presentation velocity only. Its magnitude comes from the real
        // BoatScene velocity and its sign naturally sends reverse behind the bow.
        Vector2 navigationVelocity =
            headingForward *
            physicalForwardSpeed;

        state.SetNavigationState(
            navigationPosition,
            navigationVelocity,
            heading,
            angularVelocity);
    }

    private void ResetPhysicalPositionSample()
    {
        ResolveRefs();

        if (boat != null && boat.rb != null)
        {
            _lastPhysicalPosition = boat.rb.position;
            _hasLastPhysicalPosition = true;
        }
        else
        {
            _lastPhysicalPosition = Vector2.zero;
            _hasLastPhysicalPosition = false;
        }

        _physicalTravelDelta = 0f;
        _physicalForwardSpeed = 0f;
    }

    private Vector2 GetSceneForwardAxis()
    {
        if (sceneForwardAxis.sqrMagnitude <= 0.000001f)
            return Vector2.right;

        return sceneForwardAxis.normalized;
    }

    private void ResolveRefs()
    {
        if (state == null)
            state =
                GetComponent<BoatPilotingState>();

        if (boat == null)
            boat =
                GetComponent<Boat>() ??
                GetComponentInParent<Boat>();

        if (throttleForce == null)
        {
            if (boat != null)
            {
                throttleForce =
                    boat.GetComponent<ThrottleForce>() ??
                    boat.GetComponentInChildren<ThrottleForce>(true);
            }

            if (throttleForce == null)
            {
                throttleForce =
                    GetComponent<ThrottleForce>() ??
                    GetComponentInChildren<ThrottleForce>(true);
            }
        }
    }

    private static Vector2 HeadingToForward(
        float headingDegrees)
    {
        float radians =
            headingDegrees *
            Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Sin(radians),
            Mathf.Cos(radians));
    }
}