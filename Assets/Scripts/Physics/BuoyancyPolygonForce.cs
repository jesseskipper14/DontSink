using UnityEngine;
using System.Collections.Generic;

public class BuoyancyPolygonForce : MonoBehaviour, IForceProvider, ISubmersionProvider
{
    public MonoBehaviour bodySource; // must implement IForceBody
    private IForceBody body;

    public WaveManager waveManager;
    public WaveField wave;

    [Header("Water Context")]
    [SerializeField] private bool useBoatWaterContext = true;
    [SerializeField] private BoatWaterContextResolver explicitWaterContext;

    [Tooltip("Boat hull buoyancy should keep using ocean water. Interior water context is for players/items/cargo.")]
    [SerializeField] private bool boatBodyAlwaysUsesOcean = true;

    private BoatWaterExposure _activeExposure;

    private IWaveService waveService => waveManager;
    public int sliceCount = 10;

    [HideInInspector] public float lastTotalSubmersion = 0f;

    private PhysicsGlobals physicsGlobals;

    // Boat-only contributor cache. Compartments remain authoritative on Boat.Compartments;
    // this cache only avoids rediscovering static structural boundary authoring every physics tick.
    private Boat cachedContributorBoat;
    private CompartmentBoundaryAuthoring[] cachedBoundaryContributors;

    public float SubmergedFraction => lastTotalSubmersion;

    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 50;

    void Awake()
    {
        physicsGlobals = PhysicsManager.Instance?.globals;
        if (physicsGlobals == null)
        {
            Debug.LogError("PhysicsGlobals not found!");
        }

        body = ResolveAuthoritativeBodySource();
        if (body == null)
        {
            Debug.LogError("BuoyancyForce could not resolve an authoritative IForceBody");
            enabled = false;
            return;
        }

        ResolveWaveRefs();
    }

    private IForceBody ResolveAuthoritativeBodySource()
    {
        // Boat owns its specialized mass/geometry authority. For every generic
        // body, ForceBody2D owns dimensions/volume even if a legacy component
        // on the same GameObject also implements IForceBody.
        if (bodySource is Boat configuredBoat)
            return configuredBoat;

        ForceBody2D forceBody =
            GetComponent<ForceBody2D>();

        if (forceBody != null)
            return forceBody;

        return bodySource as IForceBody;
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag)
            return;

        ResolveWaveRefs();

        if (!TryResolveWaterExposure(body, out _activeExposure) ||
            !_activeExposure.HasWater)
        {
            lastTotalSubmersion = 0f;
            return;
        }

        if (_activeExposure.UsesOceanSurface &&
            waveManager == null)
        {
            lastTotalSubmersion = 0f;
            return;
        }

        List<Vector2[]> contributorPolygons =
            BuildWorldBuoyancyContributorPolygons(
                body,
                out float totalContributorArea,
                out bool usingLegacyFallback);

        if (contributorPolygons.Count == 0 ||
            totalContributorArea <= 0.000001f)
        {
            lastTotalSubmersion = 0f;
            return;
        }

        float totalSubmergedArea = 0f;

        // The fallback polygon describes the body's submersion geometry.
        // ForceBody2D.Volume is the authoritative displacement amount, so extra
        // volume contributions scale displacement without changing the visible/
        // collision dimensions or the geometric submerged fraction. Boat
        // contributor geometry already represents real displacement 1:1.
        float fallbackDisplacementScale =
            usingLegacyFallback
                ? Mathf.Max(0f, body.Volume) /
                  Mathf.Max(totalContributorArea, 0.000001f)
                : 1f;

        float accumulatedImpulse = 0f;
        float accumulatedImpulseX = 0f;
        float accumulatedSubmergedWidth = 0f;

        float maxTotalBuoyantForce =
            physicsGlobals.MaxBuoyantAcceleration *
            body.Mass;

