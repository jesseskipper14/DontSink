using UnityEngine;

/// <summary>
/// Defines how a Hardpoint verifies that a module's required horizontal boat
/// support span is actually supported by boat structure beneath the mount.
///
/// This is intentionally separate from HardpointReservedFootprint:
/// - HardpointReservedFootprint = "other builder content may not occupy here"
/// - HardpointSupportFootprint  = "the boat must physically support this module here"
///
/// ModuleDefinition owns the required width. This component owns the probe
/// location/rules used to test that width on this particular hardpoint.
/// </summary>
[DisallowMultipleComponent]
public sealed class HardpointSupportFootprint :
    MonoBehaviour
{
    [Header("Support Probe")]
    [SerializeField] private bool enforceSupport = true;

    [Tooltip(
        "Local offset from this transform to the CENTER of the support span. " +
        "Place this at the module's mounting/contact line on the boat.")]
    [SerializeField]
    private Vector2 localProbeOffset =
        Vector2.zero;

    [Tooltip(
        "How far below the authored support line boat structure may be and still count.")]
    [Min(0.01f)]
    [SerializeField]
    private float maximumSupportDrop =
        0.35f;

    [Tooltip(
        "Maximum spacing between support samples across the required module width. " +
        "Smaller values catch smaller unsupported gaps.")]
    [Min(0.02f)]
    [SerializeField]
    private float sampleSpacing =
        0.20f;

    [Tooltip(
        "Small inset from each required-width edge before the first/last sample. " +
        "Prevents floating-point edge contact from rejecting an otherwise flush placement.")]
    [Min(0f)]
    [SerializeField]
    private float edgeInset =
        0.02f;

    [Tooltip(
        "Layers that may count as supporting boat structure. " +
        "A zero mask automatically prefers Hull + Ground if those layers exist, " +
        "otherwise it falls back to all layers.")]
    [SerializeField]
    private LayerMask supportLayerMask =
        0;

    [Tooltip(
        "Normally false. Trigger volumes such as boarding/visibility/cargo zones " +
        "should not magically become structural deck.")]
    [SerializeField]
    private bool includeTriggerSurfaces =
        false;

    [Tooltip(
        "If true, only colliders under the same Boat root as this hardpoint can count.")]
    [SerializeField]
    private bool requireSameBoat =
        true;

    [Header("Debug")]
    [SerializeField]
    private bool drawGizmo =
        true;

    [SerializeField]
    private bool verboseLogging =
        false;

    public bool EnforceSupport =>
        enforceSupport;

    public Vector2 LocalProbeOffset =>
        localProbeOffset;

    public float MaximumSupportDrop =>
        Mathf.Max(
            0.01f,
            maximumSupportDrop);

    public float SampleSpacing =>
        Mathf.Max(
            0.02f,
            sampleSpacing);

    /// <summary>
    /// Validates the support width authored by a ModuleDefinition.
    /// Modules with a zero support requirement always pass.
    /// </summary>
    public bool TryValidateModuleSupport(
        ModuleDefinition module,
        out string reason)
    {
        reason =
            null;

        if (module == null)
        {
            reason =
                "Module definition is null.";

            return false;
        }

        if (!enforceSupport ||
            !module.RequiresBoatSupport)
        {
            return true;
        }

        Boat boat =
            GetComponentInParent<Boat>();

        Transform boatRoot =
            boat != null
                ? boat.transform
                : null;

        return
            TryValidateSupportWidth(
                module.RequiredBoatSupportWidth,
                boatRoot,
                out reason);
    }

    /// <summary>
    /// Runtime/editor-safe structural support test.
    ///
    /// The required span follows this transform's local X axis. At evenly spaced
    /// points across that span, a ray is cast along local -Y. Every sample must
    /// find a valid supporting collider within MaximumSupportDrop.
    /// </summary>
    public bool TryValidateSupportWidth(
        float requiredWidth,
        Transform explicitBoatRoot,
        out string reason)
    {
        reason =
            null;

        if (!enforceSupport ||
            requiredWidth <= 0.001f)
        {
            return true;
        }

        if (requireSameBoat &&
            explicitBoatRoot == null)
        {
            Boat boat =
                GetComponentInParent<Boat>();

            explicitBoatRoot =
                boat != null
                    ? boat.transform
                    : null;
        }

        if (requireSameBoat &&
            explicitBoatRoot == null)
        {
            reason =
                "No Boat root could be resolved for support validation.";

            return false;
        }

        Physics2D.SyncTransforms();

        float width =
            Mathf.Max(
                0.001f,
                requiredWidth);

        float half =
            width * 0.5f;

        float inset =
            Mathf.Clamp(
                edgeInset,
                0f,
                Mathf.Max(
                    0f,
                    half - 0.001f));

        float left =
            -half + inset;

        float right =
            half - inset;

        float span =
            Mathf.Max(
                0f,
                right - left);

        int sampleCount =
            Mathf.Max(
                2,
                Mathf.CeilToInt(
                    span /
                    SampleSpacing) +
                1);

        Vector3 origin =
            transform.TransformPoint(
                localProbeOffset);

        Vector2 rightAxis =
            transform.right;

        Vector2 downAxis =
            -transform.up;

        float startLift =
            0.03f;

        int validSamples =
            0;

        for (int i = 0;
             i < sampleCount;
             i++)
        {
            float t =
                sampleCount <= 1
                    ? 0.5f
                    : i /
                      (float)(
                          sampleCount - 1);

            float localX =
                Mathf.Lerp(
                    left,
                    right,
                    t);

            Vector2 sampleOrigin =
                (Vector2)origin +
                rightAxis *
                localX +
                (Vector2)transform.up *
                startLift;

            if (!TryFindSupportBelow(
                    sampleOrigin,
                    downAxis,
                    MaximumSupportDrop +
                    startLift,
                    explicitBoatRoot,
                    out RaycastHit2D hit))
            {
                reason =
                    $"Required support width {width:0.##} m is not fully supported. " +
                    $"Missing support near sample {i + 1}/{sampleCount} " +
                    $"at world ({sampleOrigin.x:0.##}, {sampleOrigin.y:0.##}).";

                Log(
                    reason);

                return false;
            }

            validSamples++;

            if (verboseLogging)
            {
                Debug.Log(
                    $"[HardpointSupportFootprint:{name}] " +
                    $"Support sample {i + 1}/{sampleCount} hit '{hit.collider.name}' " +
                    $"distance={hit.distance:0.###}.",
                    this);
            }
        }

        if (validSamples != sampleCount)
        {
            reason =
                $"Support validation failed: {validSamples}/{sampleCount} samples were supported.";

            return false;
        }

        return true;
    }

    private bool TryFindSupportBelow(
        Vector2 origin,
        Vector2 direction,
        float distance,
        Transform boatRoot,
        out RaycastHit2D bestHit)
    {
        bestHit =
            default;

        int mask =
            GetEffectiveSupportMask();

        RaycastHit2D[] hits =
            Physics2D.RaycastAll(
                origin,
                direction,
                Mathf.Max(
                    0.01f,
                    distance),
                mask);

        if (hits == null ||
            hits.Length == 0)
        {
            return false;
        }

        float bestDistance =
            float.PositiveInfinity;

        for (int i = 0;
             i < hits.Length;
             i++)
        {
            RaycastHit2D hit =
                hits[i];

            Collider2D collider =
                hit.collider;

            if (!IsValidSupportCollider(
                    collider,
                    boatRoot))
            {
                continue;
            }

            float hitDistance =
                hit.distance;

            if (hitDistance >=
                bestDistance)
            {
                continue;
            }

            bestDistance =
                hitDistance;

            bestHit =
                hit;
        }

        return
            bestHit.collider != null;
    }

    private bool IsValidSupportCollider(
        Collider2D collider,
        Transform boatRoot)
    {
        if (collider == null ||
            !collider.enabled)
        {
            return false;
        }

        if (!includeTriggerSurfaces &&
            collider.isTrigger)
        {
            return false;
        }

        // The hardpoint and its own children cannot support themselves.
        if (collider.transform == transform ||
            collider.transform.IsChildOf(
                transform))
        {
            return false;
        }

        Hardpoint ownHardpoint =
            GetComponentInParent<Hardpoint>();

        if (ownHardpoint != null &&
            (collider.transform ==
                 ownHardpoint.transform ||
             collider.transform.IsChildOf(
                 ownHardpoint.transform)))
        {
            return false;
        }

        if (requireSameBoat &&
            boatRoot != null)
        {
            Transform ct =
                collider.transform;

            if (ct != boatRoot &&
                !ct.IsChildOf(
                    boatRoot))
            {
                return false;
            }
        }

        return true;
    }

    private int GetEffectiveSupportMask()
    {
        if (supportLayerMask.value != 0)
            return supportLayerMask.value;

        int mask =
            0;

        AddNamedLayer(
            ref mask,
            "Hull");

        AddNamedLayer(
            ref mask,
            "Ground");

        return
            mask != 0
                ? mask
                : Physics2D.AllLayers;
    }

    private static void AddNamedLayer(
        ref int mask,
        string layerName)
    {
        int layer =
            LayerMask.NameToLayer(
                layerName);

        if (layer < 0)
            return;

        mask |=
            1 << layer;
    }

    private void Log(
        string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[HardpointSupportFootprint:{name}] {message}",
            this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maximumSupportDrop =
            Mathf.Max(
                0.01f,
                maximumSupportDrop);

        sampleSpacing =
            Mathf.Max(
                0.02f,
                sampleSpacing);

        edgeInset =
            Mathf.Max(
                0f,
                edgeInset);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo)
            return;

        Hardpoint hardpoint =
            GetComponentInParent<Hardpoint>();

        ModuleDefinition module =
            hardpoint != null &&
            hardpoint.HasInstalledModule &&
            hardpoint.InstalledModule != null
                ? hardpoint.InstalledModule.Definition
                : hardpoint != null
                    ? hardpoint.StartingModuleDefinition
                    : null;

        float width =
            module != null
                ? module.RequiredBoatSupportWidth
                : 0f;

        if (width <= 0.001f)
        {
            Gizmos.color =
                new Color(
                    0.35f,
                    0.75f,
                    1f,
                    0.85f);

            Vector3 center =
                transform.TransformPoint(
                    localProbeOffset);

            Gizmos.DrawWireSphere(
                center,
                0.06f);

            return;
        }

        Boat boat =
            GetComponentInParent<Boat>();

        Transform boatRoot =
            boat != null
                ? boat.transform
                : null;

        bool valid =
            TryValidateSupportWidth(
                width,
                boatRoot,
                out _);

        Gizmos.color =
            valid
                ? new Color(
                    0.2f,
                    1f,
                    0.35f,
                    0.9f)
                : new Color(
                    1f,
                    0.2f,
                    0.15f,
                    0.9f);

        Vector3 centerWorld =
            transform.TransformPoint(
                localProbeOffset);

        Vector3 halfAxis =
            transform.right *
            (width * 0.5f);

        Vector3 a =
            centerWorld -
            halfAxis;

        Vector3 b =
            centerWorld +
            halfAxis;

        Gizmos.DrawLine(
            a,
            b);

        Vector3 down =
            -transform.up *
            MaximumSupportDrop;

        Gizmos.DrawLine(
            a,
            a + down);

        Gizmos.DrawLine(
            b,
            b + down);
    }
#endif
}
