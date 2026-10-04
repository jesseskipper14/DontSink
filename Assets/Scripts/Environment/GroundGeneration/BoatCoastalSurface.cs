using UnityEngine;

/// <summary>Local coastal interpretation. Negative depth means land above the waterline.</summary>
public static class BoatCoastalSurface
{
    public static float Target(float height01, float sea01, float waterY, float baseY,
        AnimationCurve depthCurve, float maximumLandHeight, float coastalBand)
    {
        if (!WorldTopology.IsFinite(height01) || !WorldTopology.IsFinite(sea01) || sea01 <= 0 || sea01 >= 1)
            return baseY;
        if (height01 >= sea01)
            return waterY + Mathf.Clamp01((height01 - sea01) / (1 - sea01)) * maximumLandHeight;
        float depth01 = Mathf.Clamp01((sea01 - height01) / sea01);
        if (depth01 >= coastalBand) return baseY;
        float blend = Mathf.SmoothStep(0, 1, depth01 / Mathf.Max(.001f, coastalBand));
        float depth = Mathf.Max(0, depthCurve.Evaluate(depth01));
        // Meet the committed seabed continuously at the outer edge of the band.
        return Mathf.Lerp(waterY - depth * blend, baseY, blend);
    }

    public static float Advance(float current, float target, float ceiling, float dt,
        float riseSpeed, float fallSpeed)
    {
        // A body may already be touching terrain: never lower it just because its
        // clearance envelope overlaps existing ground. Only prevent NEW uplift.
        target = Mathf.Min(target, Mathf.Max(current, ceiling));
        return Mathf.MoveTowards(current, target, Mathf.Max(0, dt) *
            (target > current ? riseSpeed : fallSpeed));
    }

    public static float BodyCeiling(float x, Bounds bounds, float clearance, float slope)
    {
        float distance = Mathf.Max(bounds.min.x - clearance - x, x - bounds.max.x - clearance, 0);
        return bounds.min.y - clearance + distance * slope;
    }
}
