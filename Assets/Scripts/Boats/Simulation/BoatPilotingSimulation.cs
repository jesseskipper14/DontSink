using UnityEngine;
using UnityEngine.Serialization;

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
    [SerializeField] private BoatHandlingAggregator handlingAggregator;

    [Header("Physical Scene Axis")]
    [Tooltip("The side-view BoatScene travel axis. Current BoatScenes use world +X as forward.")]
    [SerializeField] private Vector2 sceneForwardAxis = Vector2.right;

    [Header("Control Travel")]
    [Tooltip("How quickly W/S physically moves the signed throttle lever through reverse, neutral, and forward.")]
    [SerializeField, Min(0f)] private float throttleTravelPerSecond = 0.45f;

    [Tooltip("Reference angle used to convert physical rudder angle into steering authority. Actual maximum rudder travel comes from installed RudderModule contributors.")]
    [FormerlySerializedAs("maxRudderDegrees")]
    [SerializeField, Min(0.01f)] private float referenceRudderAngleDegrees = 35f;

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
             "Course Instability now drives navigation-certainty loss; Handling Efficiency remains diagnostic.")]
    [SerializeField, Range(0.05f, 0.95f)] private float courseNeutralQuality = 0.65f;

    [Tooltip("How far ahead in navigation-world Y the simulation keeps route geometry generated.")]
    [SerializeField, Min(100f)] private float routeCoverageAhead = 1200f;

    [Header("Celestial Recovery Prototype")]
    [Tooltip("Minimum original-route distance ahead of the current projected position where a successful positional fix tries to rejoin.")]
    [SerializeField, Min(5f)] private float recoveryMinimumRejoinLeadDistance = 60f;

    [Tooltip("Additional route-ahead distance per unit the boat is displaced from the original route. Larger values make very lost recoveries broader and more gradual.")]
    [SerializeField, Min(0f)] private float recoveryRejoinLeadPerOffRouteUnit = 0.65f;

    [Tooltip("Approximate world-space spacing between sampled points on the generated recovery curve.")]
    [SerializeField, Min(2f)] private float recoveryCurvePointSpacing = 10f;

    [Header("Environmental Disturbance Prototype")]
    [Tooltip("Continuous physical-wave-phase-driven lateral/yaw disturbance. Installed handling resistance attenuates the resulting accelerations.")]
    [SerializeField]
    private PilotingEnvironmentalDisturbance environmentalDisturbance =
        new PilotingEnvironmentalDisturbance();

    [Tooltip("Environmental yaw is allowed more angular-velocity headroom than ordinary rudder steering so a strong sea shove can visibly throw the bow off course.")]
    [SerializeField, Min(1f)]
    private float environmentalAngularVelocityCeilingDegrees = 72f;

    private object _controlOwner;
    private BoatControlIntent _currentIntent;

    private int _installedPropulsionSources;
    private int _activePropulsionSources;

    private BoatHandlingProfile _handlingProfile;

    private float _physicalForwardSpeed;
    private float _physicalTravelDelta;

    private IWaveService _waveService;

    private Vector2 _environmentNavigationVelocity;
    private Vector2 _environmentLateralAcceleration;
    private float _environmentYawAngularAccelerationDegrees;
    private float _physicalWaveLoad01;

    private Vector2 _lastPhysicalPosition;
    private bool _hasLastPhysicalPosition;

    private BoatPilotingRouteState _routeGuidance;

    public BoatPilotingState State => state;
    public BoatPilotingRouteState RouteGuidance => _routeGuidance;
    public BoatControlIntent CurrentIntent => _currentIntent;

    /// <summary>
    /// Read-only current control authority. UI/diagnostics may inspect this,
    /// but ownership changes still go through TryClaimControl/ReleaseControl.
    /// </summary>
    public object ControlOwner => _controlOwner;
    public bool HasControlOwner => _controlOwner != null;

    public BoatHandlingProfile HandlingProfile => _handlingProfile;
    public bool HasSteering => _handlingProfile.HasSteering;
    public float MaxRudderDegrees => _handlingProfile.MaxTurnAngle;
    public float LateralDisturbanceResistance => _handlingProfile.LateralDisturbanceResistance;
    public float YawDisturbanceResistance => _handlingProfile.YawDisturbanceResistance;

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

    public float EnvironmentalWaveAmplitude => environmentalDisturbance != null ? environmentalDisturbance.CurrentWaveAmplitude : 0f;
    public float EnvironmentalSeverity01 => environmentalDisturbance != null ? environmentalDisturbance.Severity01 : 0f;
    public float EnvironmentalPhysicalWaveLoad01 => _physicalWaveLoad01;
    public float EnvironmentalBeamExposure01 => environmentalDisturbance != null ? environmentalDisturbance.BeamExposure01 : 0f;
    public float EnvironmentalEncounterBroadsideDegrees => environmentalDisturbance != null ? environmentalDisturbance.EncounterBroadsideDegrees : 0f;
    public Vector2 EnvironmentalNavigationVelocity => _environmentNavigationVelocity;
    public Vector2 EnvironmentalLateralAcceleration => _environmentLateralAcceleration;
    public float EnvironmentalYawAngularAccelerationDegrees => _environmentYawAngularAccelerationDegrees;
    public float EnvironmentalWaveFrequency => _waveService != null ? Mathf.Max(0f, _waveService.Frequency) : 0f;
    public float EnvironmentalWaveSpeed => _waveService != null ? Mathf.Max(0f, _waveService.Speed) : 0f;

    private void Reset()
    {
        ResolveRefs();
    }

    private void Awake()
    {
        ResolveRefs();
        if (environmentalDisturbance == null)
            environmentalDisturbance = new PilotingEnvironmentalDisturbance();
        RefreshHandlingProfile();
        ResetPhysicalPositionSample();
        InitializeRouteGuidanceIfNeeded();

        if (state != null && throttleForce != null)
            throttleForce.SetThrottle(state.Throttle);
    }

    private void OnEnable()
    {
        if (environmentalDisturbance == null)
            environmentalDisturbance = new PilotingEnvironmentalDisturbance();
        RefreshHandlingProfile();
        ResetPhysicalPositionSample();
        InitializeRouteGuidanceIfNeeded();
    }

    private void OnValidate()
    {
        throttleTravelPerSecond = Mathf.Max(0f, throttleTravelPerSecond);
        referenceRudderAngleDegrees = Mathf.Max(0.01f, referenceRudderAngleDegrees);
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

        recoveryMinimumRejoinLeadDistance =
            Mathf.Max(
                5f,
                recoveryMinimumRejoinLeadDistance);

        recoveryRejoinLeadPerOffRouteUnit =
            Mathf.Max(
                0f,
                recoveryRejoinLeadPerOffRouteUnit);

        recoveryCurvePointSpacing =
            Mathf.Max(
                2f,
                recoveryCurvePointSpacing);

        if (environmentalAngularVelocityCeilingDegrees <= 0f)
            environmentalAngularVelocityCeilingDegrees = 72f;

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

    /// <summary>
    /// Applies the downstream gameplay result of a successful celestial position
    /// fix. The current debug HUD button calls this directly; the future star-map
    /// alignment minigame should call the same operation after a valid match.
    /// </summary>
    public bool TryApplySuccessfulCelestialFix()
    {
        InitializeRouteGuidanceIfNeeded();

        if (state == null ||
            _routeGuidance == null ||
            !_routeGuidance.IsInitialized)
        {
            return false;
        }

        if (_routeGuidance.NavigationCertainty01 >=
            0.9999f)
        {
            return false;
        }

        return
            _routeGuidance.TryRecoverKnownPosition(
                state.NavigationPosition,
                state.HeadingDegrees,
                recoveryMinimumRejoinLeadDistance,
                recoveryRejoinLeadPerOffRouteUnit,
                recoveryCurvePointSpacing);
    }

    /// <summary>
    /// Applies a discrete persistent throttle order through the same authority
    /// boundary used by live control. This is for consoles/automation, not a
    /// second propulsion model.
    /// </summary>
    public bool TrySetThrottleOrder(
        object owner,
        float throttle,
        out float appliedThrottle)
    {
        appliedThrottle =
            state != null
                ? state.Throttle
                : 0f;

        if (!HasControlAuthority(owner) ||
            state == null)
        {
            return false;
        }

        appliedThrottle =
            Mathf.Clamp(
                throttle,
                -1f,
                1f);

        state.SetControlPositions(
            appliedThrottle,
            state.RudderDegrees);

        if (throttleForce != null)
            throttleForce.SetThrottle(appliedThrottle);

        return true;
    }

    /// <summary>
    /// Applies a discrete persistent rudder order. Installed steering hardware
    /// remains authoritative for whether the order is possible and for the
    /// allowed angle.
    /// </summary>
    public bool TrySetRudderOrder(
        object owner,
        float rudderDegrees,
        out float appliedRudderDegrees)
    {
        appliedRudderDegrees =
            state != null
                ? state.RudderDegrees
                : 0f;

        if (!HasControlAuthority(owner) ||
            state == null)
        {
            return false;
        }

        RefreshHandlingProfile();

        if (!_handlingProfile.HasSteering ||
            _handlingProfile.MaxTurnAngle <= 0.0001f)
        {
            state.SetControlPositions(
                state.Throttle,
                0f);

            appliedRudderDegrees = 0f;
            return false;
        }

        appliedRudderDegrees =
            Mathf.Clamp(
                rudderDegrees,
                -_handlingProfile.MaxTurnAngle,
                _handlingProfile.MaxTurnAngle);

        state.SetControlPositions(
            state.Throttle,
            appliedRudderDegrees);

        return true;
    }

    /// <summary>
    /// Starts or stops all currently installed EngineModule instances.
    /// The simulation is the command authority; individual engines still decide
    /// whether they can actually start based on fuel/power/module state.
    /// </summary>
    public bool TrySetEnginesRunning(
        object owner,
        bool running,
        out int installedEngineCount,
        out int successfulEngineCount)
    {
        installedEngineCount = 0;
        successfulEngineCount = 0;

        if (!HasControlAuthority(owner))
            return false;

        ResolveRefs();

        if (boat == null)
            return true;

        Hardpoint[] hardpoints =
            boat.GetComponentsInChildren<Hardpoint>(
                true);

        for (int i = 0;
             i < hardpoints.Length;
             i++)
        {
            Hardpoint hardpoint =
                hardpoints[i];

            if (hardpoint == null ||
                !hardpoint.HasInstalledModule ||
                hardpoint.InstalledModule == null)
            {
                continue;
            }

            EngineModule engine =
                hardpoint.InstalledModule
                    .GetComponent<EngineModule>();

            if (engine == null)
                continue;

            installedEngineCount++;

            bool accepted =
                engine.SetOn(running);

            if (running)
            {
                if (accepted &&
                    engine.IsOn)
                {
                    successfulEngineCount++;
                }
            }
            else if (!engine.IsOn)
            {
                successfulEngineCount++;
            }
        }

        if (throttleForce != null)
        {
            if (state != null)
                throttleForce.SetThrottle(state.Throttle);

            throttleForce.GetPropulsionStatus(
                out _installedPropulsionSources,
                out _activePropulsionSources);
        }
        else
        {
            _installedPropulsionSources =
                installedEngineCount;

            _activePropulsionSources = 0;
        }

        return true;
    }

    private void FixedUpdate()
    {
        ResolveRefs();

        if (state == null)
            return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f)
            return;

        RefreshHandlingProfile();
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
        AdvanceEnvironmentalDisturbance(dt);

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
        TravelPayload activeTravel =
            GameState.I != null
                ? GameState.I.activeTravel
                : null;

        // Route guidance belongs to an active voyage. A docked boat, or a boat
        // with only a locked destination, intentionally has no generated route.
        if (activeTravel == null)
        {
            _routeGuidance = null;
            return;
        }

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

        int routeSeed =
            activeTravel.seed != 0
                ? activeTravel.seed
                : routePrototypeSeed;

        _routeGuidance.Initialize(
            state.NavigationPosition,
            routeSeed,
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

        throttle =
            Mathf.Clamp(
                throttle,
                -1f,
                1f);

        float rudder =
            state.RudderDegrees +
            _currentIntent.RudderAdjust *
            rudderTravelDegreesPerSecond *
            dt;

        float maxRudderDegrees =
            _handlingProfile.MaxTurnAngle;

        // No functional steering contributor means there is physically no
        // rudder position to command. Clear any stale saved/previous angle.
        if (!_handlingProfile.HasSteering ||
            maxRudderDegrees <= 0.0001f)
        {
            rudder = 0f;
        }
        else
        {
            rudder =
                Mathf.Clamp(
                    rudder,
                    -maxRudderDegrees,
                    maxRudderDegrees);
        }

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

        float rudderAngleAuthority =
            referenceRudderAngleDegrees > 0.0001f
                ? state.RudderDegrees /
                  referenceRudderAngleDegrees
                : 0f;

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
            rudderAngleAuthority *
            rudderAngularAcceleration *
            _handlingProfile.TurnEfficiency *
            rudderAuthority01 *
            travelSign +
            _environmentYawAngularAccelerationDegrees;

        angularVelocity +=
            angularAcceleration *
            dt;

        angularVelocity *=
            Mathf.Exp(
                -angularWaterDrag *
                dt);

        float environmentalAngularCeiling =
            environmentalAngularVelocityCeilingDegrees > 0.0001f
                ? environmentalAngularVelocityCeilingDegrees
                : 72f;

        float angularVelocityLimit =
            environmentalDisturbance != null &&
            environmentalDisturbance.Severity01 > 0.0001f
                ? Mathf.Max(
                    maxAngularVelocityDegrees,
                    environmentalAngularCeiling)
                : maxAngularVelocityDegrees;

        angularVelocity =
            Mathf.Clamp(
                angularVelocity,
                -angularVelocityLimit,
                angularVelocityLimit);

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
            physicalTravelDelta +
            _environmentNavigationVelocity *
            dt;

        // Presentation velocity combines real physical forward/reverse travel
        // with simulation-owned environmental lateral drift.
        Vector2 navigationVelocity =
            headingForward *
            physicalForwardSpeed +
            _environmentNavigationVelocity;

        state.SetNavigationState(
            navigationPosition,
            navigationVelocity,
            heading,
            angularVelocity);
    }

    private void AdvanceEnvironmentalDisturbance(
        float dt)
    {
        _environmentLateralAcceleration =
            Vector2.zero;

        _environmentYawAngularAccelerationDegrees =
            0f;

        if (environmentalDisturbance == null)
        {
            environmentalDisturbance =
                new PilotingEnvironmentalDisturbance();
        }

        ResolveWaveService();

        TravelPayload activeTravel =
            GameState.I != null
                ? GameState.I.activeTravel
                : null;

        bool hasActiveVoyage =
            activeTravel != null;

        float waveAmplitude =
            _waveService != null
                ? Mathf.Max(
                    0f,
                    _waveService.Amplitude)
                : 0f;

        _physicalWaveLoad01 =
            SamplePhysicalWaveLoad01(
                hasActiveVoyage &&
                waveAmplitude > 0.0001f);

        environmentalDisturbance.Tick(
            hasActiveVoyage,
            waveAmplitude,
            state != null
                ? state.HeadingDegrees
                : 0f,
            _physicalWaveLoad01,
            out Vector2 rawLateralAcceleration,
            out float rawYawAngularAccelerationDegrees);

        float lateralResistance =
            Mathf.Clamp01(
                _handlingProfile.LateralDisturbanceResistance);

        float yawResistance =
            Mathf.Clamp01(
                _handlingProfile.YawDisturbanceResistance);

        _environmentLateralAcceleration =
            rawLateralAcceleration *
            (1f - lateralResistance);

        _environmentYawAngularAccelerationDegrees =
            rawYawAngularAccelerationDegrees *
            (1f - yawResistance);

        if (!hasActiveVoyage)
        {
            _environmentNavigationVelocity =
                Vector2.zero;

            return;
        }

        // Continuous load:
        // acceleration -> accumulated lateral velocity -> exponential damping.
        _environmentNavigationVelocity +=
            _environmentLateralAcceleration *
            dt;

        float lateralDamping =
            environmentalDisturbance.LateralVelocityDamping;

        _environmentNavigationVelocity *=
            Mathf.Exp(
                -lateralDamping *
                dt);
    }

    /// <summary>
    /// Samples one current physical wavelength around the boat and maps the
    /// surface directly beneath the boat between the local trough and crest.
    ///
    /// Result:
    ///   0 = physical trough
    ///   1 = physical crest
    ///
    /// The disturbance system therefore follows the actual WaveField phase rather
    /// than running a second timer or merely guessing from frequency.
    /// </summary>
    private float SamplePhysicalWaveLoad01(
        bool samplingEnabled)
    {
        if (!samplingEnabled ||
            _waveService == null ||
            boat == null ||
            boat.rb == null)
        {
            return 0f;
        }

        float frequency =
            Mathf.Max(
                0f,
                _waveService.Frequency);

        if (frequency <= 0.0001f)
            return 0f;

        float wavelength =
            2f /
            frequency;

        if (wavelength <= 0.0001f)
            return 0f;

        float boatWorldX =
            boat.rb.position.x;

        float currentSurfaceY =
            _waveService.SampleHeightAtWorldXWrapped(
                boatWorldX);

        float minimumSurfaceY =
            float.PositiveInfinity;

        float maximumSurfaceY =
            float.NegativeInfinity;

        const int sampleCount = 24;

        float halfWavelength =
            wavelength *
            0.5f;

        for (int i = 0;
             i < sampleCount;
             i++)
        {
            float t =
                sampleCount > 1
                    ? i /
                      (float)(sampleCount - 1)
                    : 0.5f;

            float sampleX =
                boatWorldX +
                Mathf.Lerp(
                    -halfWavelength,
                    halfWavelength,
                    t);

            float sampleSurfaceY =
                _waveService.SampleHeightAtWorldXWrapped(
                    sampleX);

            minimumSurfaceY =
                Mathf.Min(
                    minimumSurfaceY,
                    sampleSurfaceY);

            maximumSurfaceY =
                Mathf.Max(
                    maximumSurfaceY,
                    sampleSurfaceY);
        }

        float localWaveRange =
            maximumSurfaceY -
            minimumSurfaceY;

        if (localWaveRange <= 0.0001f)
            return 0f;

        return Mathf.Clamp01(
            Mathf.InverseLerp(
                minimumSurfaceY,
                maximumSurfaceY,
                currentSurfaceY));
    }

    private void ResolveWaveService()
    {
        IWaveService currentService =
            ServiceRoot.Instance != null
                ? ServiceRoot.Instance.WaveManager
                : null;

        if (ReferenceEquals(
                _waveService,
                currentService))
        {
            return;
        }

        _waveService =
            currentService;

        // A replaced service may represent a different physical sea.
        _environmentNavigationVelocity =
            Vector2.zero;

        _environmentLateralAcceleration =
            Vector2.zero;

        _environmentYawAngularAccelerationDegrees =
            0f;

        _physicalWaveLoad01 =
            0f;
    }

    private void RefreshHandlingProfile()
    {
        ResolveRefs();

        _handlingProfile =
            handlingAggregator != null
                ? handlingAggregator.RefreshProfile()
                : BoatHandlingProfile.Empty;
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
        _environmentNavigationVelocity = Vector2.zero;
        _environmentLateralAcceleration = Vector2.zero;
        _environmentYawAngularAccelerationDegrees = 0f;
        _physicalWaveLoad01 = 0f;
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

        if (handlingAggregator == null)
        {
            if (boat != null)
            {
                handlingAggregator =
                    boat.GetComponent<BoatHandlingAggregator>() ??
                    boat.GetComponentInChildren<BoatHandlingAggregator>(true);
            }

            if (handlingAggregator == null)
            {
                handlingAggregator =
                    GetComponent<BoatHandlingAggregator>() ??
                    GetComponentInChildren<BoatHandlingAggregator>(true);
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