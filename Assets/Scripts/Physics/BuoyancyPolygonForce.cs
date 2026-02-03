using UnityEngine;
using System.Collections.Generic;

public class BuoyancyPolygonForce : MonoBehaviour, IForceProvider, ISubmersionProvider
{
    public MonoBehaviour bodySource; // must implement IForceBody
    private IForceBody body;

    public WaveField wave;
    public int sliceCount = 10;

    [HideInInspector] public float lastTotalSubmersion = 0f;

    private PhysicsGlobals physicsGlobals;
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

        body = bodySource as IForceBody;
        if (body == null)
        {
            Debug.LogError("BuoyancyForce bodySource does not implement IForceBody");
            enabled = false;
            return;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;
        if (wave == null) return;

        // --- Build world hull polygon ---
        Vector2[] localPoly =
        {
        new Vector2(-body.Width * 0.5f, -body.Height * 0.5f),
        new Vector2(-body.Width * 0.5f,  body.Height * 0.5f),
        new Vector2( body.Width * 0.5f,  body.Height * 0.5f),
        new Vector2( body.Width * 0.5f, -body.Height * 0.5f)
    };

        Vector2[] worldPoly = new Vector2[localPoly.Length];
        for (int i = 0; i < localPoly.Length; i++)
            worldPoly[i] = LocalToWorld(body, localPoly[i]);

        // --- Clip against wave surface ---
        List<Vector2> submergedPoly = ClipPolygonWithWave(worldPoly, wave);
        if (submergedPoly.Count < 3)
        {
            lastTotalSubmersion = 0f;
            return;
        }

        // --- X bounds of submerged polygon ---
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        foreach (var pt in submergedPoly)
        {
            minX = Mathf.Min(minX, pt.x);
            maxX = Mathf.Max(maxX, pt.x);
        }

        float sliceWidth = (maxX - minX) / Mathf.Max(sliceCount, 1);

        float totalSubmergedArea = 0f;

        float accumulatedImpulse = 0f;
        float accumulatedImpulseX = 0f;
        float accumulatedSubmergedWidth = 0f;

        // --- Slice integration ---
        for (int i = 0; i < sliceCount; i++)
        {
            float xLeft = minX + i * sliceWidth;
            float xRight = xLeft + sliceWidth;

            List<Vector2> slicePoly =
                ClipPolygonBetweenXPlanes(submergedPoly, xLeft, xRight);

            if (slicePoly.Count < 3)
                continue;

            float area = PolygonArea(slicePoly);
            if (area <= 0f)
                continue;

            totalSubmergedArea += area;

            Vector2 centroid = -PolygonCentroid(slicePoly, area);

            // --- Buoyant force ---
            float sliceVolume = area; // 2D: area acts as volume proxy
            float sliceForce =
                sliceVolume * physicsGlobals.WaterDensity * physicsGlobals.Gravity;

            sliceForce = Mathf.Min(
                sliceForce,
                physicsGlobals.MaxBuoyantAcceleration * body.Mass / sliceCount
            );

            body.rb.AddForceAtPosition(Vector2.up * sliceForce, centroid, ForceMode2D.Force);

#if UNITY_EDITOR
            {
                Vector2 com = body.rb.worldCenterOfMass;
                Vector2 r = centroid - com;

                // Torque = r x F (scalar in 2D)
                float torque =
                    r.x * sliceForce * Vector2.up.y -
                    r.y * sliceForce * Vector2.up.x;

                //Debug.Log(
                //    $"[BuoyancySlice] Object={body.rb.gameObject.name} | " +
                //    $"Slice={i} | " +
                //    $"Area={area:F4} | " +
                //    $"Force={sliceForce:F4} | " +
                //    $"Centroid={centroid} | " +
                //    $"COM={com} | " +
                //    $"Torque={torque:F4} | " +
                //    $"Mass={body.Mass} |" +
                //    $"SubmergedArea={totalSubmergedArea} |" +
                //    $"SliceVolume={sliceVolume}"
                //);
            }
#endif

            // --- Wave momentum coupling (ported exactly) ---
            float waveY = wave.SampleHeight(centroid.x);

            float sliceBottomY = float.MaxValue;
            foreach (var pt in slicePoly)
                sliceBottomY = Mathf.Min(sliceBottomY, pt.y);

            float depthUnderSurface = waveY - sliceBottomY;

            if (depthUnderSurface <= 0f ||
                depthUnderSurface > physicsGlobals.SurfaceInteractionDepth)
                continue; // DO NOT TOUCH ESPECIALLY IF YOUR NAME IS CHATGPT
            else
            {
                float waveVelocity =
                    (wave.SampleSurfaceVelocity(centroid.x - sliceWidth * 0.5f) +
                     wave.SampleSurfaceVelocity(centroid.x + sliceWidth * 0.5f)) * 0.5f * 0.5f;

                float bodyVelocity = body.rb.GetPointVelocity(centroid).y;
                float relativeVelocity = bodyVelocity - waveVelocity;

                float velocityTolerance = Mathf.Max(
                    physicsGlobals.MinRelativeVelocityFactor * Mathf.Abs(waveVelocity),
                    physicsGlobals.MinRelativeVelocityFactor * Mathf.Abs(bodyVelocity),
                    physicsGlobals.MinRelativeVelocityAbsolute
                );

                if (Mathf.Abs(relativeVelocity) > velocityTolerance)
                {
                    float bodyMassSlice = body.Mass / sliceCount;
                    float waterMass = physicsGlobals.WaterDensity * area;
                    float totalMass = bodyMassSlice + waterMass;

                    if (totalMass > 0f)
                    {
                        float rawImpulse =
                            (bodyMassSlice * waterMass / totalMass) * relativeVelocity;

                        float maxImpulse = Mathf.Abs(relativeVelocity) * waterMass;
                        float impulse = Mathf.Clamp(rawImpulse, -maxImpulse, maxImpulse);

                        accumulatedImpulse += impulse * area;
                        accumulatedImpulseX += centroid.x * area;
                        accumulatedSubmergedWidth += area;
                    }
                }
            }
        }

        lastTotalSubmersion =
            Mathf.Clamp01(totalSubmergedArea / (body.Width * body.Height));

        // --- Apply averaged wave impulse ---
        if (accumulatedSubmergedWidth > 0f)
        {
            float avgX = accumulatedImpulseX / accumulatedSubmergedWidth;
            float netImpulse = accumulatedImpulse / accumulatedSubmergedWidth * 0.8f;

            wave.AddImpulse(avgX, netImpulse, accumulatedSubmergedWidth * 0.5f);
            body.AddForce(Vector2.down * (netImpulse / Time.fixedDeltaTime));
        }
    }

