using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Floodable compartment defined by a convex 4-point polygon
/// (rectangle or trapezoid in 2D side view).
/// </summary>
[DisallowMultipleComponent]
public class Compartment : MonoBehaviour, IMassContribution
{
    // ========================
    // Identification
    // ========================

    public string compartmentName;

    // ========================
    // Geometry
    // ========================

    [Header("Geometry (Local Space, CW)")]
    public Vector2 p0; // top-left
    public Vector2 p1; // top-right
    public Vector2 p2; // bottom-right
    public Vector2 p3; // bottom-left
    //public float effectiveDepth = 5f;

    [Header("Shape (Local Space, CW)")]
    [SerializeField]
    private Vector2[] localCorners = new Vector2[4]
    {
        new Vector2(-0.5f, 0.5f),
        new Vector2( 0.5f, 0.5f),
        new Vector2( 0.5f, -0.5f),
        new Vector2(-0.5f, -0.5f)
    };

    // ========================
    // Mass Contribution
    // ========================

    public float MassContribution => WaterMass;
    public float WaterMass => waterArea;

    public Vector2 WorldCenterOfMass
    {
        get
        {
            float topY = Mathf.Max(GetWaterSurfaceWorldY(), FloorY);
            float centerY = (FloorY + topY) * 0.5f;

            // Average X of compartment bottom/top edges (simple approximation)
            float centerX =
                (transform.TransformPoint(p0).x +
                 transform.TransformPoint(p1).x +
                 transform.TransformPoint(p2).x +
                 transform.TransformPoint(p3).x) * 0.25f;

            return new Vector2(centerX, centerY);
        }
    }

    // ========================
    // Fluid State
    // ========================

    private float waterSurfaceOffset;    // world-space horizontal plane
    [SerializeField] private float waterArea; // 2D cross-section area

    public float WaterArea => waterArea;

    public float MaxWaterArea =>
        (BottomWidth + TopWidth) * 0.5f * Height;

    public float WaterSurfaceOffset => waterSurfaceOffset;

    // ========================
    // Air State
    // ========================

    public float minAirFraction = 0.2f;
    [Range(0f, 1f)] public float airIntegrity = 1f;
    public float airLeakRate = 0.00001f;
    [HideInInspector]
    internal bool _canReleaseAir;

    // ========================
    // Connectivity
    // ========================

    public readonly List<CompartmentConnection> connections = new();
    [Header("External Water Sources")]
    public List<ExternalWaterSource> externalWaterSources = new List<ExternalWaterSource>();

    // ========================
    // Derived Geometry
    // ========================

    public float FloorY => transform.TransformPoint(p3).y;
    public float CeilingY => transform.TransformPoint(p0).y;
    public float Height => Vector2.Distance(transform.TransformPoint(p0), transform.TransformPoint(p3));

    public float BottomWidth =>
        Vector2.Distance(
            transform.TransformPoint(p2),
            transform.TransformPoint(p3)
        );

    public float TopWidth =>
        Vector2.Distance(
            transform.TransformPoint(p0),
            transform.TransformPoint(p1)
        );

    // ========================
    // Fluid Queries
    // ========================

    //public float AirVolume => Mathf.Max(0f, maxWaterVolume - waterArea);

    public float AvailableCapacity
    {
        get
        {
            float maxAllowed =
                Mathf.Min(
                    MaxWaterArea,
                    MaxWaterArea * (1f - minAirFraction * airIntegrity)
                );

            return Mathf.Max(0f, maxAllowed - WaterArea);
        }
    }

    private void OnEnable()
    {
        RecomputeWaterSurface();
    }

    // ========================
    // Public API
    // ========================

    public float AcceptWater(float volume)
    {
        if (volume <= 0f)
            return 0f;

        float accepted = Mathf.Min(volume, AvailableCapacity);
        waterArea += accepted;
        RecomputeWaterSurface();
        return accepted;
    }

    public void RemoveWater(float volume)
    {
        if (volume <= 0f)
            return;

        waterArea = Mathf.Max(0f, waterArea - volume);
        RecomputeWaterSurface();
    }

    public void UpdateAirIntegrity(bool canReleaseAir, float dt)
    {
        if (canReleaseAir)
            airIntegrity = 1f;
        else
            airIntegrity = Mathf.Clamp01(airIntegrity - airLeakRate * dt);
    }

    public bool HasActiveExternalWaterSource()
    {
        foreach (var src in externalWaterSources)
        {
            if (src.IsActive)
                return true;
        }
        return false;
    }

    public float GetExternalWaterDelta(float deltaTime)
    {
        float total = 0f;
        foreach (var src in externalWaterSources)
        {
            if (src.IsActive)
                total += src.GetWaterContribution(deltaTime);
        }
        return total;
    }

    public void RecomputeWaterSurface()
    {
        waterSurfaceOffset = SolveSurfaceOffsetFromArea(waterArea);
    }

    public float GetWaterSurfaceWorldY()
    {
        Vector2 planeNormal = -Physics2D.gravity.normalized;

        // find a point on the plane
        Vector2 p = planeNormal * waterSurfaceOffset;

        return p.y;
    }


    // ========================
    // Geometry Math
    // ========================

    public float ComputeSubmergedArea(
        Vector2 planeNormal,
        float planeOffset)
    {
        Vector2[] poly = GetWorldCorners();
        List<Vector2> clipped =
            ConvexPolygonUtil.ClipBelowPlane(
                poly,
                planeNormal,
                planeOffset);

        return ConvexPolygonUtil.PolygonArea(clipped);
    }

    public float SolveSurfaceOffsetFromArea(float targetArea)
    {

        Vector2 planeNormal = -Physics2D.gravity.normalized; // TO DO - shouldn't be hard coded here
        Vector2[] corners = GetWorldCorners();

        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;

        foreach (var c in corners)
        {
            float d = Vector2.Dot(c, planeNormal);
            min = Mathf.Min(min, d);
            max = Mathf.Max(max, d);
        }

        for (int i = 0; i < 12; i++)
        {
            float mid = (min + max) * 0.5f;
            float area = ComputeSubmergedArea(planeNormal, mid);

            if (area < targetArea)
                min = mid;
            else
                max = mid;
        }

        return (min + max) * 0.5f;
    }


    public Vector2[] GetWorldCorners()
    {
        Vector2[] world = new Vector2[4];
        for (int i = 0; i < 4; i++)
            world[i] = transform.TransformPoint(localCorners[i]);
        return world;
    }

    public Vector2[] GetWaterPolygonWorld()
    {
        Vector2 planeNormal = -Physics2D.gravity.normalized;
        float planeOffset = waterSurfaceOffset;
        List<Vector2> clipped = ConvexPolygonUtil.ClipBelowPlane(GetWorldCorners(), planeNormal, planeOffset);
        return clipped.ToArray();
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure vertical ordering
        if (p0.y < p3.y)
        {
            float mid = (p0.y + p3.y) * 0.5f;
            p0.y = p1.y = mid + 0.5f;
            p2.y = p3.y = mid - 0.5f;
        }
    }

    private void OnDrawGizmos()
    {
        DrawCompartmentGizmo();
    }

    private void DrawCompartmentGizmo()
    {
        Vector2[] corners = GetWorldCorners();
        if (corners == null || corners.Length != 4)
            return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < 4; i++)
        {
            Vector2 a = corners[i];
            Vector2 b = corners[(i + 1) % 4];
            Gizmos.DrawLine(a, b);
        }
    }

#endif
}
