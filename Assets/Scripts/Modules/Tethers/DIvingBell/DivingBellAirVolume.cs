using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative trapped-air + internal-water state for an open-bottom diving bell.
///
/// V1 model:
/// - when the bottom opening is dry, the bell freely exchanges with atmosphere;
/// - when submerged and upright, trapped gas compresses approximately by Boyle's law;
/// - internal water rises as compressed air volume falls;
/// - excessive tilt vents trapped gas, permanently reducing the available air pocket
///   until the opening reaches atmosphere again;
/// - occupants with their head above the internal waterline breathe bell air;
/// - occupants below the waterline use the ordinary underwater/source model;
/// - bell air quality slowly degrades per breathing occupant while sealed underwater.
///
/// Persistence is intentionally NOT owned here in Pass 12A. The later persistence
/// hardening pass should snapshot TrappedAirMoles01 + AirQuality01.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(DivingBellOccupancy))]
public sealed class DivingBellAirVolume :
    MonoBehaviour,
    IVolumeContribution
{
    [Header("Geometry")]
    [Tooltip(
        "Center of the open bottom plane. If blank, the bell's Bottom Interior Point is used when available.")]
    [SerializeField] private Transform bottomOpeningPoint;

    [Tooltip(
        "Highest usable point of the trapped-air chamber. Used only as a fallback when no supported " +
        "Chamber Collider is available.")]
    [SerializeField] private Transform airCeilingPoint;

    [Tooltip(
        "Authoritative usable air/water chamber geometry. Prefer the same InteriorSafetyVolume collider " +
        "used by DivingBellInternalWaterRenderer. WaterFill01 is resolved against this actual shape, " +
        "so 72% fill means 72% of the chamber rather than 72% between two arbitrary transforms.")]
    [SerializeField] private Collider2D chamberCollider;

    [Tooltip(
        "Optional simple water-surface visual. Its world Y follows the calculated waterline. " +
        "Use a thin horizontal sprite/line; this pass does not attempt polygon clipping.")]
    [SerializeField] private Transform waterSurfaceVisual;

    [SerializeField] private bool keepWaterSurfaceVisualWorldHorizontal = true;

    [Header("Ocean / Pressure")]
    [SerializeField] private WaveManager waveManager;

    [Tooltip("Atmospheric pressure used by the simplified Boyle-law compression model.")]
    [SerializeField, Min(1f)] private float atmosphericPressureKPa = 101.325f;

    [Tooltip("Approximate hydrostatic pressure increase per meter of water depth.")]
    [SerializeField, Min(0.01f)] private float waterPressureKPaPerMeter = 9.80665f;

    [Tooltip(
        "Gameplay multiplier applied only to hydrostatic compression. " +
        "1.0 is approximately physical pressure scaling; 0.65 makes the bell fill about 35% more slowly with depth. " +
        "This does not change the actual ocean depth, opening-submersion test, or venting behavior.")]
    [SerializeField, Range(0.1f, 1.5f)] private float pressureCompressionScale = 0.65f;

    [Tooltip("Small clearance before the open bottom counts as submerged.")]
    [SerializeField, Min(0f)] private float openingSubmergeEpsilonMeters = 0.02f;

    [SerializeField, Range(1, 8)] private int pressureSolveIterations = 4;

    [Header("Air Escape / Tipping")]
    [Tooltip("Air begins escaping once bell tilt from upright exceeds this angle.")]
    [SerializeField, Range(0f, 179f)] private float ventStartTiltDegrees = 55f;

    [Tooltip("At/above this tilt, the configured full vent rate is applied.")]
    [SerializeField, Range(1f, 180f)] private float fullVentTiltDegrees = 105f;

    [Tooltip("Normalized trapped-air amount lost per second at full vent rate.")]
    [SerializeField, Min(0f)] private float fullVentAirMolesPerSecond = 0.65f;

    [Header("Air Quality")]
    [Tooltip(
        "Simplified stale-air loss per second for each occupant currently breathing the trapped pocket. " +
        "This represents oxygen depletion + CO2 buildup as one gameplay quality scalar.")]
    [SerializeField, Min(0f)] private float qualityLossPerBreathingOccupantPerSecond = 0.0015f;

    [Tooltip("How quickly trapped-air quality recovers while the opening is exposed to atmosphere.")]
    [SerializeField, Min(0f)] private float atmosphericQualityRecoveryPerSecond = 1.5f;

    [Tooltip("Head must be this far above the calculated waterline to breathe the bell pocket.")]
    [SerializeField, Min(0f)] private float breathingHeadClearanceMeters = 0.03f;

    [Header("Persistence Restore")]
    [Tooltip(
        "Short grace period after persisted trapped-air state is restored. During this window a transient " +
        "above-water reading caused by scene/physics initialization will NOT refill the bell. Observing the " +
        "opening genuinely submerged ends the grace immediately.")]
    [SerializeField, Min(0f)] private float restoredAtmosphereEqualizationGraceSeconds = 0.25f;

    [Header("Optional Buoyancy Coupling")]
    [Tooltip(
        "Optional ADDITIONAL displacement volume represented by a full surface-pressure air pocket. " +
        "Leave 0 if the bell's current ForceBody2D dimensions already include all desired displacement. " +
        "If you later author ForceBody2D as shell-only displacement, set this to the air-pocket volume; " +
        "the contribution will automatically shrink with compression/venting.")]
    [SerializeField, Min(0f)] private float maxAirDisplacementVolume = 0f;

    [Header("Runtime")]
    [SerializeField, Range(0f, 1f)] private float trappedAirMoles01 = 1f;
    [SerializeField, Range(0f, 1f)] private float compressedAirVolume01 = 1f;
    [SerializeField, Range(0f, 1f)] private float waterFill01 = 0f;
    [SerializeField, Range(0f, 1f)] private float airQuality01 = 1f;
    [SerializeField] private float oceanSurfaceWorldY;
    [SerializeField] private float waterSurfaceWorldY;
    [SerializeField] private float currentTiltDegrees;
    [SerializeField] private bool openingSubmerged;
    [SerializeField] private bool venting;
    [SerializeField] private int breathingOccupantCount;
    [SerializeField] private float restoredAtmosphereEqualizationGraceRemaining;

    private DivingBellOccupancy _occupancy;
    private ForceBody2D _forceBody;

    private readonly List<Vector2> _chamberWorldPolygon =
        new List<Vector2>(8);

    private readonly List<Vector2> _chamberClipScratch =
        new List<Vector2>(8);

    public float TrappedAirMoles01 => trappedAirMoles01;
    public float CompressedAirVolume01 => compressedAirVolume01;
    public float WaterFill01 => waterFill01;
    public float AirQuality01 => airQuality01;
    public float OceanSurfaceWorldY => oceanSurfaceWorldY;
    public float WaterSurfaceWorldY => waterSurfaceWorldY;
    public float CurrentTiltDegrees => currentTiltDegrees;
    public bool OpeningSubmerged => openingSubmerged;
    public bool IsVenting => venting;
    public int BreathingOccupantCount => breathingOccupantCount;

    public float VolumeContribution =>
        Mathf.Max(0f, maxAirDisplacementVolume) *
        Mathf.Clamp01(compressedAirVolume01);

    private void Awake()
    {
        ResolveRefs();
        ClampRuntimeState();

        if (chamberCollider == null &&
            airCeilingPoint == null)
        {
            Debug.LogWarning(
                $"[DivingBellAirVolume:{name}] Neither Chamber Collider nor Air Ceiling Point is assigned. " +
                "Trapped-air/flooding simulation will remain inactive until chamber geometry is configured.",
                this);
        }
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (_forceBody != null)
            _forceBody.RegisterVolumeContribution(this);
    }

    private void OnDisable()
    {
        if (_forceBody != null)
            _forceBody.UnregisterVolumeContribution(this);
    }

    private void OnValidate()
    {
        atmosphericPressureKPa = Mathf.Max(1f, atmosphericPressureKPa);
        waterPressureKPaPerMeter = Mathf.Max(0.01f, waterPressureKPaPerMeter);
        openingSubmergeEpsilonMeters = Mathf.Max(0f, openingSubmergeEpsilonMeters);
        pressureSolveIterations = Mathf.Clamp(pressureSolveIterations, 1, 8);
        ventStartTiltDegrees = Mathf.Clamp(ventStartTiltDegrees, 0f, 179f);
        fullVentTiltDegrees = Mathf.Clamp(fullVentTiltDegrees, ventStartTiltDegrees + 0.1f, 180f);
        fullVentAirMolesPerSecond = Mathf.Max(0f, fullVentAirMolesPerSecond);
        qualityLossPerBreathingOccupantPerSecond = Mathf.Max(0f, qualityLossPerBreathingOccupantPerSecond);
        atmosphericQualityRecoveryPerSecond = Mathf.Max(0f, atmosphericQualityRecoveryPerSecond);
        breathingHeadClearanceMeters = Mathf.Max(0f, breathingHeadClearanceMeters);
        restoredAtmosphereEqualizationGraceSeconds = Mathf.Max(0f, restoredAtmosphereEqualizationGraceSeconds);
        maxAirDisplacementVolume = Mathf.Max(0f, maxAirDisplacementVolume);
        ClampRuntimeState();
    }

    private void FixedUpdate()
    {
        ResolveRefs();

        Transform opening = BottomOpeningPoint;
        if (opening == null ||
            !HasUsableChamberGeometry() ||
            !TrySampleOceanSurface(opening.position.x, out float sampledOceanY))
        {
            breathingOccupantCount = 0;
            venting = false;
            UpdateWaterSurfaceVisual();
            return;
        }

        float dt = Mathf.Max(0f, Time.fixedDeltaTime);

        oceanSurfaceWorldY = sampledOceanY;
        openingSubmerged =
            opening.position.y <=
            oceanSurfaceWorldY - openingSubmergeEpsilonMeters;

        currentTiltDegrees =
            Vector2.Angle(
                transform.up,
                Vector2.up);

        if (openingSubmerged)
        {
            // A confirmed underwater sample means scene/environment hydration is complete
            // for this bell. From here onward ordinary atmosphere equalization rules apply.
            restoredAtmosphereEqualizationGraceRemaining = 0f;
        }
        else if (restoredAtmosphereEqualizationGraceRemaining > 0f)
        {
            // Persistence may restore the bell before its Transform hierarchy / wave context
            // has fully settled. Never destroy authoritative saved air state because one of
            // those bootstrap frames temporarily reports the opening in breathable air.
            restoredAtmosphereEqualizationGraceRemaining =
                Mathf.Max(
                    0f,
                    restoredAtmosphereEqualizationGraceRemaining - dt);

            venting = false;
            breathingOccupantCount = 0;
            UpdateWaterSurfaceVisual();
            return;
        }

        if (!openingSubmerged)
        {
            // Open-bottom bell exposed to atmosphere: pressure equalizes and lost air
            // is naturally replaced. Quality also refreshes rapidly instead of being
            // a hidden consumable while the bell is sitting on deck.
            trappedAirMoles01 = 1f;
            compressedAirVolume01 = 1f;
            waterFill01 = 0f;
            waterSurfaceWorldY = opening.position.y;
            venting = false;
            breathingOccupantCount = 0;

            airQuality01 = Mathf.MoveTowards(
                airQuality01,
                1f,
                atmosphericQualityRecoveryPerSecond * dt);

            UpdateWaterSurfaceVisual();
            return;
        }

        TickTiltVenting(dt);
        SolveCompressedAirVolume();
        TickAirQuality(dt);
        UpdateWaterSurfaceVisual();
    }

    public float GetOccupantSubmersion01(
        GameObject playerObject,
        Vector2 bodyBottomWorld,
        Vector2 bodyTopWorld)
    {
        if (_occupancy == null ||
            playerObject == null ||
            !_occupancy.Contains(playerObject))
        {
            return 0f;
        }

        if (waterFill01 <= 0.0001f)
            return 0f;

        if (waterFill01 >= 0.9999f ||
            compressedAirVolume01 <= 0.0001f)
        {
            return 1f;
        }

        float bottomY = Mathf.Min(bodyBottomWorld.y, bodyTopWorld.y);
        float topY = Mathf.Max(bodyBottomWorld.y, bodyTopWorld.y);
        float height = Mathf.Max(0.01f, topY - bottomY);

        return Mathf.Clamp01(
            (waterSurfaceWorldY - bottomY) /
            height);
    }

    public bool TryResolveBreathingEnvironment(
        GameObject playerObject,
        Vector2 headWorld,
        out bool headUnderwater,
        out float ambientAirQuality01)
    {
        headUnderwater = false;
        ambientAirQuality01 = 1f;

        ResolveRefs();

        if (_occupancy == null ||
            playerObject == null ||
            !_occupancy.Contains(playerObject))
        {
            return false;
        }

        bool noUsableAirPocket =
            compressedAirVolume01 <= 0.0001f ||
            waterFill01 >= 0.9999f;

        headUnderwater =
            noUsableAirPocket ||
            (waterFill01 > 0.0001f &&
             headWorld.y <=
             waterSurfaceWorldY + breathingHeadClearanceMeters);

        ambientAirQuality01 =
            Mathf.Clamp01(airQuality01);

        return true;
    }

    public void RestoreRuntimeState(
        float restoredTrappedAirMoles01,
        float restoredAirQuality01)
    {
        trappedAirMoles01 = Mathf.Clamp01(restoredTrappedAirMoles01);
        airQuality01 = Mathf.Clamp01(restoredAirQuality01);

        restoredAtmosphereEqualizationGraceRemaining =
            Mathf.Max(
                0f,
                restoredAtmosphereEqualizationGraceSeconds);

        // Compression/waterline depend on current world depth and will be resolved
        // on the next physics step. Keep these bounded in the meantime.
        ClampRuntimeState();
    }

    [ContextMenu("Refill Bell Air From Atmosphere")]
    private void DebugRefillBellAir()
    {
        trappedAirMoles01 = 1f;
        compressedAirVolume01 = 1f;
        waterFill01 = 0f;
        airQuality01 = 1f;

        Transform opening = BottomOpeningPoint;
        if (opening != null)
            waterSurfaceWorldY = opening.position.y;

        UpdateWaterSurfaceVisual();
    }

    [ContextMenu("Vent All Bell Air")]
    private void DebugVentAllBellAir()
    {
        trappedAirMoles01 = 0f;
        compressedAirVolume01 = 0f;
        waterFill01 = 1f;
        SolveCompressedAirVolume();
        UpdateWaterSurfaceVisual();
    }

    private Transform BottomOpeningPoint
    {
        get
        {
            if (bottomOpeningPoint != null)
                return bottomOpeningPoint;

            if (_occupancy != null &&
                _occupancy.BottomInteriorPoint != null)
            {
                return _occupancy.BottomInteriorPoint;
            }

            return null;
        }
    }

    private void TickTiltVenting(float dt)
    {
        if (currentTiltDegrees <= ventStartTiltDegrees ||
            trappedAirMoles01 <= 0f ||
            fullVentAirMolesPerSecond <= 0f)
        {
            venting = false;
            return;
        }

        float t = Mathf.InverseLerp(
            ventStartTiltDegrees,
            fullVentTiltDegrees,
            currentTiltDegrees);

        // Start with a small but real leak as soon as the seal angle is exceeded,
        // then ramp to full loss as the bell approaches/inverts past fullVentTilt.
        float ventFactor = Mathf.Lerp(0.15f, 1f, t);

        trappedAirMoles01 = Mathf.Max(
            0f,
            trappedAirMoles01 -
            fullVentAirMolesPerSecond *
            ventFactor *
            dt);

        venting = true;
    }

    private void SolveCompressedAirVolume()
    {
        Transform opening =
            BottomOpeningPoint;

        if (opening == null ||
            !HasUsableChamberGeometry())
        {
            return;
        }

        if (trappedAirMoles01 <= 0.000001f)
        {
            compressedAirVolume01 = 0f;
            waterFill01 = 1f;

            if (!TryResolveWaterSurfaceWorldY(
                    waterFill01,
                    out waterSurfaceWorldY))
            {
                waterSurfaceWorldY =
                    airCeilingPoint != null
                        ? airCeilingPoint.position.y
                        : opening.position.y;
            }

            return;
        }

        float solvedWaterFill =
            Mathf.Clamp01(
                waterFill01);

        for (int i = 0;
             i < pressureSolveIterations;
             i++)
        {
            if (!TryResolveWaterSurfaceWorldY(
                    solvedWaterFill,
                    out float interfaceWorldY))
            {
                return;
            }

            float interfaceDepthMeters =
                Mathf.Max(
                    0f,
                    oceanSurfaceWorldY -
                    interfaceWorldY);

            float pressureKPa =
                atmosphericPressureKPa +
                interfaceDepthMeters *
                waterPressureKPaPerMeter *
                pressureCompressionScale;

            float solvedAirVolume =
                Mathf.Clamp01(
                    trappedAirMoles01 *
                    atmosphericPressureKPa /
                    Mathf.Max(
                        1f,
                        pressureKPa));

            solvedWaterFill =
                1f -
                solvedAirVolume;
        }

        compressedAirVolume01 =
            Mathf.Clamp01(
                1f -
                solvedWaterFill);

        waterFill01 =
            Mathf.Clamp01(
                solvedWaterFill);

        if (!TryResolveWaterSurfaceWorldY(
                waterFill01,
                out waterSurfaceWorldY))
        {
            waterSurfaceWorldY =
                opening.position.y;
        }
    }

    private bool TryResolveWaterSurfaceWorldY(
        float targetWaterFill01,
        out float surfaceWorldY)
    {
        if (DivingBellChamberGeometry
            .TryResolveHorizontalSurfaceY(
                chamberCollider,
                targetWaterFill01,
                _chamberWorldPolygon,
                _chamberClipScratch,
                out surfaceWorldY))
        {
            return true;
        }

        Transform opening =
            BottomOpeningPoint;

        if (opening != null &&
            airCeilingPoint != null)
        {
            surfaceWorldY =
                Vector3.Lerp(
                    opening.position,
                    airCeilingPoint.position,
                    Mathf.Clamp01(
                        targetWaterFill01)).y;

            return true;
        }

        surfaceWorldY =
            opening != null
                ? opening.position.y
                : transform.position.y;

        return false;
    }

    private bool HasUsableChamberGeometry()
    {
        if (DivingBellChamberGeometry
            .IsSupported(
                chamberCollider))
        {
            return true;
        }

        return
            BottomOpeningPoint != null &&
            airCeilingPoint != null;
    }

    private void TickAirQuality(float dt)
    {
        breathingOccupantCount =
            CountBreathingOccupants();

        if (breathingOccupantCount <= 0 ||
            qualityLossPerBreathingOccupantPerSecond <= 0f)
        {
            return;
        }

        airQuality01 = Mathf.Max(
            0f,
            airQuality01 -
            qualityLossPerBreathingOccupantPerSecond *
            breathingOccupantCount *
            dt);
    }

    private int CountBreathingOccupants()
    {
        if (_occupancy == null ||
            !_occupancy.HasOccupants)
        {
            return 0;
        }

        int count = 0;
        var occupants = _occupancy.Occupants;

        for (int i = 0; i < occupants.Count; i++)
        {
            PlayerBellOccupantState occupant =
                occupants[i];

            if (occupant == null)
                continue;

            PlayerSubmersionState submersion =
                occupant.GetComponent<PlayerSubmersionState>() ??
                occupant.GetComponentInChildren<PlayerSubmersionState>(true);

            Vector2 headWorld =
                submersion != null
                    ? submersion.HeadWorldPosition
                    : (Vector2)occupant.transform.position;

            if (TryResolveBreathingEnvironment(
                    occupant.gameObject,
                    headWorld,
                    out bool headUnderwater,
                    out _) &&
                !headUnderwater)
            {
                count++;
            }
        }

        return count;
    }

    private void ResolveRefs()
    {
        if (_occupancy == null)
        {
            _occupancy =
                GetComponent<DivingBellOccupancy>() ??
                GetComponentInParent<DivingBellOccupancy>();
        }

        if (bottomOpeningPoint == null &&
            _occupancy != null &&
            _occupancy.BottomInteriorPoint != null)
        {
            bottomOpeningPoint =
                _occupancy.BottomInteriorPoint;
        }

        if (chamberCollider == null)
        {
            Collider2D[] candidates =
                GetComponentsInChildren<Collider2D>(true);

            for (int i = 0;
                 i < candidates.Length;
                 i++)
            {
                Collider2D candidate =
                    candidates[i];

                if (candidate == null ||
                    !candidate.isTrigger)
                {
                    continue;
                }

                if (candidate.name.Contains("InteriorSafety"))
                {
                    chamberCollider =
                        candidate;

                    break;
                }
            }
        }

        if (_forceBody == null)
        {
            _forceBody =
                GetComponent<ForceBody2D>() ??
                GetComponentInParent<ForceBody2D>();
        }

        ResolveWaveRef();
    }

    private void ResolveWaveRef()
    {
        if (waveManager == null &&
            ServiceRoot.Instance != null)
        {
            waveManager =
                ServiceRoot.Instance.WaveManager;
        }

        if (waveManager == null)
        {
            waveManager =
                FindFirstObjectByType<WaveManager>();
        }
    }

    private bool TrySampleOceanSurface(
        float worldX,
        out float surfaceY)
    {
        ResolveWaveRef();

        if (waveManager == null)
        {
            surfaceY = 0f;
            return false;
        }

        surfaceY =
            waveManager.SampleSurfaceY(worldX);

        return true;
    }

    private void ClampRuntimeState()
    {
        trappedAirMoles01 = Mathf.Clamp01(trappedAirMoles01);
        compressedAirVolume01 = Mathf.Clamp01(compressedAirVolume01);
        waterFill01 = Mathf.Clamp01(waterFill01);
        airQuality01 = Mathf.Clamp01(airQuality01);
    }

    private void UpdateWaterSurfaceVisual()
    {
        if (waterSurfaceVisual == null)
            return;

        bool visible =
            openingSubmerged &&
            waterFill01 > 0.0001f;

        if (waterSurfaceVisual.gameObject.activeSelf != visible)
            waterSurfaceVisual.gameObject.SetActive(visible);

        if (!visible)
            return;

        Transform opening = BottomOpeningPoint;
        if (opening == null)
            return;

        Vector3 center;

        if (chamberCollider != null)
        {
            center =
                chamberCollider.bounds.center;
        }
        else if (airCeilingPoint != null)
        {
            center =
                Vector3.Lerp(
                    opening.position,
                    airCeilingPoint.position,
                    waterFill01);
        }
        else
        {
            center =
                opening.position;
        }

        center.y =
            waterSurfaceWorldY;

        waterSurfaceVisual.position =
            center;

        if (keepWaterSurfaceVisualWorldHorizontal)
            waterSurfaceVisual.rotation = Quaternion.identity;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform opening = bottomOpeningPoint;
        if (opening == null)
        {
            DivingBellOccupancy occupancy =
                GetComponent<DivingBellOccupancy>();

            if (occupancy != null)
                opening = occupancy.BottomInteriorPoint;
        }

        if (opening == null || airCeilingPoint == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(opening.position, airCeilingPoint.position);
        Gizmos.DrawWireSphere(opening.position, 0.06f);
        Gizmos.DrawWireSphere(airCeilingPoint.position, 0.06f);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Vector3 p = Vector3.Lerp(opening.position, airCeilingPoint.position, waterFill01);
            p.y = waterSurfaceWorldY;
            Gizmos.DrawLine(p + Vector3.left * 0.5f, p + Vector3.right * 0.5f);
        }
    }
#endif
}
