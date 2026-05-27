using System;
using UnityEngine;

public enum SeatEjectReason
{
    Manual,
    AccessInvalid,
    SeatUnderwater,
    OccupantMissing
}

/// <summary>
/// Generic reusable seat occupancy controller.
/// Handles:
/// - occupant tracking
/// - pinning occupant to a seat point
/// - optional auto-eject when the seat/probe point is underwater
///
/// This intentionally does NOT decide whether a player is allowed to use the seat.
/// Interactables like PilotChairInteractable still own access rules.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeatController2D : MonoBehaviour
{
    [Header("Seat")]
    [SerializeField] private Transform seatPoint;
    [SerializeField] private bool pinOccupantToSeat = true;
    [SerializeField] private bool zeroVelocityWhilePinned = true;

    [Header("Underwater Auto-Eject")]
    [SerializeField] private bool ejectWhenSeatPointUnderwater = true;

    [Tooltip("Optional probe used for underwater checks. If unset, uses Seat Point.")]
    [SerializeField] private Transform underwaterProbePoint;

    [Tooltip("How far below the water surface the probe must be before it counts as underwater.")]
    [SerializeField, Min(0f)] private float underwaterDepthTolerance = 0.05f;

    [Tooltip("Small grace time to avoid ejecting from one-frame wave/contact jitter.")]
    [SerializeField, Min(0f)] private float underwaterGraceSeconds = 0.10f;

    [Header("Water Context")]
    [Tooltip("Optional explicit resolver. Usually leave null and let this resolve from the owning boat.")]
    [SerializeField] private BoatWaterContextResolver explicitWaterContext;
    [SerializeField] private bool autoAssignExplicitWaterContext = true;

    [Tooltip("Optional explicit wave manager. Usually leave null and let this resolve from ServiceRoot.")]
    [SerializeField] private WaveManager waveManager;

    [Tooltip("If this seat is not part of a boat and no resolver exists, use ocean surface checks.")]
    [SerializeField] private bool fallbackToOceanWhenNotPartOfBoat = true;

    [Header("Debug")]
    [SerializeField] private bool logEjections = false;

    public event Action<GameObject, SeatEjectReason> OccupantEjected;

    public GameObject Occupant { get; private set; }
    public bool HasOccupant => Occupant != null;

    public Transform SeatPoint
    {
        get
        {
            if (seatPoint != null)
                return seatPoint;

            if (_runtimeFallbackSeatPoint != null)
                return _runtimeFallbackSeatPoint;

            return transform;
        }
    }

    public Transform ProbePoint
    {
        get
        {
            if (underwaterProbePoint != null)
                return underwaterProbePoint;

            return SeatPoint;
        }
    }

    private Transform _runtimeFallbackSeatPoint;
    private bool _hasRuntimePinFallback;
    private bool _runtimePinOccupantToSeat;

    private Boat _cachedBoat;
    private float _underwaterTimer;

    private bool EffectivePinOccupantToSeat =>
        _hasRuntimePinFallback ? _runtimePinOccupantToSeat : pinOccupantToSeat;

    private void Reset()
    {
        if (seatPoint == null)
            seatPoint = transform;
    }

    private void Awake()
    {
        if (seatPoint == null && _runtimeFallbackSeatPoint == null)
            seatPoint = transform;

        CacheBoat();
        ResolveWaterContext();
    }

    private void OnValidate()
    {
        underwaterDepthTolerance = Mathf.Max(0f, underwaterDepthTolerance);
        underwaterGraceSeconds = Mathf.Max(0f, underwaterGraceSeconds);
    }

    /// <summary>
    /// Lets legacy components, like PilotChairInteractable, keep their existing serialized seat point
    /// while still delegating actual seat behavior here.
    /// </summary>
    public void SetRuntimeFallbackSeat(Transform fallbackSeatPoint, bool fallbackPinOccupantToSeat)
    {
        _runtimeFallbackSeatPoint = fallbackSeatPoint;
        _runtimePinOccupantToSeat = fallbackPinOccupantToSeat;
        _hasRuntimePinFallback = true;
    }

    public bool TrySeat(GameObject newOccupant)
    {
        if (newOccupant == null)
            return false;

        if (Occupant != null)
            return false;

        Occupant = newOccupant;
        _underwaterTimer = 0f;

        PinOccupantIfNeeded();
        return true;
    }

    /// <summary>
    /// Ticks seat behavior. Returns true if an occupant remains seated after the tick.
    /// </summary>
    public bool TickSeat()
    {
        if (Occupant == null)
            return false;

        if (ejectWhenSeatPointUnderwater && IsSeatProbeUnderwater())
        {
            _underwaterTimer += Time.deltaTime;

            if (_underwaterTimer >= underwaterGraceSeconds)
            {
                Eject(SeatEjectReason.SeatUnderwater);
                return false;
            }
        }
        else
        {
            _underwaterTimer = 0f;
        }

        PinOccupantIfNeeded();
        return Occupant != null;
    }

    public void Eject(SeatEjectReason reason)
    {
        if (Occupant == null)
            return;

        GameObject oldOccupant = Occupant;
        Occupant = null;
        _underwaterTimer = 0f;

        if (logEjections)
            Debug.Log($"[SeatController2D:{name}] Ejected '{oldOccupant.name}' reason={reason}.", this);

        OccupantEjected?.Invoke(oldOccupant, reason);
    }

    private void PinOccupantIfNeeded()
    {
        if (!EffectivePinOccupantToSeat)
            return;

        if (Occupant == null)
            return;

        Transform targetSeatPoint = SeatPoint;
        if (targetSeatPoint == null)
            return;

        Occupant.transform.position = targetSeatPoint.position;

        if (!zeroVelocityWhilePinned)
            return;

        Rigidbody2D rb = Occupant.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private bool IsSeatProbeUnderwater()
    {
        Transform probe = ProbePoint;
        if (probe == null)
            return false;

        Vector2 probeWorld = probe.position;

        BoatWaterContextResolver resolver = ResolveWaterContext();

        if (resolver != null && resolver.TryResolveAtPoint(probeWorld, out BoatWaterExposure exposure))
            return IsExposureUnderwaterAtPoint(exposure, probeWorld);

        CacheBoat();

        // Important: if this is part of a boat but has no resolver, do NOT blindly use ocean.
        // That would recreate the old "inside intact boat still counts as ocean" nonsense.
        if (_cachedBoat != null)
            return false;

        if (!fallbackToOceanWhenNotPartOfBoat)
            return false;

        return IsOceanUnderwaterAtPoint(probeWorld);
    }

    private bool IsExposureUnderwaterAtPoint(BoatWaterExposure exposure, Vector2 probeWorld)
    {
        switch (exposure.Kind)
        {
            case BoatWaterExposureKind.None:
            case BoatWaterExposureKind.DryInterior:
                return false;

            case BoatWaterExposureKind.CompartmentFull:
                return true;

            case BoatWaterExposureKind.CompartmentPartial:
                return probeWorld.y < exposure.FlatSurfaceY - underwaterDepthTolerance;

            case BoatWaterExposureKind.Ocean:
            case BoatWaterExposureKind.FullyFloodedBoatOcean:
                return IsOceanUnderwaterAtPoint(probeWorld);

            default:
                return false;
        }
    }

    private bool IsOceanUnderwaterAtPoint(Vector2 probeWorld)
    {
        ResolveWaveRef();

        if (waveManager == null)
            return false;

        float oceanSurfaceY = waveManager.SampleSurfaceY(probeWorld.x);
        return probeWorld.y < oceanSurfaceY - underwaterDepthTolerance;
    }

    private BoatWaterContextResolver ResolveWaterContext()
    {
        if (explicitWaterContext != null)
            return explicitWaterContext;

        CacheBoat();

        BoatWaterContextResolver resolved = null;

        if (_cachedBoat != null)
        {
            resolved =
                _cachedBoat.GetComponent<BoatWaterContextResolver>() ??
                _cachedBoat.GetComponentInChildren<BoatWaterContextResolver>(true);
        }

        if (resolved == null)
            resolved = GetComponentInParent<BoatWaterContextResolver>();

        if (resolved == null)
            resolved = GetComponentInChildren<BoatWaterContextResolver>(true);

        if (resolved != null && autoAssignExplicitWaterContext)
            explicitWaterContext = resolved;

        return resolved;
    }

    private void ResolveWaveRef()
    {
        if (waveManager == null && ServiceRoot.Instance != null)
            waveManager = ServiceRoot.Instance.WaveManager;

        if (waveManager == null)
            waveManager = FindFirstObjectByType<WaveManager>();
    }

    private void CacheBoat()
    {
        if (_cachedBoat == null)
            _cachedBoat = GetComponentInParent<Boat>();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform seat = SeatPoint;
        Transform probe = ProbePoint;

        if (seat != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(seat.position, 0.06f);
        }

        if (probe != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(probe.position, 0.09f);
        }
    }
#endif
}