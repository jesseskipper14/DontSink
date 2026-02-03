using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a floodable opening between two compartments.
/// Geometry is defined in world space.
/// </summary>
[System.Serializable]
public class CompartmentConnection
{
    // ========================
    // Topology
    // ========================

    public Compartment A;
    public Compartment B;
    public Transform transform;
    public FlowVisual flowVisual;

    public bool isOpen = true;

    // ========================
    // Geometry (World Space)
    // ========================

    [Tooltip("Opening left X in local space of the reference transform")]
    public float openingLeftX;

    [Tooltip("Opening right X in local space of the reference transform")]
    public float openingRightX;

    [Tooltip("Opening bottom Y in local space of the reference transform")]
    public float openingBottomY;

    [Tooltip("Opening top Y in local space of the reference transform")]
    public float openingTopY;
    public float LeftXWorld
    {
        get
        {
            Vector3 local = new Vector3(openingLeftX, 0f, 0f);
            return transform.TransformPoint(local).x;
        }
    }

    public float RightXWorld
    {
        get
        {
            Vector3 local = new Vector3(openingRightX, 0f, 0f);
            return transform.TransformPoint(local).x;
        }
    }

    public float BottomYWorld
    {
        get
        {
            Vector3 local = new Vector3(0f, openingBottomY, 0f);
            return transform.TransformPoint(local).y;
        }
    }

    public float TopYWorld
    {
        get
        {
            Vector3 local = new Vector3(0f, openingTopY, 0f);
            return transform.TransformPoint(local).y;
        }
    }

    // ========================
    // Flow Parameters
    // ========================

    [Header("Flow")]
    [Tooltip("Base flow coefficient (volume per second per unit height)")]
    public float flowCoefficient = 1f;

    // ========================
    // Derived
    // ========================

    public float Width => Mathf.Abs(openingRightX - openingLeftX);
    public float Height => Mathf.Max(0f, openingTopY - openingBottomY);

    // ========================
    // Utility
    // ========================

    public bool Connects(Compartment c)
    {
        return c == A || c == B;
    }

    public Compartment Other(Compartment c)
    {
        if (c == A) return B;
        if (c == B) return A;
        return null;
    }

    /// <summary>
    /// Returns the vertical overlap between a compartment's water surface
    /// and the opening.
    /// </summary>
    public float GetSubmergedHeight(float waterSurfaceY)
    {
        if (waterSurfaceY <= BottomYWorld)
            return 0f;

        return Mathf.Clamp(waterSurfaceY - BottomYWorld, 0f, Height);
    }

    public void GetBottomEdgeWorld(out Vector2 left, out Vector2 right)
    {
        Vector3 blLocal = new Vector3(openingLeftX, openingBottomY, 0f);
        Vector3 brLocal = new Vector3(openingRightX, openingBottomY, 0f);

        left = transform.TransformPoint(blLocal);
        right = transform.TransformPoint(brLocal);
    }

    public void EnsureFlowVisualInstance()
    {
        if (flowVisual == null)
            return; // nothing assigned

        // If it’s a prefab (not in scene), instantiate it
        if (flowVisual.gameObject.scene.rootCount == 0)
        {
            flowVisual = Object.Instantiate(flowVisual);
        }
    }

#if UNITY_EDITOR
    public void DrawGizmos()
    {
        if (transform == null)
            return;

        // =========================================================
        // GIZMO 1 — Opening Rectangle (World Space)
        // Draws the physical opening bounds
        // =========================================================

        Gizmos.color = isOpen ? Color.cyan : Color.red;

        Vector3 blLocal = new Vector3(openingLeftX, openingBottomY, 0f);
        Vector3 brLocal = new Vector3(openingRightX, openingBottomY, 0f);
        Vector3 tlLocal = new Vector3(openingLeftX, openingTopY, 0f);
        Vector3 trLocal = new Vector3(openingRightX, openingTopY, 0f);

        Vector3 bl = transform.TransformPoint(blLocal);
        Vector3 br = transform.TransformPoint(brLocal);
        Vector3 tl = transform.TransformPoint(tlLocal);
        Vector3 tr = transform.TransformPoint(trLocal);

        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);

        if (!isOpen || A == null || B == null)
            return;

        // =========================================================
        // GIZMO 2 — Bottom Edge Intersection Debug
        // Visualizes the lowest intersection point between:
        // - the opening bottom edge
        // - the source compartment polygon
        // =========================================================

        //DrawIntersectingBottomGizmo();
    }

    public void DrawIntersectingBottomGizmo()
    {
        if (transform == null || A == null)
            return;

        // =========================================================
        // GIZMO 2A — Bottom Edge of Opening (World Space)
        // =========================================================

        Vector2 blWorld = transform.TransformPoint(
            new Vector3(openingLeftX, openingBottomY, 0f));

        Vector2 brWorld = transform.TransformPoint(
            new Vector3(openingRightX, openingBottomY, 0f));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(blWorld, brWorld);

        // =========================================================
        // GIZMO 2B — Intersection Candidates
        // - Endpoints inside compartment
        // - Edge intersections
        // =========================================================

        Vector2[] compPoly = A.GetWorldCorners();
        List<Vector2> candidates = new();

        if (ConvexPolygonUtil.PointInsideConvex(blWorld, compPoly))
            candidates.Add(blWorld);

        if (ConvexPolygonUtil.PointInsideConvex(blWorld, compPoly))
            candidates.Add(brWorld);

        for (int i = 0; i < compPoly.Length; i++)
        {
            Vector2 a = compPoly[i];
            Vector2 b = compPoly[(i + 1) % compPoly.Length];

            if (ConvexPolygonUtil.LineSegmentsIntersect(blWorld, brWorld, a, b, out Vector2 hit)) 
                candidates.Add(hit);
        }

        if (candidates.Count == 0)
            return;

        // =========================================================
        // GIZMO 2C — Lowest Point Along Gravity
        // Used as effective bottom of the opening
        // =========================================================

        Vector2 gravityDir = -Physics2D.gravity.normalized;

        Vector2 lowest = candidates[0];
        float minD = Vector2.Dot(lowest, gravityDir);

        foreach (var p in candidates)
        {
            float d = Vector2.Dot(p, gravityDir);
            if (d < minD)
            {
                minD = d;
                lowest = p;
            }
        }

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(lowest, 0.16f);

        // =========================================================
        // GIZMO 2D — Debug Rays to Lowest Point
        // =========================================================

        Gizmos.color = Color.magenta;
        foreach (var p in candidates)
            Gizmos.DrawLine(p, lowest);
    }
#endif
}