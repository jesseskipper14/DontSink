using System;

/// <summary>Immutable strip-space request applied before macro depth commitment.
/// Positive depth delta forms a trench; negative forms a rise. Not physical geometry.</summary>
public readonly struct BoatTerrainFeatureDirective
{
    public readonly string Id;
    public readonly double Center;
    public readonly float HalfWidth, DepthDelta, EdgeFraction, MaximumSlopeDegrees;

    public BoatTerrainFeatureDirective(string id, double center, float halfWidth, float depthDelta,
        float edgeFraction = 1f, float maximumSlopeDegrees = 40f)
    {
        if (string.IsNullOrWhiteSpace(id) || double.IsNaN(center) || double.IsInfinity(center) ||
            !WorldTopology.IsFinite(halfWidth) || halfWidth <= 0 || !WorldTopology.IsFinite(depthDelta) ||
            !WorldTopology.IsFinite(edgeFraction) || edgeFraction <= 0 || edgeFraction > 1 ||
            !WorldTopology.IsFinite(maximumSlopeDegrees) || maximumSlopeDegrees < 1 || maximumSlopeDegrees > 80)
            throw new ArgumentException("Terrain features require a stable ID, finite center/depth and positive finite half-width.");
        Id = id; Center = center; HalfWidth = halfWidth; DepthDelta = depthDelta;
        EdgeFraction = edgeFraction; MaximumSlopeDegrees = maximumSlopeDegrees;
    }

    public float DepthOffset(double strip)
    {
        double t = Math.Abs(strip - Center) / HalfWidth;
        if (t >= 1) return 0;
        t = Math.Max(0, (t - (1 - EdgeFraction)) / EdgeFraction);
        double smooth = t * t * t * (t * (t * 6 - 15) + 10);
        return (float)(DepthDelta * (1 - smooth));
    }
}
