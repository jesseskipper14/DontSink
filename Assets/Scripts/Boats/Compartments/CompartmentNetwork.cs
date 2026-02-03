using System.Collections.Generic;
using UnityEngine;

public static class CompartmentNetwork
{
    static Vector2 PlaneNormal =>
        -Physics2D.gravity.normalized;

    static readonly bool DEBUG_FLOW = false;

    // =========================================================
    // PUBLIC ENTRY POINT
    // =========================================================

    public static void EqualizeNetwork(
        IEnumerable<Compartment> compartments,
        float deltaTime,
        int maxIterations = 8)
    {
        if (deltaTime <= 0f)
            return;

        Vector2 planeNormal = PlaneNormal;
        List<Compartment> comps = new(compartments);

        for (int iter = 0; iter < maxIterations; iter++)
        {
            bool anyFlow = false;

            Dictionary<Compartment, float> surfaceCache = new();

            float GetSurface(Compartment c)
            {
                if (!surfaceCache.TryGetValue(c, out float s))
                {
                    s = c.WaterSurfaceOffset;
                    surfaceCache[c] = s;
                }
                return s;
            }

            HashSet<CompartmentConnection> visited = new();

            foreach (var c in comps)
            {
                foreach (var conn in c.connections)
                {
                    if (!visited.Add(conn))
                        continue;

                    if (!conn.isOpen || conn.A == null || conn.B == null)
                        continue;

                    if (ProcessConnection(conn, GetSurface, planeNormal, deltaTime))
                        anyFlow = true;
                }
            }

            if (!anyFlow)
                break;
        }
    }

    // =========================================================
    // CORE CONNECTION SOLVER
    // =========================================================

