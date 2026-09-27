using UnityEngine;

/// <summary>
/// Adds/removes retained water from a physical WorldItem based on submersion.
///
/// The authoritative retained-water value lives on ItemInstance so it survives:
/// - pickup into Hands/inventory;
/// - world-object destruction/recreation;
/// - save/load through ItemInstanceSnapshot.
///
/// Capacity comes from ForceBody2D.Volume. Water mass always uses the shared
/// PhysicsGlobals.WaterDensity.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ForceBody2D))]
[RequireComponent(typeof(BuoyancyPolygonForce))]
[RequireComponent(typeof(WorldItem))]
public sealed class WaterloggingMass2D :
    MonoBehaviour,
    IWorldItemMassModifier,
    IInteractionDetailProvider
{
    [Header("Gameplay Authority")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("Flood Capacity")]
    [Tooltip(
        "Fraction of ForceBody2D.Volume available to retained flood water. " +
        "0.5 means half of the body's physical volume can eventually contain water.")]
    [SerializeField, Range(0f, 1f)]
    private float usableFloodVolumeFraction = 0.5f;

    [Header("Rates")]
    [Tooltip(
        "Fraction of usable flood capacity added per second while fully submerged. " +
        "0.001667 is approximately 10 minutes from dry to full.")]
    [SerializeField, Min(0f)]
    private float leakRateFractionPerSecond = 1f / 600f;

    [Tooltip(
        "Fraction of usable flood capacity drained per second while completely out of water. " +
        "0.003333 is approximately 5 minutes from full to dry.")]
    [SerializeField, Min(0f)]
    private float drainRateFractionPerSecond = 1f / 300f;

    [Header("Hover Wetness")]
    [SerializeField] private bool showWetnessDetail = true;

    [Tooltip("Common prompt line format. {0} is replaced with Dry/Damp/Wet/Soaked/Flooded.")]
    [SerializeField] private string wetnessDetailFormat = "Wetness: {0}";

    [Tooltip("Flooded01 at or above this value is described as Damp.")]
    [SerializeField, Range(0f, 1f)] private float dampAt01 = 0.05f;

    [Tooltip("Flooded01 at or above this value is described as Wet.")]
    [SerializeField, Range(0f, 1f)] private float wetAt01 = 0.25f;

    [Tooltip("Flooded01 at or above this value is described as Soaked.")]
    [SerializeField, Range(0f, 1f)] private float soakedAt01 = 0.50f;

    [Tooltip("Flooded01 at or above this value is described as Flooded.")]
    [SerializeField, Range(0f, 1f)] private float floodedAt01 = 0.90f;

    private ForceBody2D _forceBody;
    private BuoyancyPolygonForce _buoyancy;
    private WorldItem _worldItem;
    private PhysicsGlobals _physicsGlobals;

    public float UsableFloodVolumeFraction => usableFloodVolumeFraction;
    public float LeakRateFractionPerSecond => leakRateFractionPerSecond;
    public float DrainRateFractionPerSecond => drainRateFractionPerSecond;

    public float MaximumFloodedVolume =>
        _forceBody != null
            ? Mathf.Max(0f, _forceBody.Volume) *
              Mathf.Clamp01(usableFloodVolumeFraction)
            : 0f;

    public float CurrentFloodedVolume =>
        Mathf.Min(
            ResolveRetainedWaterVolume(),
            MaximumFloodedVolume);

    public float CurrentWaterMass =>
        CurrentFloodedVolume *
        ResolveWaterDensity();

    public float Flooded01
    {
        get
        {
            float maxVolume = MaximumFloodedVolume;

            return maxVolume > 0.000001f
                ? Mathf.Clamp01(CurrentFloodedVolume / maxVolume)
                : 0f;
        }
    }

    private void Reset()
    {
        ResolveRefs();
    }

    private void Awake()
    {
        ResolveRefs();
    }

    private void OnEnable()
    {
        ResolveRefs();
        ClampStoredStateToCapacity();

        if (_worldItem != null)
            _worldItem.RefreshPhysicalMass();
    }

    private void OnValidate()
    {
        usableFloodVolumeFraction =
            Mathf.Clamp01(usableFloodVolumeFraction);

        leakRateFractionPerSecond =
            Mathf.Max(0f, leakRateFractionPerSecond);

        drainRateFractionPerSecond =
            Mathf.Max(0f, drainRateFractionPerSecond);

        dampAt01 =
            Mathf.Clamp01(dampAt01);

        wetAt01 =
            Mathf.Clamp(wetAt01, dampAt01, 1f);

        soakedAt01 =
            Mathf.Clamp(soakedAt01, wetAt01, 1f);

        floodedAt01 =
            Mathf.Clamp(floodedAt01, soakedAt01, 1f);
    }

    private void FixedUpdate()
    {
        if (!GameplayAuthority.CanRun(gameplayAuthorityMode))
            return;

        ResolveRefs();

        ItemInstance instance =
            ResolveItemInstance();

        if (_forceBody == null ||
            _buoyancy == null ||
            _worldItem == null ||
            instance == null ||
            ResolvePhysicsGlobals() == null)
        {
            return;
        }

        float maxFloodedVolume =
            MaximumFloodedVolume;

        if (maxFloodedVolume <= 0f)
        {
            if (instance.RetainedWaterVolume > 0f)
                instance.SetRetainedWaterVolume(0f);

            return;
        }

        if (instance.RetainedWaterVolume >
            maxFloodedVolume)
        {
            instance.SetRetainedWaterVolume(
                maxFloodedVolume);
            return;
        }

        float submerged01 =
            Mathf.Clamp01(
                _buoyancy.SubmergedFraction);

        float current =
            instance.RetainedWaterVolume;

        float next =
            current;

        if (submerged01 > 0f &&
            leakRateFractionPerSecond > 0f &&
            current < maxFloodedVolume)
        {
            // Leak rate scales with actual geometric submersion. As the crate
            // gets heavier it naturally rides lower, which accelerates flooding.
            float floodVolumePerSecond =
                maxFloodedVolume *
                leakRateFractionPerSecond *
                submerged01;

            next =
                Mathf.Min(
                    maxFloodedVolume,
                    current +
                    floodVolumePerSecond *
                    Time.fixedDeltaTime);
        }
        else if (submerged01 <= 0f &&
                 drainRateFractionPerSecond > 0f &&
                 current > 0f)
        {
            // Drain only when completely clear of water. A partially submerged
            // crate is still taking water rather than somehow drying through the
            // dry half of its hull.
            float drainVolumePerSecond =
                maxFloodedVolume *
                drainRateFractionPerSecond;

            next =
                Mathf.Max(
                    0f,
                    current -
                    drainVolumePerSecond *
                    Time.fixedDeltaTime);
        }

        if (Mathf.Abs(next - current) >
            0.000001f)
        {
            // ItemInstance.Changed makes WorldItem refresh physical mass, so the
            // item remains the single Rigidbody2D-mass writer.
            instance.SetRetainedWaterVolume(
                next);
        }
    }

    public bool TryGetInteractionDetail(
        in InteractContext context,
        out string detail)
    {
        detail = null;

        if (!showWetnessDetail)
            return false;

        string descriptor =
            ResolveWetnessDescriptor(
                Flooded01);

        if (string.IsNullOrWhiteSpace(wetnessDetailFormat))
        {
            detail = descriptor;
        }
        else
        {
            detail =
                wetnessDetailFormat.Replace(
                    "{0}",
                    descriptor);
        }

        return !string.IsNullOrWhiteSpace(detail);
    }

    private string ResolveWetnessDescriptor(
        float flooded01)
    {
        flooded01 =
            Mathf.Clamp01(
                flooded01);

        if (flooded01 >= floodedAt01)
            return "Flooded";

        if (flooded01 >= soakedAt01)
            return "Soaked";

        if (flooded01 >= wetAt01)
            return "Wet";

        if (flooded01 >= dampAt01)
            return "Damp";

        return "Dry";
    }

    /// <summary>
    /// WorldItem remains the single writer for Rigidbody2D mass. This modifier
    /// adds retained-water mass on top of the canonical ItemInstance/container
    /// mass and any earlier mass modifiers.
    /// </summary>
    public float ModifyWorldItemMass(
        ItemInstance instance,
        float canonicalMass)
    {
        float retainedVolume =
            instance != null
                ? Mathf.Min(
                    instance.RetainedWaterVolume,
                    MaximumFloodedVolume)
                : 0f;

        float waterMass =
            retainedVolume *
            ResolveWaterDensity();

        return
            Mathf.Max(0f, canonicalMass) +
            Mathf.Max(0f, waterMass);
    }

    private ItemInstance ResolveItemInstance()
    {
        return
            _worldItem != null
                ? _worldItem.Instance
                : null;
    }

    private float ResolveRetainedWaterVolume()
    {
        ItemInstance instance =
            ResolveItemInstance();

        return
            instance != null
                ? Mathf.Max(
                    0f,
                    instance.RetainedWaterVolume)
                : 0f;
    }

    private void ResolveRefs()
    {
        if (_forceBody == null)
            _forceBody =
                GetComponent<ForceBody2D>();

        if (_buoyancy == null)
            _buoyancy =
                GetComponent<BuoyancyPolygonForce>();

        if (_worldItem == null)
            _worldItem =
                GetComponent<WorldItem>();

        ResolvePhysicsGlobals();
    }

    private PhysicsGlobals ResolvePhysicsGlobals()
    {
        if (_physicsGlobals == null)
        {
            _physicsGlobals =
                PhysicsManager.Instance?.globals;
        }

        return _physicsGlobals;
    }

    private float ResolveWaterDensity()
    {
        PhysicsGlobals globals =
            ResolvePhysicsGlobals();

        return globals != null
            ? Mathf.Max(
                0f,
                globals.WaterDensity)
            : 0f;
    }

    private void ClampStoredStateToCapacity()
    {
        ResolveRefs();

        ItemInstance instance =
            ResolveItemInstance();

        if (instance == null)
            return;

        float maxFloodedVolume =
            MaximumFloodedVolume;

        float clamped =
            Mathf.Clamp(
                instance.RetainedWaterVolume,
                0f,
                Mathf.Max(
                    0f,
                    maxFloodedVolume));

        if (Mathf.Abs(
                clamped -
                instance.RetainedWaterVolume) >
            0.000001f)
        {
            instance.SetRetainedWaterVolume(
                clamped);
        }
    }
}
