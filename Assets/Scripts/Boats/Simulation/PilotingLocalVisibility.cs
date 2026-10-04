using UnityEngine;

/// <summary>Local physical observation math. No knowledge, route or navigation writes.</summary>
public static class PilotingLocalVisibility
{
    public static float PhysicalRadius(float nauticalMiles, float physicalUnitsPerMile, float mapUnitsPerPhysicalUnit, float observerHeightMultiplier = 1)
    {
        float radius = nauticalMiles * physicalUnitsPerMile * mapUnitsPerPhysicalUnit * observerHeightMultiplier;
        return nauticalMiles > 0 && physicalUnitsPerMile > 0 && mapUnitsPerPhysicalUnit > 0 && observerHeightMultiplier >= 0 &&
            WorldTopology.IsFinite(radius) ? radius : 0;
    }

    public static float Radius(float circumference, float observerHeightMultiplier = 1) =>
        Mathf.Max(.01f, circumference) / (Mathf.PI * 2) * Mathf.Max(0, observerHeightMultiplier);

    public static Vector2 WorldOffset(Vector2 boatRelative, float heading)
    {
        float a = heading * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        return new Vector2(forward.y, -forward.x) * boatRelative.x + forward * boatRelative.y;
    }

    public static float LandAlpha(WorldMapTopographyField field, float sea, Vector2 observer,
        float heading, Vector2 relative, float effectiveRange, float fadeStart)
    {
        if (field == null || !field.IsValid || effectiveRange <= .0001f) return 0;
        Vector2 point = observer + WorldOffset(relative, heading);
        if (!new WorldTopology(field.WorldBounds).ContainsY(point.y)) return 0;
        float alpha = HarborPresentationMath.RangeFade(relative.magnitude, effectiveRange, fadeStart);
        if (alpha <= 0) return 0;
        return field.Sample01World(point) >= sea ? alpha : 0;
    }
}