    static bool ProcessConnection(
    CompartmentConnection conn,
    System.Func<Compartment, float> getSurface,
    Vector2 planeNormal,
    float deltaTime)
    {
        // ---------------------------------------------------------
        // Basic setup & debug context
        // ---------------------------------------------------------
        // Each connection is processed once per iteration.
        // We treat flow symmetrically and decide direction dynamically
        // based on current water surface heights.
        // ---------------------------------------------------------

        Compartment A = conn.A;
        Compartment B = conn.B;

        string tag = $"[Flow {A.name} ↔ {B.name}]";

        // Cached water surface offsets along gravity for this iteration
        float surfaceA = getSurface(A);
        float surfaceB = getSurface(B);

        Log($"{tag} Surface A={surfaceA:F4}, B={surfaceB:F4}");

        // ---------------------------------------------------------
        // Determine source and target compartments
        // ---------------------------------------------------------
        // The compartment with the higher water surface (along gravity)
        // becomes the source. Equal surfaces mean no driving head.
        // ---------------------------------------------------------

        if (!TryGetFlowDirection(
                A, B,
                surfaceA, surfaceB,
                out Compartment source,
                out Compartment target,
                out float sourceSurface,
                out float targetSurface,
                tag))
            return false;

        // ---------------------------------------------------------
        // Build world-space geometry for overlap & hydraulic checks
        // ---------------------------------------------------------
        // connectionPolyWorld:
        //   The full rectangular opening polygon in world space.
        //
        // sourcePolyWorld:
        //   The polygon of water currently occupying the source compartment.
        //
        // sourcePoly:
        //   The compartment hull polygon (used for geometric containment).
        // ---------------------------------------------------------

        Vector2[] connectionPolyWorld = BuildConnectionPolygonWorld(conn);
        Vector2[] sourcePolyWorld = source.GetWaterPolygonWorld();
        Vector2[] sourcePoly = source.GetWorldCorners();

        // ---------------------------------------------------------
        // Hydraulic contact validation
        // ---------------------------------------------------------
        // If the source water polygon does not overlap the opening polygon
        // at all, there is no possible physical contact → no flow.
        // This prevents flow through openings that are geometrically dry.
        // ---------------------------------------------------------

        if (!ConvexPolygonsOverlap(sourcePolyWorld, connectionPolyWorld))
        {
            Warn($"{tag} No polygon overlap between source water and connection opening - flow rejected");
            return false;
        }

        // ---------------------------------------------------------
        // Compute the effective hydraulic sill of the opening
        // ---------------------------------------------------------
        // This determines the *lowest point of the opening along gravity*
        // that is in contact with the source compartment.
        //
        // IMPORTANT:
        // - No local-space "top" or "bottom" assumptions
        // - Entire opening polygon is considered
        // - Fully orientation- and gravity-invariant
        // ---------------------------------------------------------

        if (!TryComputeConnectionSill(
                connectionPolyWorld,
                sourcePoly,
                planeNormal,
                tag,
                out float connBottom,
                out Vector2 lowestPoint))
            return false;

        Log($"{tag} Effective connection bottom = {connBottom:F4} at {lowestPoint}");

        // ---------------------------------------------------------
        // Eligibility check: is the opening submerged at all?
        // ---------------------------------------------------------
        // If the source water surface is at or below the sill,
        // the opening is not hydraulically active.
        // ---------------------------------------------------------

        if (sourceSurface <= connBottom)
        {
            Log($"{tag} BLOCKED: source surface ({sourceSurface:F4}) <= conn bottom ({connBottom:F4})");
            return false;
        }

        // ---------------------------------------------------------
        // Compute submerged opening depth (along gravity)
        // ---------------------------------------------------------
        // This clamps the effective flow area based on how much of
        // the opening is actually below the source water surface.
        //
        // This calculation:
        // - Projects the opening polygon onto gravity
        // - Computes its gravity-aligned depth
        // - Clamps submergence accordingly
        // ---------------------------------------------------------

        float submerged = ComputeSubmergedHeight(
            connectionPolyWorld,
            planeNormal,
            sourceSurface,
            tag);

        if (submerged <= 0f)
        {
            Log($"{tag} BLOCKED: submerged height <= 0");
            return false;
        }

        // ---------------------------------------------------------
        // Flow calculation
        // ---------------------------------------------------------
        // Flow is proportional to:
        // - driving head (surface difference)
        // - submerged opening depth
        // - opening width
        // - flow coefficient
        // - delta time
        //
        // This is a simple linearized hydraulic model
        // (stable and gameplay-friendly).
        // ---------------------------------------------------------

        float flow = ComputeFlow(
            sourceSurface,
            targetSurface,
            submerged,
            conn,
            deltaTime,
            tag);

        if (flow <= 0f)
        {
            Log($"{tag} BLOCKED: flow <= 0");
            return false;
        }

        // ---------------------------------------------------------
        // Clamp by available water in the source
        // ---------------------------------------------------------
        // Water is tracked as area (2D), so we cannot move
        // more than the source currently contains.
        // ---------------------------------------------------------

        flow = Mathf.Min(flow, source.WaterArea);

        if (flow <= 0f)
        {
            Log($"{tag} BLOCKED: source has no water");
            return false;
        }

        // ---------------------------------------------------------
        // Apply flow
        // ---------------------------------------------------------
        // Water is removed from the source and offered to the target.
        // The target may reject some water due to capacity constraints,
        // in which case it is returned to the source.
        // ---------------------------------------------------------

        float accepted = ApplyFlow(source, target, flow, tag);

        if (accepted > 0f)
        {
            //conn.EnsureFlowVisualInstance(); // TO DO - fix this shit
            //FlowVisualizer.ReportAcceptedFlow(conn, source, target, accepted); // TO DO - fix this shit
        }

        return true;
    }

    // =========================================================
    // HELPERS (LOGIC UNCHANGED)
    // =========================================================

