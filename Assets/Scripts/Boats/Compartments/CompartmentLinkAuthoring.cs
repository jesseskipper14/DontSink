using UnityEngine;

public enum CompartmentLinkType
{
    Hatch = 0,
    Door = 1,
    Ledge = 2
}

public enum CompartmentLinkResolutionType
{
    Invalid = 0,
    Internal = 1,
    ExternalExposure = 2
}

[DisallowMultipleComponent]
public sealed class CompartmentLinkAuthoring : MonoBehaviour
{
    public const string GeneratedExternalPrefix = "[GeneratedLink]:";

    [Header("Identity")]
    [SerializeField] private string linkId = "";

    [Header("References")]
    public Boat boat;
    public HatchRuntime hatchRuntime;      // optional, mainly for closeable hatches/doors
    public BoxCollider2D openingCollider;  // defines opening rectangle

    [Header("Type")]
    public CompartmentLinkType linkType = CompartmentLinkType.Hatch;

    [Header("Resolve Settings")]
    [Min(0.01f)] public float sampleOffset = 0.6f;
    public bool autoResolveInEditor = false;

    [Header("Generated Internal Connection")]
    [Min(0f)] public float flowCoefficient = 1f;

    [Header("Generated External Exposure")]
    public ExternalWaterSourceType externalSourceType = ExternalWaterSourceType.SeaBreach;
    [Min(0f)] public float externalRate = 0.1f;

    public string LinkId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(linkId))
                linkId = gameObject.name;
            return linkId;
        }
    }

    public bool IsCloseable =>
        linkType == CompartmentLinkType.Hatch ||
        linkType == CompartmentLinkType.Door;

    public bool IsCurrentlyOpen
    {
        get
        {
            if (!IsCloseable)
                return true;

            return hatchRuntime == null || hatchRuntime.IsOpen;
        }
    }

    // Through-axis is the direction we sample "across" the opening.
    // Keep transform.right for all link types for now.
    public Vector2 ThroughAxisWorld
    {
        get
        {
            Transform axisSource = openingCollider != null ? openingCollider.transform : transform;

            return linkType switch
            {
                // Horizontal opening in floor/roof: probe above/below.
                CompartmentLinkType.Hatch => axisSource.up,
                CompartmentLinkType.Ledge => axisSource.up,

                // Vertical opening in wall: probe left/right.
                CompartmentLinkType.Door => axisSource.right,

                _ => axisSource.right
            };
        }
    }

    public struct ResolutionResult
    {
        public CompartmentLinkResolutionType resolutionType;
        public Compartment A;
        public Compartment B;
        public Vector2 sampleA;
        public Vector2 sampleB;
        public string reason;

        public Compartment ExposedCompartment
        {
            get
            {
                if (A != null && B == null) return A;
                if (B != null && A == null) return B;
                return null;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(linkId))
            linkId = gameObject.name;

        if (!autoResolveInEditor)
            return;
    }
#endif

    private void Reset()
    {
        ResolveRefs();
        if (string.IsNullOrWhiteSpace(linkId))
            linkId = gameObject.name;
    }

    public void ResolveRefs()
    {
        if (boat == null)
            boat = GetComponentInParent<Boat>();

        if (hatchRuntime == null)
            hatchRuntime = GetComponent<HatchRuntime>();

        if (openingCollider == null)
            openingCollider = GetComponent<BoxCollider2D>();

        if (string.IsNullOrWhiteSpace(linkId))
            linkId = gameObject.name;
    }

    public ResolutionResult Resolve()
    {
        ResolveRefs();

        ResolutionResult result = new ResolutionResult
        {
            resolutionType = CompartmentLinkResolutionType.Invalid,
            reason = "Unresolved"
        };

        if (boat == null)
        {
            result.reason = "No Boat found in parents.";
            return result;
        }

        if (openingCollider == null)
        {
            result.reason = "No BoxCollider2D assigned/found for openingCollider.";
            return result;
        }

        Vector2 axis = ThroughAxisWorld.normalized;
        Vector2 center = openingCollider.bounds.center;

        Vector2 pA = center - axis * sampleOffset;
        Vector2 pB = center + axis * sampleOffset;

        Debug.Log(
            $"[CompartmentLinkAuthoring] Resolve '{name}' type={linkType} " +
            $"axis={axis} center={center} sampleA={pA} sampleB={pB}",
            this);

        Compartment A = FindContainingCompartment(boat, pA);
        Compartment B = FindContainingCompartment(boat, pB);

        result.A = A;
        result.B = B;
        result.sampleA = pA;
        result.sampleB = pB;

        if (A != null && B != null)
        {
            if (A == B)
            {
                result.resolutionType = CompartmentLinkResolutionType.Invalid;
                result.reason = "Both sample points resolved to the same compartment.";
                return result;
            }

            result.resolutionType = CompartmentLinkResolutionType.Internal;
            result.reason = "Resolved internal connection.";
            return result;
        }

        if ((A != null && B == null) || (A == null && B != null))
        {
            result.resolutionType = CompartmentLinkResolutionType.ExternalExposure;
            result.reason = "Resolved compartment-to-exterior exposure.";
            return result;
        }

        result.resolutionType = CompartmentLinkResolutionType.Invalid;
        result.reason = "No compartment found on either side of the opening.";
        return result;
    }

    public void ApplyGeometryToConnection(CompartmentConnection conn)
    {
        if (conn == null || openingCollider == null)
            return;

        conn.transform = transform;

        // Use collider world bounds, then convert into local space of this transform.
        Bounds wb = openingCollider.bounds;

        Vector3 bl = transform.InverseTransformPoint(new Vector3(wb.min.x, wb.min.y, 0f));
        Vector3 tr = transform.InverseTransformPoint(new Vector3(wb.max.x, wb.max.y, 0f));

        conn.openingLeftX = Mathf.Min(bl.x, tr.x);
        conn.openingRightX = Mathf.Max(bl.x, tr.x);
        conn.openingBottomY = Mathf.Min(bl.y, tr.y);
        conn.openingTopY = Mathf.Max(bl.y, tr.y);
        conn.flowCoefficient = flowCoefficient;
        conn.isOpen = IsCurrentlyOpen;
    }

    public ExternalWaterSource BuildExternalSource()
    {
        return new ExternalWaterSource
        {
            name = GetGeneratedExternalSourceName(LinkId),
            type = externalSourceType,
            rate = externalRate,
            IsActive = IsCurrentlyOpen
        };
    }

    public static string GetGeneratedExternalSourceName(string linkId)
    {
        return $"{GeneratedExternalPrefix}{linkId}";
    }

    private static Compartment FindContainingCompartment(Boat boat, Vector2 worldPoint)
    {
        if (boat == null || boat.Compartments == null)
            return null;

        foreach (var c in boat.Compartments)
        {
            if (c == null)
                continue;

            var poly = c.GetWorldCorners();
            if (poly != null && poly.Length >= 3)
            {
                if (ConvexPolygonUtil.PointInsideConvex(worldPoint, poly))
                    return c;
            }

            var box = c.GetComponent<BoxCollider2D>();
            if (box != null && box.bounds.Contains(worldPoint))
                return c;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector2 axis = ThroughAxisWorld.normalized;
        Vector2 c = openingCollider != null ? openingCollider.bounds.center : (Vector2)transform.position;

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(c - axis * sampleOffset, 0.05f);
        Gizmos.DrawSphere(c + axis * sampleOffset, 0.05f);
        Gizmos.DrawLine(c - axis * sampleOffset, c + axis * sampleOffset);
    }
#endif
}