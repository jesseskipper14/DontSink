using UnityEngine;

/// <summary>
/// Shared visual transform for every map-table page. World map and star-chart board
/// deliberately use the same center and pixels-per-world-unit so switching pages or
/// enabling compare mode preserves exact screen registration.
/// </summary>
public sealed class MapTableViewportState
{
    public const float MinPixelsPerWorldUnit = 1f;
    public const float MaxPixelsPerWorldUnit = 240f;

    public bool IsInitialized { get; private set; }
    public Vector2 CenterWorld { get; private set; }
    public float PixelsPerWorldUnit { get; private set; } = 32f;

    public void Set(Vector2 centerWorld, float pixelsPerWorldUnit)
    {
        CenterWorld = centerWorld;
        PixelsPerWorldUnit = Mathf.Clamp(
            pixelsPerWorldUnit,
            MinPixelsPerWorldUnit,
            MaxPixelsPerWorldUnit);
        IsInitialized = true;
    }

    public void Clear()
    {
        IsInitialized = false;
        CenterWorld = Vector2.zero;
        PixelsPerWorldUnit = 32f;
    }

    public void FitToBounds(Rect viewport, Rect worldBounds, float fillFraction = 0.82f)
    {
        float width = Mathf.Max(0.001f, worldBounds.width);
        float height = Mathf.Max(0.001f, worldBounds.height);

        float zoomX = Mathf.Max(1f, viewport.width) / width;
        float zoomY = Mathf.Max(1f, viewport.height) / height;
        float zoom = Mathf.Min(zoomX, zoomY) * Mathf.Clamp(fillFraction, 0.1f, 1f);

        Set(worldBounds.center, zoom);
    }

    public Vector2 WorldToLocal(Vector2 worldPosition, Rect localRect)
    {
        float zoom = Mathf.Max(0.0001f, PixelsPerWorldUnit);
        return new Vector2(
            localRect.width * 0.5f + (worldPosition.x - CenterWorld.x) * zoom,
            localRect.height * 0.5f - (worldPosition.y - CenterWorld.y) * zoom);
    }

    public Vector2 WorldToScreen(Vector2 worldPosition, Rect viewport)
    {
        return viewport.position + WorldToLocal(
            worldPosition,
            new Rect(0f, 0f, viewport.width, viewport.height));
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition, Rect viewport)
    {
        Vector2 local = screenPosition - viewport.position;
        float zoom = Mathf.Max(0.0001f, PixelsPerWorldUnit);

        return new Vector2(
            CenterWorld.x + (local.x - viewport.width * 0.5f) / zoom,
            CenterWorld.y - (local.y - viewport.height * 0.5f) / zoom);
    }

    public void PanByScreenDelta(Vector2 screenDelta)
    {
        float invZoom = 1f / Mathf.Max(0.0001f, PixelsPerWorldUnit);
        Set(
            new Vector2(
                CenterWorld.x - screenDelta.x * invZoom,
                CenterWorld.y + screenDelta.y * invZoom),
            PixelsPerWorldUnit);
    }

    public void ZoomAroundScreenPoint(
        Vector2 screenPoint,
        Rect viewport,
        float zoomFactor)
    {
        if (!IsInitialized)
            return;

        Vector2 before = ScreenToWorld(screenPoint, viewport);
        float nextZoom = Mathf.Clamp(
            PixelsPerWorldUnit * Mathf.Max(0.01f, zoomFactor),
            MinPixelsPerWorldUnit,
            MaxPixelsPerWorldUnit);

        Set(CenterWorld, nextZoom);
        Vector2 after = ScreenToWorld(screenPoint, viewport);
        Set(CenterWorld + (before - after), nextZoom);
    }
}
