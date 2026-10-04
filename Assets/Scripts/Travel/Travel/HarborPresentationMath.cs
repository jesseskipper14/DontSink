using UnityEngine;

/// <summary>Observation-only projection shared by harbor presentation and future piloting viewscape.</summary>
public static class HarborPresentationMath
{
    public static Vector2 BoatRelative(Vector2 offset, float headingDegrees)
    {
        float angle = headingDegrees * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        return new Vector2(Vector2.Dot(offset, new Vector2(forward.y, -forward.x)), Vector2.Dot(offset, forward));
    }

    public static float Visibility(float fog, float rain) =>
        (1 - Mathf.Clamp01(fog)) * Mathf.Lerp(1, .35f, Mathf.Clamp01(rain));

    public static float RangeFade(float distance, float range, float fadeStart)
    {
        if (range <= .0001f || !WorldTopology.IsFinite(distance) || !WorldTopology.IsFinite(range)) return 0;
        return 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(range * Mathf.Clamp(fadeStart, 0, .99f), range, distance));
    }

    public static float SmoothFactor(float seconds, float deltaTime) =>
        1 - Mathf.Exp(-Mathf.Max(0, deltaTime) / Mathf.Max(.01f, seconds));

    public static void BerthCorners(HarborBerth berth, Vector2[] corners)
    {
        Vector2 forward = berth.Harbor.Waterward * berth.HalfLength;
        Vector2 right = new Vector2(berth.Harbor.Waterward.y, -berth.Harbor.Waterward.x) * berth.HalfWidth;
        corners[0] = berth.Center - forward - right;
        corners[1] = berth.Center + forward - right;
        corners[2] = berth.Center + forward + right;
        corners[3] = berth.Center - forward + right;
    }
}
