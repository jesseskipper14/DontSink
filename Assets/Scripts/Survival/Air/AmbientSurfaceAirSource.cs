using UnityEngine;
using Survival.Vitals;

[DisallowMultipleComponent]
public sealed class AmbientSurfaceAirSource : MonoBehaviour, IAirSource
{
    public float MaxAirBonus => 0f;

    [Header("Refs")]
    public PlayerAirState air;
    public WaveManager waveManager;

    [Tooltip("Point representing where the player's mouth/head is.")]
    public Transform headPoint;

    [Header("Boat Water Context")]
    [SerializeField] private bool useBoatWaterContext = true;
    [SerializeField] private BoatWaterContextResolver explicitWaterContext;
    [SerializeField] private PlayerBoardingState boardingState;

    [Tooltip("If no boat water context is available, fall back to global ocean water.")]
    [SerializeField] private bool fallbackToOceanWhenNoContext = true;

    [Header("Detection")]
    [Tooltip("Head must be this far above water to count as breathing air.")]
    public float headClearance = 0.05f;

    [Tooltip("Optional: smooth underwater detection to avoid flicker at the surface.")]
    public float hysteresis = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private bool _isUnderwater;

    private void Awake()
    {
        if (!air)
            air = GetComponent<PlayerAirState>();

        if (!headPoint)
            headPoint = transform;

        if (boardingState == null)
        {
            boardingState =
                GetComponentInParent<PlayerBoardingState>() ??
                GetComponentInChildren<PlayerBoardingState>(true);
        }

        ResolveWaveRef();
    }

    private void ResolveWaveRef()
    {
        if (waveManager == null && ServiceRoot.Instance != null)
            waveManager = ServiceRoot.Instance.WaveManager;

        if (waveManager == null)
            waveManager = FindFirstObjectByType<WaveManager>();
    }

    public float GetAirFlowPerSecond(PlayerAirState airState, float dt)
    {
        if (headPoint == null)
            return 0f;

        bool hasWaterAtHeadContext = TryResolveHeadWaterSurface(out float waterY);

        if (!hasWaterAtHeadContext)
        {
            SetUnderwater(airState, false, "no water exposure");
            return 0f;
        }

        float headY = headPoint.position.y;

        float enter = waterY - hysteresis;
        float exit = waterY + hysteresis + headClearance;

        if (_isUnderwater)
        {
            if (headY > exit)
                _isUnderwater = false;
        }
        else
        {
            if (headY <= enter)
                _isUnderwater = true;
        }

        SetUnderwater(airState, _isUnderwater, $"headY={headY:0.00} waterY={waterY:0.00}");

        // Ambient source doesn't directly add air.
        // PlayerAirState handles surface regen when IsUnderwater is false.
        return 0f;
    }

    private bool TryResolveHeadWaterSurface(out float waterY)
    {
        waterY = 0f;

        Vector2 headPos = headPoint.position;

        if (useBoatWaterContext)
        {
            BoatWaterContextResolver resolver = ResolveWaterContext();

            if (resolver != null &&
                resolver.TryResolveAtPoint(headPos, out BoatWaterExposure exposure))
            {
                if (!exposure.HasWater)
                    return false;

                if (exposure.UsesFlatSurface)
                {
                    waterY = exposure.FlatSurfaceY;
                    return true;
                }

                if (exposure.UsesOceanSurface)
                {
                    ResolveWaveRef();

                    if (waveManager == null)
                        return false;

                    waterY = waveManager.SampleSurfaceY(headPos.x);
                    return true;
                }

                return false;
            }

            if (!fallbackToOceanWhenNoContext)
                return false;
        }

        ResolveWaveRef();

        if (waveManager == null)
            return false;

        waterY = waveManager.SampleSurfaceY(headPos.x);
        return true;
    }

    private BoatWaterContextResolver ResolveWaterContext()
    {
        if (explicitWaterContext != null)
            return explicitWaterContext;

        if (boardingState == null)
        {
            boardingState =
                GetComponentInParent<PlayerBoardingState>() ??
                GetComponentInChildren<PlayerBoardingState>(true);
        }

        if (boardingState != null &&
            boardingState.IsBoarded &&
            boardingState.CurrentBoatRoot != null)
        {
            BoatWaterContextResolver resolver =
                boardingState.CurrentBoatRoot.GetComponent<BoatWaterContextResolver>() ??
                boardingState.CurrentBoatRoot.GetComponentInChildren<BoatWaterContextResolver>(true);

            if (resolver != null)
                return resolver;
        }

        return GetComponentInParent<BoatWaterContextResolver>();
    }

    private void SetUnderwater(PlayerAirState airState, bool underwater, string reason)
    {
        if (airState != null)
            airState.IsUnderwater = underwater;

        if (verboseLogging && underwater != _isUnderwater)
            Debug.Log($"[AmbientSurfaceAirSource:{name}] underwater={underwater} reason={reason}", this);
    }
}