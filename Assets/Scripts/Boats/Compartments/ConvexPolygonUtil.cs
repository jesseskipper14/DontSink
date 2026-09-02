using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared geometry utilities for convex polygons and line segments.
/// All methods are allocation-free except where lists are returned.
/// </summary>
public static class ConvexPolygonUtil
{
    // =========================================================
    // Convex polygon clipping against a plane
    // Plane defined as: dot(p, normal) - offset <= 0
    // =========================================================

    public static List<Vector2> ClipBelowPlane(
        Vector2[] poly,
        Vector2 planeNormal,
        float planeOffset)
    {
        List<Vector2> clipped = new();

        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Length];

            float da = Vector2.Dot(a, planeNormal) - planeOffset;
            float db = Vector2.Dot(b, planeNormal) - planeOffset;

            bool aInside = da <= 0f;
            bool bInside = db <= 0f;

            if (aInside)
                clipped.Add(a);

            if (aInside ^ bInside)
            {
                float t = da / (da - db);
                clipped.Add(Vector2.Lerp(a, b, t));
            }
        }

        return clipped;
    }

    // =========================================================
    // Convex polygon area (assumes planar, ordered vertices)
    // =========================================================

    public static float PolygonArea(IReadOnlyList<Vector2> pts)
    {
        if (pts.Count < 3)
            return 0f;

        float area = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % pts.Count];
            area += (a.x * b.y - b.x * a.y);
        }

        return Mathf.Abs(area) * 0.5f;
    }

    // =========================================================
    // Convex point-in-polygon test (CW winding)
    // =========================================================

    public static bool PointInsideConvex(Vector2 point, Vector2[] poly)
    {
        bool inside = true;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Length];

            float cross =
                (b.x - a.x) * (point.y - a.y) -
                (b.y - a.y) * (point.x - a.x);

            if (cross < 0f)
            {
                inside = false;
                break;
            }
        }

        return inside;
    }


    // =========================================================
    // Line segment intersection (2D)
    // =========================================================

    public static bool LineSegmentsIntersect(
        Vector2 p1, Vector2 p2,
        Vector2 q1, Vector2 q2,
        out Vector2 intersection)
    {
        intersection = Vector2.zero;

        Vector2 r = p2 - p1;
        Vector2 s = q2 - q1;

        float rxs = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(rxs) < 1e-6f)
            return false;

        Vector2 qp = q1 - p1;

        float t = (qp.x * s.y - qp.y * s.x) / rxs;
        float u = (qp.x * r.y - qp.y * r.x) / rxs;

        if (t >= 0f && t <= 1f && u >= 0f && u <= 1f)
        {
            intersection = p1 + t * r;
            return true;
        }

        return false;
    }
}