        for (int contributorIndex = 0;
             contributorIndex < contributorPolygons.Count;
             contributorIndex++)
        {
            Vector2[] worldPoly =
                contributorPolygons[contributorIndex];

            if (worldPoly == null ||
                worldPoly.Length < 3)
            {
                continue;
            }

            float contributorArea =
                PolygonArea(
                    worldPoly);

            if (contributorArea <= 0.000001f)
                continue;

            float contributorAreaFraction =
                Mathf.Clamp01(
                    contributorArea /
                    totalContributorArea);

            List<Vector2> submergedPoly =
                ClipPolygonWithWave(
                    worldPoly);

            if (submergedPoly.Count < 3)
                continue;

            float minX = float.MaxValue;
            float maxX = float.MinValue;

            foreach (Vector2 pt in submergedPoly)
            {
                minX =
                    Mathf.Min(
                        minX,
                        pt.x);

                maxX =
                    Mathf.Max(
                        maxX,
                        pt.x);
            }

            float submergedWidth =
                maxX -
                minX;

            if (submergedWidth <= 0.000001f)
                continue;

            float sliceWidth =
                submergedWidth /
                Mathf.Max(
                    sliceCount,
                    1);

            for (int i = 0;
                 i < sliceCount;
                 i++)
            {
                float xLeft =
                    minX +
                    i *
                    sliceWidth;

                float xRight =
                    xLeft +
                    sliceWidth;

                List<Vector2> slicePoly =
                    ClipPolygonBetweenXPlanes(
                        submergedPoly,
                        xLeft,
                        xRight);

                if (slicePoly.Count < 3)
                    continue;

                float area =
                    PolygonArea(
                        slicePoly);

                if (area <= 0f)
                    continue;

                totalSubmergedArea +=
                    area;

                Vector2 centroid =
                    PolygonCentroid(
                        slicePoly,
                        area);

                // --- Buoyant force ---
                float sliceVolume =
                    area *
                    fallbackDisplacementScale;

                float sliceForce =
                    sliceVolume *
                    physicsGlobals.WaterDensity *
                    physicsGlobals.Gravity;

                // Preserve the legacy per-slice cap exactly for a single fallback
                // rectangle, while dividing the same whole-body cap among multiple
                // Boat contributors in proportion to their nominal displacement area.
                float maxSliceForce =
                    maxTotalBuoyantForce *
                    contributorAreaFraction /
                    Mathf.Max(
                        sliceCount,
                        1);

                sliceForce =
                    Mathf.Min(
                        sliceForce,
                        maxSliceForce);

                body.rb.AddForceAtPosition(
                    Vector2.up *
                    sliceForce,
                    centroid,
                    ForceMode2D.Force);

                // --- Wave momentum coupling (ported exactly) ---
                float waveY =
                    SampleActiveSurfaceY(
                        centroid.x);

                float sliceBottomY =
                    float.MaxValue;

                foreach (Vector2 pt in slicePoly)
                {
                    sliceBottomY =
                        Mathf.Min(
                            sliceBottomY,
                            pt.y);
                }

                float depthUnderSurface =
                    waveY -
                    sliceBottomY;

                if (depthUnderSurface <= 0f ||
                    depthUnderSurface > physicsGlobals.SurfaceInteractionDepth)
                    continue; // DO NOT TOUCH ESPECIALLY IF YOUR NAME IS CHATGPT
                else
                {
                    if (!_activeExposure.AllowsWaveMomentumCoupling)
                        continue;

                    float waveVelocity =
                        (SampleActiveSurfaceVelocity(
                             centroid.x -
                             sliceWidth *
                             0.5f) +
                         SampleActiveSurfaceVelocity(
                             centroid.x +
                             sliceWidth *
                             0.5f)) *
                        0.5f *
                        0.5f;

                    float bodyVelocity =
                        body.rb.GetPointVelocity(
                            centroid).y;

                    float relativeVelocity =
                        bodyVelocity -
                        waveVelocity;

                    float velocityTolerance =
                        Mathf.Max(
                            physicsGlobals.MinRelativeVelocityFactor *
                                Mathf.Abs(
                                    waveVelocity),
                            physicsGlobals.MinRelativeVelocityFactor *
                                Mathf.Abs(
                                    bodyVelocity),
                            physicsGlobals.MinRelativeVelocityAbsolute);

                    if (Mathf.Abs(
                            relativeVelocity) >
                        velocityTolerance)
                    {
                        // A single fallback rectangle resolves to the exact legacy
                        // body.Mass / sliceCount value. Multiple Boat contributors
                        // divide that same body-mass budget by contributor area.
                        float bodyMassSlice =
                            body.Mass *
                            contributorAreaFraction /
                            Mathf.Max(
                                sliceCount,
                                1);

                        float waterMass =
                            physicsGlobals.WaterDensity *
                            sliceVolume;

                        float totalMass =
                            bodyMassSlice +
                            waterMass;

                        if (totalMass > 0f)
                        {
                            float rawImpulse =
                                (bodyMassSlice *
                                 waterMass /
                                 totalMass) *
                                relativeVelocity;

                            float maxImpulse =
                                Mathf.Abs(
                                    relativeVelocity) *
                                waterMass;

                            float impulse =
                                Mathf.Clamp(
                                    rawImpulse,
                                    -maxImpulse,
                                    maxImpulse);

                            accumulatedImpulse +=
                                impulse *
                                area;

                            accumulatedImpulseX +=
                                centroid.x *
                                area;

                            accumulatedSubmergedWidth +=
                                area;
                        }
                    }
                }
            }
        }

