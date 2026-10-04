using UnityEngine;

public enum WorldBoundaryBand { Unavailable, Normal, Soft, Hard }
public enum WorldBoundaryPole { None, South, North }

/// <summary>Derived geographic context. Does not move actors or apply environmental effects.</summary>
public readonly struct WorldBoundarySample
{
    public readonly WorldBoundaryBand Band;
    public readonly WorldBoundaryPole Pole;
    public readonly float SignedDistanceToEdge, SoftWidth, HardWidth, Severity;
    public bool IsAvailable => Band != WorldBoundaryBand.Unavailable;
    public bool IsOutside => IsAvailable && SignedDistanceToEdge < 0;
    public Vector2 ReturnDirection => Pole == WorldBoundaryPole.North ? Vector2.down :
        Pole == WorldBoundaryPole.South ? Vector2.up : Vector2.zero;

    public WorldBoundarySample(WorldBoundaryBand band, WorldBoundaryPole pole, float distance,
        float softWidth, float hardWidth, float severity)
    {
        Band = band; Pole = pole; SignedDistanceToEdge = distance;
        SoftWidth = softWidth; HardWidth = hardWidth; Severity = severity;
    }
}

/// <summary>Periodic X has no boundary. Polar bands lie inside finite Y bounds.</summary>
public static class WorldBoundaryQuery
{
    public const float DefaultSoftFraction = .06f;
    public const float DefaultHardFraction = .02f;

    public static WorldBoundarySample Evaluate(WorldTopology topology, Vector2 position,
        float softFraction = DefaultSoftFraction, float hardFraction = DefaultHardFraction)
    {
        if (!topology.IsValid || !WorldTopology.IsFinite(position.x) || !WorldTopology.IsFinite(position.y) ||
            !WorldTopology.IsFinite(softFraction) || !WorldTopology.IsFinite(hardFraction) ||
            hardFraction <= 0 || softFraction <= hardFraction || softFraction > .5f) return default;
        Rect bounds = topology.Bounds;
        float south = position.y - bounds.yMin, north = bounds.yMax - position.y;
        var pole = north < south ? WorldBoundaryPole.North : WorldBoundaryPole.South;
        float distance = Mathf.Min(south, north);
        float softWidth = bounds.height * softFraction, hardWidth = bounds.height * hardFraction;
        var band = distance <= hardWidth ? WorldBoundaryBand.Hard :
            distance <= softWidth ? WorldBoundaryBand.Soft : WorldBoundaryBand.Normal;
        float severity = Mathf.Clamp01(1 - distance / softWidth);
        return new WorldBoundarySample(band, pole, distance, softWidth, hardWidth, severity);
    }
}