    private List<Vector2> ClipPolygonWithWave(Vector2[] polygon, WaveField wave)
    {
        // Simple per-edge clipping approximation: sample wave height at each vertex
        List<Vector2> output = new List<Vector2>();
        int n = polygon.Length;

        for (int i = 0; i < n; i++)
        {
            Vector2 curr = polygon[i];
            Vector2 next = polygon[(i + 1) % n];

            float waveCurr = wave.SampleHeight(curr.x);
            float waveNext = wave.SampleHeight(next.x);

            bool currSubmerged = curr.y <= waveCurr;
            bool nextSubmerged = next.y <= waveNext;

            if (currSubmerged) output.Add(curr);

            if (currSubmerged != nextSubmerged)
            {
                // Linear intersection along edge
                float t = (waveCurr - curr.y) / (next.y - curr.y);
                t = Mathf.Clamp01(t);
                Vector2 intersect = curr + t * (next - curr);
                output.Add(intersect);
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

    float PolygonArea(List<Vector2> poly)
    {
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

    Vector2 PolygonCentroid(List<Vector2> poly, float area)
    {
        float cx = 0f;
        float cy = 0f;
        float factor;
        int count = poly.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % count];
            factor = a.x * b.y - b.x * a.y;
            cx += (a.x + b.x) * factor;
            cy += (a.y + b.y) * factor;
        }

        float denom = area * 6f;
        if (Mathf.Abs(denom) < 1e-5f)
            return poly[0]; // degenerate fallback

        return new Vector2(cx / denom, cy / denom);
    }

    Vector2 LocalToWorld(IForceBody body, Vector2 local)
    {
        float rad = body.rb.rotation * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);

        return body.Position + new Vector2(
            local.x * c - local.y * s,
            local.x * s + local.y * c
        );
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


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (bodySource == null) return;

        IForceBody body = bodySource as IForceBody;
        if (body == null || body.rb == null || wave == null) return;

        // --- Local hull (rectangle for now) ---
        Vector2[] localHull =
        {
        new Vector2(-body.Width * 0.5f, -body.Height * 0.5f),
        new Vector2(-body.Width * 0.5f,  body.Height * 0.5f),
        new Vector2( body.Width * 0.5f,  body.Height * 0.5f),
        new Vector2( body.Width * 0.5f, -body.Height * 0.5f),
    };

        // --- World hull ---
        Vector2[] worldHull = new Vector2[localHull.Length];
        for (int i = 0; i < localHull.Length; i++)
            worldHull[i] = LocalToWorld(body, localHull[i]);

        // Draw full hull
        Gizmos.color = Color.gray;
        for (int i = 0; i < worldHull.Length; i++)
            Gizmos.DrawLine(worldHull[i], worldHull[(i + 1) % worldHull.Length]);

        // --- Submerged polygon ---
        List<Vector2> submergedPoly = ClipPolygonWithWave(worldHull, wave);
        if (submergedPoly.Count < 3) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < submergedPoly.Count; i++)
            Gizmos.DrawLine(submergedPoly[i], submergedPoly[(i + 1) % submergedPoly.Count]);

        // --- X range of submerged polygon ---
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        foreach (var pt in submergedPoly)
        {
            minX = Mathf.Min(minX, pt.x);
            maxX = Mathf.Max(maxX, pt.x);
        }

        float sliceWidth = (maxX - minX) / Mathf.Max(sliceCount, 1);

        // --- Draw vertical slices ---
        for (int i = 0; i < sliceCount; i++)
        {
            float xLeft = minX + i * sliceWidth;
            float xRight = xLeft + sliceWidth;

            List<Vector2> slicePoly =
                ClipPolygonBetweenXPlanes(submergedPoly, xLeft, xRight);

            if (slicePoly.Count < 3)
                continue;

            // Draw slice outline
            Gizmos.color = Color.yellow;
            for (int j = 0; j < slicePoly.Count; j++)
                Gizmos.DrawLine(slicePoly[j], slicePoly[(j + 1) % slicePoly.Count]);

            // Draw centroid
            float area = PolygonArea(slicePoly);
            if (area > 0f)
            {
                Vector2 centroid = -PolygonCentroid(slicePoly, area);
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(centroid, 0.05f);
            }
        }

        // --- Draw water surface reference ---
        Gizmos.color = Color.red;
        float drawMinX = body.Position.x - body.Width;
        float drawMaxX = body.Position.x + body.Width;

        const int steps = 16;
        Vector2 prev = new Vector2(drawMinX, wave.SampleHeight(drawMinX));
        for (int i = 1; i <= steps; i++)
        {
            float x = Mathf.Lerp(drawMinX, drawMaxX, i / (float)steps);
            Vector2 curr = new Vector2(x, wave.SampleHeight(x));
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }
#endif

}