        // Submersion is geometric. Added displacement volume changes how much
        // water the body displaces, not how much of its authored shape is wet.
        lastTotalSubmersion =
            Mathf.Clamp01(
                totalSubmergedArea /
                totalContributorArea);

        // --- Apply averaged wave impulse ---
        if (_activeExposure.AllowsWaveMomentumCoupling &&
            accumulatedSubmergedWidth > 0f)
        {
            float avgX =
                accumulatedImpulseX /
                accumulatedSubmergedWidth;

            float netImpulse =
                accumulatedImpulse /
                accumulatedSubmergedWidth *
                0.8f;

            waveManager.AddImpulse(
                avgX,
                netImpulse,
                accumulatedSubmergedWidth *
                0.5f);

            body.AddForce(
                Vector2.down *
                (netImpulse /
                 Time.fixedDeltaTime));
        }
    }

    /// <summary>
    /// Builds the geometry that actually displaces water.
    ///
    /// Boat:
    /// - every authoritative floodable Compartment contributes its full polygon;
    /// - structural CompartmentBoundaryAuthoring with Floor/Wall/Roof roles
    ///   contributes the exact collider footprint 1:1.
    ///
    /// Non-Boat objects, or Boats without any valid contributors, retain the
    /// original Width x Height rectangle behavior.
    /// </summary>
    private List<Vector2[]> BuildWorldBuoyancyContributorPolygons(
        IForceBody forceBody,
        out float totalArea,
        out bool usingLegacyFallback)
    {
        List<Vector2[]> polygons =
            new List<Vector2[]>();

        totalArea =
            0f;

        usingLegacyFallback =
            false;

        if (forceBody == null)
            return polygons;

        if (forceBody is Boat boat)
        {
            AddCompartmentBuoyancyPolygons(
                boat,
                polygons,
                ref totalArea);

            AddStructuralBoundaryBuoyancyPolygons(
                boat,
                polygons,
                ref totalArea);
        }

        // Preserve the original simple-buoyancy path for crates, loose items,
        // other IForceBody implementations, and legacy/simple Boats with no
        // compartment or structural contributor geometry.
        if (polygons.Count == 0 ||
            totalArea <= 0.000001f)
        {
            usingLegacyFallback =
                true;

            polygons.Clear();
            totalArea = 0f;

            Vector2[] fallback =
                BuildFallbackWorldRectangle(
                    forceBody);

            float fallbackArea =
                PolygonArea(
                    fallback);

            if (fallbackArea > 0.000001f)
            {
                polygons.Add(
                    fallback);

                totalArea =
                    fallbackArea;
            }
        }

        return polygons;
    }

    private void AddCompartmentBuoyancyPolygons(
        Boat boat,
        List<Vector2[]> polygons,
        ref float totalArea)
    {
        if (boat == null ||
            polygons == null ||
            boat.Compartments == null)
        {
            return;
        }

        HashSet<Compartment> seen =
            new HashSet<Compartment>();

        for (int i = 0;
             i < boat.Compartments.Count;
             i++)
        {
            Compartment compartment =
                boat.Compartments[i];

            if (compartment == null ||
                !seen.Add(
                    compartment))
            {
                continue;
            }

            Vector2[] worldCorners =
                compartment.GetWorldCorners();

            if (worldCorners == null ||
                worldCorners.Length < 3)
            {
                continue;
            }

            float area =
                PolygonArea(
                    worldCorners);

            if (area <= 0.000001f)
                continue;

            polygons.Add(
                worldCorners);

            totalArea +=
                area;
        }
    }

    private void AddStructuralBoundaryBuoyancyPolygons(
        Boat boat,
        List<Vector2[]> polygons,
        ref float totalArea)
    {
        if (boat == null ||
            polygons == null)
        {
            return;
        }

        EnsureBoundaryContributorCache(
            boat);

        if (cachedBoundaryContributors == null)
            return;

        for (int i = 0;
             i < cachedBoundaryContributors.Length;
             i++)
        {
            CompartmentBoundaryAuthoring boundary =
                cachedBoundaryContributors[i];

            if (!IsStructuralBuoyancyBoundary(
                    boundary))
            {
                continue;
            }

            Collider2D col =
                boundary.Collider;

            if (col == null ||
                !col.enabled ||
                !col.gameObject.activeInHierarchy)
            {
                continue;
            }

            AddColliderWorldPolygons(
                col,
                polygons,
                ref totalArea);
        }
    }

    private void EnsureBoundaryContributorCache(
        Boat boat)
    {
        bool shouldRefresh =
            cachedContributorBoat !=
                boat ||
            cachedBoundaryContributors ==
                null;

#if UNITY_EDITOR
        // In edit mode, authoring changes need to show up immediately in gizmos.
        if (!Application.isPlaying)
            shouldRefresh = true;
#endif

        if (!shouldRefresh)
            return;

        cachedContributorBoat =
            boat;

        cachedBoundaryContributors =
            boat.GetComponentsInChildren<CompartmentBoundaryAuthoring>(
                true);
    }

    private static bool IsStructuralBuoyancyBoundary(
        CompartmentBoundaryAuthoring boundary)
    {
        if (boundary == null)
            return false;

        // CountsAsBoundary controls compartment detection/sealing, not whether
        // the physical structural material itself displaces water.
        return
            boundary.HasRole(
                CompartmentBoundaryRole.Floor) ||
            boundary.HasRole(
                CompartmentBoundaryRole.Wall) ||
            boundary.HasRole(
                CompartmentBoundaryRole.Roof);
    }

    private void AddColliderWorldPolygons(
        Collider2D col,
        List<Vector2[]> polygons,
        ref float totalArea)
    {
        if (col == null ||
            polygons == null)
        {
            return;
        }

        if (col is BoxCollider2D box)
        {
            Vector2[] world =
                BuildBoxColliderWorldPolygon(
                    box);

            float area =
                PolygonArea(
                    world);

            if (area > 0.000001f)
            {
                polygons.Add(
                    world);

                totalArea +=
                    area;
            }

            return;
        }

        if (col is PolygonCollider2D polygon)
        {
            int pathCount =
                polygon.pathCount;

            for (int pathIndex = 0;
                 pathIndex < pathCount;
                 pathIndex++)
            {
                Vector2[] localPath =
                    polygon.GetPath(
                        pathIndex);

                if (localPath == null ||
                    localPath.Length < 3)
                {
                    continue;
                }

                Vector2[] world =
                    new Vector2[
                        localPath.Length];

                for (int i = 0;
                     i < localPath.Length;
                     i++)
                {
                    Vector2 local =
                        localPath[i] +
                        polygon.offset;

                    world[i] =
                        polygon.transform.TransformPoint(
                            local);
                }

                float area =
                    PolygonArea(
                        world);

                if (area <= 0.000001f)
                    continue;

                polygons.Add(
                    world);

                totalArea +=
                    area;
            }
        }

        // Structural authoring currently uses BoxCollider2D. Other Collider2D
        // shapes intentionally contribute nothing here until they have an exact
        // polygon conversion rather than a misleading world-bounds rectangle.
    }

    private static Vector2[] BuildBoxColliderWorldPolygon(
        BoxCollider2D box)
    {
        Vector2 half =
            box.size *
            0.5f;

        Vector2 offset =
            box.offset;

        Vector2[] local =
        {
            offset +
            new Vector2(
                -half.x,
                -half.y),

            offset +
            new Vector2(
                -half.x,
                 half.y),

            offset +
            new Vector2(
                 half.x,
                 half.y),

            offset +
            new Vector2(
                 half.x,
                -half.y)
        };

        Vector2[] world =
            new Vector2[
                local.Length];

        for (int i = 0;
             i < local.Length;
             i++)
        {
            world[i] =
                box.transform.TransformPoint(
                    local[i]);
        }

        return world;
    }

    private Vector2[] BuildFallbackWorldRectangle(
        IForceBody forceBody)
    {
        Vector2 localCenter =
            Vector2.zero;

        if (bodySource is Boat boat)
        {
            localCenter =
                boat.GeometryLocalCenter;
        }

        Vector2[] localPoly =
        {
            new Vector2(
                localCenter.x -
                    forceBody.Width *
                    0.5f,
                localCenter.y -
                    forceBody.Height *
                    0.5f),

            new Vector2(
                localCenter.x -
                    forceBody.Width *
                    0.5f,
                localCenter.y +
                    forceBody.Height *
                    0.5f),

            new Vector2(
                localCenter.x +
                    forceBody.Width *
                    0.5f,
                localCenter.y +
                    forceBody.Height *
                    0.5f),

            new Vector2(
                localCenter.x +
                    forceBody.Width *
                    0.5f,
                localCenter.y -
                    forceBody.Height *
                    0.5f)
        };

        Vector2[] worldPoly =
            new Vector2[
                localPoly.Length];

        for (int i = 0;
             i < localPoly.Length;
             i++)
        {
            worldPoly[i] =
                LocalToWorld(
                    forceBody,
                    localPoly[i]);
        }

        return worldPoly;
    }

    private List<Vector2> ClipPolygonWithWave(Vector2[] polygon)
    {
        List<Vector2> output = new List<Vector2>();
        int n = polygon.Length;

        for (int i = 0; i < n; i++)
        {
            Vector2 curr = polygon[i];
            Vector2 next = polygon[(i + 1) % n];

            float surfaceCurr =
                SampleActiveSurfaceY(
                    curr.x);

            float surfaceNext =
                SampleActiveSurfaceY(
                    next.x);

            // Signed distance from each polygon endpoint to the sampled water
            // surface. Negative/zero means submerged.
            float currDistance =
                curr.y -
                surfaceCurr;

            float nextDistance =
                next.y -
                surfaceNext;

            bool currSubmerged =
                currDistance <= 0f;

            bool nextSubmerged =
                nextDistance <= 0f;

            if (currSubmerged)
                output.Add(curr);

            if (currSubmerged != nextSubmerged)
            {
                // Treat the sampled surface between the two endpoints as
                // locally linear. The previous calculation used surfaceCurr
                // for both ends, which is only correct for perfectly flat water.
                float denom =
                    currDistance -
                    nextDistance;

                float t =
                    Mathf.Abs(denom) >
                    0.000001f
                        ? currDistance /
                          denom
                        : 0f;

                t =
                    Mathf.Clamp01(
                        t);

                Vector2 intersect =
                    curr +
                    t *
                    (next - curr);

                output.Add(
                    intersect);
            }
        }

        return output;
    }

    List<Vector2> ClipPolygonByPlane(
    List<Vector2> poly,
    Vector2 origin,
    Vector2 axis,
    float bound,
    bool keepAbove)
    {
        List<Vector2> output = new List<Vector2>();
        int count = poly.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % count];

            float da = Vector2.Dot(a - origin, axis) - bound;
            float db = Vector2.Dot(b - origin, axis) - bound;

            bool aInside = keepAbove ? da >= 0f : da <= 0f;
            bool bInside = keepAbove ? db >= 0f : db <= 0f;

            if (aInside) output.Add(a);

            if (aInside != bInside)
            {
                float t = da / (da - db);
                output.Add(Vector2.Lerp(a, b, t));
            }
        }

        return output;
    }

    float PolygonArea(IReadOnlyList<Vector2> poly)
    {
        if (poly == null ||
            poly.Count < 3)
        {
            return 0f;
        }

        float area = 0f;
        int count = poly.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % count];
            area += a.x * b.y - b.x * a.y;
        }

        return Mathf.Abs(area) * 0.5f;
    }

    Vector2 PolygonCentroid(
        List<Vector2> poly,
        float area)
    {
        if (poly == null ||
            poly.Count == 0)
        {
            return Vector2.zero;
        }

        float weightedX = 0f;
        float weightedY = 0f;
        float crossSum = 0f;
        int count = poly.Count;

        for (int i = 0;
             i < count;
             i++)
        {
            Vector2 a =
                poly[i];

            Vector2 b =
                poly[(i + 1) %
                     count];

            float cross =
                a.x * b.y -
                b.x * a.y;

            crossSum +=
                cross;

            weightedX +=
                (a.x + b.x) *
                cross;

            weightedY +=
                (a.y + b.y) *
                cross;
        }

        // Standard polygon centroid denominator:
        // 6 * signedArea == 3 * crossSum.
        //
        // Using the signed cross sum means this works for BOTH clockwise and
        // counter-clockwise polygons. The old code used absolute area and then
        // compensated at call sites with a unary minus, which was fragile.
        float denom =
            3f *
            crossSum;

        if (Mathf.Abs(denom) <
            0.00001f)
        {
            return poly[0];
        }

        return new Vector2(
            weightedX / denom,
            weightedY / denom);
    }

    Vector2 LocalToWorld(
        IForceBody body,
        Vector2 local)
    {
        if (body == null)
            return local;

        if (body.rb != null)
        {
            // Authoritative geometry is stored in body-local coordinates.
            // TransformPoint applies translation, rotation, AND scale exactly
            // once. The previous hand-written conversion ignored scale, which
            // caused the buoyancy hull/slices to diverge from scaled boats.
            return
                body.rb.transform.TransformPoint(
                    local);
        }

        return
            body.Position +
            local;
    }

    List<Vector2> ClipPolygonByY(
        List<Vector2> poly,
        float y,
        bool keepAbove)
    {
        List<Vector2> output = new List<Vector2>();
        int count = poly.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % count];

            bool aIn = keepAbove ? a.y >= y : a.y <= y;
            bool bIn = keepAbove ? b.y >= y : b.y <= y;

            if (aIn) output.Add(a);

            if (aIn != bIn)
            {
                float t = (y - a.y) / (b.y - a.y);
                output.Add(Vector2.Lerp(a, b, t));
            }
        }

        return output;
    }

    List<Vector2> ClipPolygonBetweenXPlanes(
    List<Vector2> poly,
    float minX,
    float maxX)
    {
        poly = ClipPolygonByX(poly, minX, true);
        poly = ClipPolygonByX(poly, maxX, false);
        return poly;
    }

    List<Vector2> ClipPolygonByX(
        List<Vector2> poly,
        float x,
        bool keepRight)
    {
        List<Vector2> output = new List<Vector2>();
        int count = poly.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % count];

            bool aIn = keepRight ? a.x >= x : a.x <= x;
            bool bIn = keepRight ? b.x >= x : b.x <= x;

            if (aIn) output.Add(a);

            if (aIn != bIn)
            {
                float t = (x - a.x) / (b.x - a.x);
                output.Add(Vector2.Lerp(a, b, t));
            }
        }

        return output;
    }

    private void ResolveWaveRefs()
    {
        if (waveManager == null)
            waveManager = FindFirstObjectByType<WaveManager>();

        if (wave == null)
            wave = FindFirstObjectByType<WaveField>();
    }

    private float SampleActiveSurfaceY(float worldX)
    {
        if (_activeExposure.UsesFlatSurface)
            return _activeExposure.FlatSurfaceY;

        if (waveManager != null)
            return waveManager.SampleSurfaceY(worldX);

        return 0f;
    }

    private float SampleActiveSurfaceVelocity(float worldX)
    {
        if (!_activeExposure.UsesOceanSurface)
            return 0f;

        if (waveManager != null)
            return waveManager.SampleSurfaceVelocity(worldX);

        return 0f;
    }

    private bool TryResolveWaterExposure(IForceBody forceBody, out BoatWaterExposure exposure)
    {
        exposure = default;

        if (forceBody == null)
            return false;

        if (!useBoatWaterContext)
            return TryResolveOceanExposure(out exposure);

        // The boat hull itself should continue using ocean buoyancy.
        // The resolver is for players/items/cargo inside/around the boat.
        if (boatBodyAlwaysUsesOcean && bodySource is Boat)
            return TryResolveOceanExposure(out exposure);

        BoatWaterContextResolver resolver = explicitWaterContext;

        if (resolver == null)
            resolver = ResolveWaterContextForSubject();

        if (resolver != null && resolver.TryResolveAtPoint(forceBody.Position, out exposure))
            return true;

        return TryResolveOceanExposure(out exposure);
    }

    private bool TryResolveOceanExposure(out BoatWaterExposure exposure)
    {
        ResolveWaveRefs();

        if (waveManager == null)
        {
            exposure = default;
            return false;
        }

        exposure = BoatWaterExposure.Ocean();
        return true;
    }

    private BoatWaterContextResolver ResolveWaterContextForSubject()
    {
        if (bodySource == null)
            return null;

        // Player path: multiplayer-safe because it uses that player's boarded boat root.
        PlayerBoardingState boarding =
            bodySource.GetComponentInParent<PlayerBoardingState>() ??
            bodySource.GetComponentInChildren<PlayerBoardingState>(true);

        if (boarding != null && boarding.IsBoarded && boarding.CurrentBoatRoot != null)
        {
            BoatWaterContextResolver resolver =
                boarding.CurrentBoatRoot.GetComponent<BoatWaterContextResolver>() ??
                boarding.CurrentBoatRoot.GetComponentInChildren<BoatWaterContextResolver>(true);

            if (resolver != null)
                return resolver;
        }

        // Parent path: works for things actually under the boat hierarchy.
        BoatWaterContextResolver parentResolver = bodySource.GetComponentInParent<BoatWaterContextResolver>();
        if (parentResolver != null)
            return parentResolver;

        // Boat-owned item path: works even if loose boat-owned items are not transform-parented.
        BoatOwnedItem owned =
            bodySource.GetComponentInParent<BoatOwnedItem>() ??
            bodySource.GetComponentInChildren<BoatOwnedItem>(true);

        if (owned != null && owned.IsOwnedByBoat)
        {
            BoatWaterContextResolver[] resolvers =
                FindObjectsByType<BoatWaterContextResolver>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < resolvers.Length; i++)
            {
                BoatWaterContextResolver r = resolvers[i];
                if (r == null)
                    continue;

                if (string.Equals(r.BoatInstanceId, owned.OwningBoatInstanceId, System.StringComparison.Ordinal))
                    return r;
            }
        }

        return null;
    }


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (bodySource == null)
            return;

        IForceBody forceBody =
            ResolveAuthoritativeBodySource();

        if (forceBody == null ||
            forceBody.rb == null)
        {
            return;
        }

        ResolveWaveRefs();

        if (waveManager == null)
            return;

        // Gizmos always visualize ocean exposure. Boat hull buoyancy is ocean
        // authoritative, while simple items keep their normal water-context
        // behavior at runtime.
        _activeExposure =
            BoatWaterExposure.Ocean();

        List<Vector2[]> contributorPolygons =
            BuildWorldBuoyancyContributorPolygons(
                forceBody,
                out _,
                out _);

        if (contributorPolygons.Count == 0)
            return;

        Color contributorOutline =
            new Color(
                0.02f,
                0.10f,
                0.38f,
                1f);

        Color submergedOutline =
            new Color(
                0.05f,
                0.32f,
                0.72f,
                1f);

        Color sliceOutline =
            new Color(
                0.01f,
                0.07f,
                0.30f,
                1f);

        Color sliceFill =
            new Color(
                0.01f,
                0.09f,
                0.42f,
                0.22f);

        Color centroidColor =
            new Color(
                0.10f,
                0.78f,
                1f,
                1f);

        float drawMinX =
            float.PositiveInfinity;

        float drawMaxX =
            float.NegativeInfinity;

        for (int contributorIndex = 0;
             contributorIndex < contributorPolygons.Count;
             contributorIndex++)
        {
            Vector2[] worldPoly =
                contributorPolygons[
                    contributorIndex];

            if (worldPoly == null ||
                worldPoly.Length < 3)
            {
                continue;
            }

            Gizmos.color =
                contributorOutline;

            for (int i = 0;
                 i < worldPoly.Length;
                 i++)
            {
                Vector2 a =
                    worldPoly[i];

                Vector2 b =
                    worldPoly[
                        (i + 1) %
                        worldPoly.Length];

                Gizmos.DrawLine(
                    a,
                    b);

                drawMinX =
                    Mathf.Min(
                        drawMinX,
                        a.x);

                drawMaxX =
                    Mathf.Max(
                        drawMaxX,
                        a.x);
            }

            List<Vector2> submergedPoly =
                ClipPolygonWithWave(
                    worldPoly);

            if (submergedPoly.Count < 3)
                continue;

            Gizmos.color =
                submergedOutline;

            for (int i = 0;
                 i < submergedPoly.Count;
                 i++)
            {
                Gizmos.DrawLine(
                    submergedPoly[i],
                    submergedPoly[
                        (i + 1) %
                        submergedPoly.Count]);
            }

            float minX =
                float.MaxValue;

            float maxX =
                float.MinValue;

            foreach (Vector2 pt in submergedPoly)
            {
                minX =
                    Mathf.Min(
                        minX,
                        pt.x);

                maxX =
                    Mathf.Max(
                        maxX,
                        pt.x);
            }

            float submergedWidth =
                maxX -
                minX;

            if (submergedWidth <= 0.000001f)
                continue;

            float sliceWidth =
                submergedWidth /
                Mathf.Max(
                    sliceCount,
                    1);

            for (int i = 0;
                 i < sliceCount;
                 i++)
            {
                float xLeft =
                    minX +
                    i *
                    sliceWidth;

                float xRight =
                    xLeft +
                    sliceWidth;

                List<Vector2> slicePoly =
                    ClipPolygonBetweenXPlanes(
                        submergedPoly,
                        xLeft,
                        xRight);

                if (slicePoly.Count < 3)
                    continue;

                Vector3[] fillPoints =
                    new Vector3[
                        slicePoly.Count];

                for (int j = 0;
                     j < slicePoly.Count;
                     j++)
                {
                    fillPoints[j] =
                        slicePoly[j];
                }

                UnityEditor.Handles.color =
                    sliceFill;

                UnityEditor.Handles.DrawAAConvexPolygon(
                    fillPoints);

                Gizmos.color =
                    sliceOutline;

                for (int j = 0;
                     j < slicePoly.Count;
                     j++)
                {
                    Gizmos.DrawLine(
                        slicePoly[j],
                        slicePoly[
                            (j + 1) %
                            slicePoly.Count]);
                }

                float area =
                    PolygonArea(
                        slicePoly);

                if (area > 0f)
                {
                    Vector2 centroid =
                        PolygonCentroid(
                            slicePoly,
                            area);

                    Gizmos.color =
                        centroidColor;

                    Gizmos.DrawSphere(
                        centroid,
                        0.05f);
                }
            }
        }

        if (float.IsNaN(drawMinX) ||
            float.IsNaN(drawMaxX) ||
            float.IsInfinity(drawMinX) ||
            float.IsInfinity(drawMaxX) ||
            drawMaxX <= drawMinX)
        {
            return;
        }

        // --- Draw water surface reference ---
        Gizmos.color =
            Color.red;

        const int steps =
            24;

        Vector2 prev =
            new Vector2(
                drawMinX,
                waveManager.SampleSurfaceY(
                    drawMinX));

        for (int i = 1;
             i <= steps;
             i++)
        {
            float x =
                Mathf.Lerp(
                    drawMinX,
                    drawMaxX,
                    i /
                    (float)steps);

            Vector2 curr =
                new Vector2(
                    x,
                    waveManager.SampleSurfaceY(
                        x));

            Gizmos.DrawLine(
                prev,
                curr);

            prev =
                curr;
        }
    }
#endif

}