    static bool TryGetFlowDirection(
        Compartment A,
        Compartment B,
        float surfaceA,
        float surfaceB,
        out Compartment source,
        out Compartment target,
        out float sourceSurface,
        out float targetSurface,
        string tag)
    {
        A.RecomputeWaterSurface();
        B.RecomputeWaterSurface();

        if (Mathf.Approximately(surfaceA, surfaceB))
        {
            Log($"{tag} No head (equal surfaces)");
            source = target = null;
            sourceSurface = targetSurface = 0f;
            return false;
        }

        if (surfaceA > surfaceB)
        {
            source = A;
            target = B;
            sourceSurface = surfaceA;
            targetSurface = surfaceB;
        }
        else
        {
            source = B;
            target = A;
            sourceSurface = surfaceB;
            targetSurface = surfaceA;
        }

        Log($"{tag} Source={source.name}, Target={target.name}, Head={(sourceSurface - targetSurface):F4}");
        return true;
    }

    static Vector2[] BuildConnectionPolygonWorld(CompartmentConnection conn)
    {
        return new Vector2[]
        {
            conn.transform.TransformPoint(new Vector3(conn.openingLeftX,  conn.openingTopY,    0f)),
            conn.transform.TransformPoint(new Vector3(conn.openingRightX, conn.openingTopY,    0f)),
            conn.transform.TransformPoint(new Vector3(conn.openingRightX, conn.openingBottomY, 0f)),
            conn.transform.TransformPoint(new Vector3(conn.openingLeftX,  conn.openingBottomY, 0f))
        };
    }

    static bool TryComputeConnectionSill(
        Vector2[] openingPoly,
        Vector2[] sourcePoly,
        Vector2 planeNormal,
        string tag,
        out float connBottom,
        out Vector2 lowestPoint)
    {
        List<Vector2> candidates = new();

        for (int i = 0; i < openingPoly.Length; i++)
        {
            if (ConvexPolygonUtil.PointInsideConvex(openingPoly[i], sourcePoly))
            {
                candidates.Add(openingPoly[i]);
                Log($"{tag} Opening corner {i} inside source at {openingPoly[i]}");
            }
        }

        for (int i = 0; i < sourcePoly.Length; i++)
        {
            Vector2 a = sourcePoly[i];
            Vector2 b = sourcePoly[(i + 1) % sourcePoly.Length];

            for (int j = 0; j < openingPoly.Length; j++)
            {
                Vector2 c = openingPoly[j];
                Vector2 d = openingPoly[(j + 1) % openingPoly.Length];

                if (ConvexPolygonUtil.LineSegmentsIntersect(a, b, c, d, out Vector2 hit))
                {
                    candidates.Add(hit);
                    Log($"{tag} Intersection source edge {i} with opening edge {j} at {hit}");
                }
            }
        }

        if (candidates.Count == 0)
        {
            float openingMin = float.PositiveInfinity;
            float openingMax = float.NegativeInfinity;

            foreach (var p in openingPoly)
            {
                float d = Vector2.Dot(p, planeNormal);
                openingMin = Mathf.Min(openingMin, d);
                openingMax = Mathf.Max(openingMax, d);
            }

            float sourceBottom = float.PositiveInfinity;
            float sourceTop = float.NegativeInfinity;

            foreach (var p in sourcePoly)
            {
                float d = Vector2.Dot(p, planeNormal);
                sourceBottom = Mathf.Min(sourceBottom, d);
                sourceTop = Mathf.Max(sourceTop, d);
            }

            Log($"{tag} Vertical fallback check:");
            Log($"{tag}   openingMin = {openingMin:F4}, openingMax = {openingMax:F4}");
            Log($"{tag}   sourceBottom = {sourceBottom:F4}, sourceTop = {sourceTop:F4}");

            bool overlapsPoly = !(openingMax < sourceBottom || openingMin > sourceTop);

            if (!overlapsPoly)
            {
                Warn($"{tag} Vertical-flow fallback REJECTED (no overlap)");
                connBottom = 0f;
                lowestPoint = Vector2.zero;
                return false;
            }

            candidates.Add(openingPoly[0]);
            candidates.Add(openingPoly[1]);
            Log($"{tag} Vertical-flow fallback ACCEPTED (overlap found)");
        }

        connBottom = Vector2.Dot(candidates[0], planeNormal);
        lowestPoint = candidates[0];

        for (int i = 1; i < candidates.Count; i++)
        {
            float d = Vector2.Dot(candidates[i], planeNormal);
            if (d < connBottom)
            {
                connBottom = d;
                lowestPoint = candidates[i];
            }
        }

        return true;
    }

