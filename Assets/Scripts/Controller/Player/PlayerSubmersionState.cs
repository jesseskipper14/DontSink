using UnityEngine;

/// <summary>
/// Player-specific submersion state.
/// Prefer contextual boat/ocean water queries over blind provider aggregation,
/// because "some provider says wet" is not enough once interiors/flooded compartments exist.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSubmersionState : MonoBehaviour
{
    [Header("Thresholds")]
    [Range(0f, 1f)]
    [Tooltip("At/above this, we consider the player in swim mode.")]
    public float swimThreshold = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("Below this, treat as effectively dry for gameplay.")]
    public float dryEpsilon = 0.02f;

    [Header("Contextual Water")]
    [SerializeField] private bool useContextualWater = true;
    [SerializeField] private bool fallbackToProvidersWhenNoContext = true;

    [SerializeField] private PlayerBoardingState boardingState;
    [SerializeField] private BoatWaterContextResolver explicitWaterContext;
    [SerializeField] private WaveManager waveManager;

    [Header("Body Probe")]
    [Tooltip("Optional bottom/body-low point. If unset, localBottomOffset is used.")]
    [SerializeField] private Transform bottomPoint;

    [Tooltip("Optional top/body-high point. If unset, localTopOffset is used.")]
    [SerializeField] private Transform topPoint;

    [SerializeField] private Vector2 localBottomOffset = new Vector2(0f, -0.45f);
    [SerializeField] private Vector2 localTopOffset = new Vector2(0f, 0.55f);

    [Header("Debug")]
    [SerializeField] private bool logWhenStateChanges = false;
    [SerializeField] private bool verboseWaterContext = false;

    public float Submersion01 { get; private set; }
    public bool InWater => Submersion01 > dryEpsilon;
    public bool SubmergedEnoughToSwim => Submersion01 >= swimThreshold;

    public float Wading01 => Mathf.InverseLerp(dryEpsilon, swimThreshold, Submersion01);

    public BoatWaterExposureKind LastExposureKind { get; private set; } = BoatWaterExposureKind.None;
    public Compartment LastCompartment { get; private set; }

    private ISubmersionProvider[] _providers;

    private bool _lastInWater;
    private bool _lastSwimming;

    private void Awake()
    {
        ResolveRefs();
        RebuildProviders();
    }

    private void OnValidate()
    {
        swimThreshold = Mathf.Clamp01(swimThreshold);
        dryEpsilon = Mathf.Clamp01(dryEpsilon);
    }

    public void RebuildProviders()
    {
        var monos = GetComponentsInChildren<MonoBehaviour>(true);

        int count = 0;
        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] is ISubmersionProvider)
                count++;
        }

        _providers = new ISubmersionProvider[count];

        int w = 0;
        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] is ISubmersionProvider p)
                _providers[w++] = p;
        }
    }

    private void FixedUpdate()
    {
        float resolved = 0f;
        bool usedContext = false;

        if (useContextualWater)
            usedContext = TryResolveContextualSubmersion(out resolved);

        if (!usedContext)
        {
            if (fallbackToProvidersWhenNoContext)
                resolved = ResolveProviderSubmersion();
            else
                resolved = 0f;

            LastExposureKind = BoatWaterExposureKind.None;
            LastCompartment = null;
        }

        Submersion01 = Mathf.Clamp01(resolved);

        if (logWhenStateChanges)
            LogStateChanges(usedContext);
    }

    private bool TryResolveContextualSubmersion(out float submersion01)
    {
        submersion01 = 0f;

        Vector2 bottom = GetBottomWorld();
        Vector2 top = GetTopWorld();
        Vector2 probe = (bottom + top) * 0.5f;

        if (!TryResolveExposure(probe, out BoatWaterExposure exposure))
            return false;

        LastExposureKind = exposure.Kind;
        LastCompartment = exposure.Compartment;

        switch (exposure.Kind)
        {
            case BoatWaterExposureKind.None:
            case BoatWaterExposureKind.DryInterior:
                submersion01 = 0f;
                LogContext($"dry/none exposure={exposure.Kind}");
                return true;

            case BoatWaterExposureKind.CompartmentFull:
                submersion01 = 1f;
                LogContext($"full compartment={DescribeCompartment(exposure.Compartment)}");
                return true;

            case BoatWaterExposureKind.CompartmentPartial:
                submersion01 = ComputeSubmersionAgainstSurface(bottom, top, exposure.FlatSurfaceY);
                LogContext($"partial compartment={DescribeCompartment(exposure.Compartment)} surfaceY={exposure.FlatSurfaceY:0.00} sub={submersion01:0.00}");
                return true;

            case BoatWaterExposureKind.Ocean:
            case BoatWaterExposureKind.FullyFloodedBoatOcean:
                if (!TrySampleOceanSurface(probe.x, out float oceanY))
                {
                    submersion01 = 0f;
                    return true;
                }

                submersion01 = ComputeSubmersionAgainstSurface(bottom, top, oceanY);
                LogContext($"ocean exposure={exposure.Kind} surfaceY={oceanY:0.00} sub={submersion01:0.00}");
                return true;

            default:
                submersion01 = 0f;
                return true;
        }
    }

    private bool TryResolveExposure(Vector2 probeWorld, out BoatWaterExposure exposure)
    {
        exposure = default;

        BoatWaterContextResolver resolver = ResolveWaterContext();

        if (resolver != null && resolver.TryResolveAtPoint(probeWorld, out exposure))
            return true;

        // If not boarded, ocean is the normal ambient context.
        if (boardingState == null || !boardingState.IsBoarded)
        {
            exposure = BoatWaterExposure.Ocean();
            return true;
        }

        // Boarded but missing resolver: don't pretend ocean applies inside the boat.
        // Let provider fallback handle legacy cases if enabled.
        return false;
    }

    private BoatWaterContextResolver ResolveWaterContext()
    {
        if (explicitWaterContext != null)
            return explicitWaterContext;

        ResolveRefs();

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

    private float ResolveProviderSubmersion()
    {
        if (_providers == null || _providers.Length == 0)
            RebuildProviders();

        float max = 0f;

        for (int i = 0; i < _providers.Length; i++)
        {
            ISubmersionProvider p = _providers[i];
            if (p == null)
                continue;

            float s = Mathf.Clamp01(p.SubmergedFraction);
            if (s > max)
                max = s;
        }

        return max;
    }

    private static float ComputeSubmersionAgainstSurface(Vector2 bottom, Vector2 top, float surfaceY)
    {
        float bottomY = Mathf.Min(bottom.y, top.y);
        float topY = Mathf.Max(bottom.y, top.y);
        float height = Mathf.Max(0.01f, topY - bottomY);

        return Mathf.Clamp01((surfaceY - bottomY) / height);
    }

    private Vector2 GetBottomWorld()
    {
        if (bottomPoint != null)
            return bottomPoint.position;

        return transform.TransformPoint(localBottomOffset);
    }

    private Vector2 GetTopWorld()
    {
        if (topPoint != null)
            return topPoint.position;

        return transform.TransformPoint(localTopOffset);
    }

    private bool TrySampleOceanSurface(float worldX, out float surfaceY)
    {
        ResolveWaveRef();

        if (waveManager == null)
        {
            surfaceY = 0f;
            return false;
        }

        surfaceY = waveManager.SampleSurfaceY(worldX);
        return true;
    }

    private void ResolveRefs()
    {
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

    private void LogStateChanges(bool usedContext)
    {
        bool inWater = InWater;
        bool swimming = SubmergedEnoughToSwim;

        if (inWater == _lastInWater && swimming == _lastSwimming)
            return;

        Debug.Log(
            $"[PlayerSubmersionState:{name}] InWater={inWater} Swimming={swimming} " +
            $"Submersion={Submersion01:0.00} Context={usedContext} Exposure={LastExposureKind} " +
            $"Compartment={DescribeCompartment(LastCompartment)}",
            this);

        _lastInWater = inWater;
        _lastSwimming = swimming;
    }

    private void LogContext(string msg)
    {
        if (!verboseWaterContext)
            return;

        Debug.Log($"[PlayerSubmersionState:{name}] {msg}", this);
    }

    private static string DescribeCompartment(Compartment c)
    {
        if (c == null)
            return "none";

        if (!string.IsNullOrWhiteSpace(c.CompartmentId))
            return c.CompartmentId;

        return c.name;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(GetBottomWorld(), 0.05f);
        Gizmos.DrawSphere(GetTopWorld(), 0.05f);
        Gizmos.DrawLine(GetBottomWorld(), GetTopWorld());
    }
#endif
}