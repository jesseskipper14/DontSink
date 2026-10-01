using UnityEngine;

/// <summary>
/// Shared conversion helpers for the continuous world-map coordinate space.
/// Celestial coordinates intentionally use this exact same space.
/// </summary>
public static class WorldMapCoordinateSpace
{
    public static bool IsValidBounds(Rect worldBounds)
    {
        return worldBounds.width > 0f && worldBounds.height > 0f;
    }

    public static Vector2 WorldToNormalized(Rect worldBounds, Vector2 worldPosition, bool clamp01 = false)
    {
        if (!IsValidBounds(worldBounds))
            return Vector2.zero;

        float u = (worldPosition.x - worldBounds.xMin) / worldBounds.width;
        float v = (worldPosition.y - worldBounds.yMin) / worldBounds.height;

        if (clamp01)
        {
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
        }

        return new Vector2(u, v);
    }

    public static Vector2 NormalizedToWorld(Rect worldBounds, Vector2 normalizedPosition, bool clamp01 = false)
    {
        if (!IsValidBounds(worldBounds))
            return Vector2.zero;

        float u = clamp01 ? Mathf.Clamp01(normalizedPosition.x) : normalizedPosition.x;
        float v = clamp01 ? Mathf.Clamp01(normalizedPosition.y) : normalizedPosition.y;

        return new Vector2(
            worldBounds.xMin + u * worldBounds.width,
            worldBounds.yMin + v * worldBounds.height);
    }

    public static Rect NormalizedToWorld(Rect worldBounds, Rect normalizedRect, bool clamp01 = false)
    {
        Vector2 min = NormalizedToWorld(
            worldBounds,
            new Vector2(normalizedRect.xMin, normalizedRect.yMin),
            clamp01);

        Vector2 max = NormalizedToWorld(
            worldBounds,
            new Vector2(normalizedRect.xMax, normalizedRect.yMax),
            clamp01);

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public static Rect WorldToNormalized(Rect worldBounds, Rect worldRect, bool clamp01 = false)
    {
        Vector2 min = WorldToNormalized(
            worldBounds,
            new Vector2(worldRect.xMin, worldRect.yMin),
            clamp01);

        Vector2 max = WorldToNormalized(
            worldBounds,
            new Vector2(worldRect.xMax, worldRect.yMax),
            clamp01);

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