    static float ComputeSubmergedHeight(
        Vector2[] openingPoly,
        Vector2 planeNormal,
        float sourceSurface,
        string tag)
    {
        float openingMin = float.PositiveInfinity;
        float openingMax = float.NegativeInfinity;

        foreach (var p in openingPoly)
        {
            float d = Vector2.Dot(p, planeNormal);
            openingMin = Mathf.Min(openingMin, d);
            openingMax = Mathf.Max(openingMax, d);
        }

        float openingDepth = openingMax - openingMin;

        float submerged =
            Mathf.Clamp(
                sourceSurface - openingMin,
                0f,
                openingDepth);

        Log($"{tag} Submerged height = {submerged:F4}");
        return submerged;
    }

    static float ComputeFlow(
        float sourceSurface,
        float targetSurface,
        float submerged,
        CompartmentConnection conn,
        float deltaTime,
        string tag)
    {
        float drivingHead = sourceSurface - targetSurface;

        float flow =
            drivingHead *
            submerged *
            conn.Width *
            conn.flowCoefficient *
            deltaTime;

        //Debug.Log(
        //    $"[FlowCalc] " +
        //    $"drivingHead={drivingHead:F4}, " +
        //    $"submerged={submerged:F4}, " +
        //    $"width={conn.Width:F4}, " +
        //    $"coeff={conn.flowCoefficient:F4}, " +
        //    $"dt={deltaTime:F4}, " +
        //    $"flow={flow:F6}");

        Log($"{tag} Raw flow = {flow:F6}");
        return flow;
    }

    static float ApplyFlow(
        Compartment source,
        Compartment target,
        float flow,
        string tag)
    {
        Log($"{tag} APPLY: {flow:F6} area from {source.name} → {target.name}");

        source.RemoveWater(flow);
        float accepted = target.AcceptWater(flow);
        float rejected = flow - accepted;

        if (rejected > 0f)
        {
            Warn($"{tag} Target rejected {rejected:F6}, returning to source");
            source.AcceptWater(rejected);
        }

        Log($"{tag} Accepted={accepted:F6}");

        return accepted;
    }

    // =========================================================
    // UTILITIES
    // =========================================================

    public static bool ConvexPolygonsOverlap(Vector2[] polyA, Vector2[] polyB)
    {
        foreach (var pt in polyA)
            if (ConvexPolygonUtil.PointInsideConvex(pt, polyB))
                return true;

        foreach (var pt in polyB)
            if (ConvexPolygonUtil.PointInsideConvex(pt, polyA))
                return true;

        for (int i = 0; i < polyA.Length; i++)
        {
            Vector2 a1 = polyA[i];
            Vector2 a2 = polyA[(i + 1) % polyA.Length];

            for (int j = 0; j < polyB.Length; j++)
            {
                Vector2 b1 = polyB[j];
                Vector2 b2 = polyB[(j + 1) % polyB.Length];

                if (ConvexPolygonUtil.LineSegmentsIntersect(a1, a2, b1, b2, out _))
                    return true;
            }
        }
        return false;
    }

    static void Log(string msg)
    {
        if (DEBUG_FLOW)
            Debug.Log(msg);
    }

    static void Warn(string msg)
    {
        if (DEBUG_FLOW)
            Debug.LogWarning(msg);
    }
}
