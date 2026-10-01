using UnityEngine;

/// <summary>
/// Pure projection math shared by the scene sky renderer and celestial charting tools.
///
/// The authored query window remains rectangular so existing sky density / survey balance stays
/// stable, but DISPLAY geometry is now shape-preserving:
/// - world +X / east projects right
/// - world +Y / north projects up
/// - one celestial world unit has the same visual scale on X and Y
///
/// The square charting instrument uses the horizontal query span as its metric span. The side-view
/// scene sky uses the same horizontal metric scale and compensates Y by the camera aspect ratio.
/// This means a celestial pattern may translate/rotate between views, but it is never mirrored or
/// non-uniformly stretched.
/// </summary>
public static class CelestialSkyProjection
{
    /// <summary>
    /// Authored query window. Keeping both fractions here preserves the amount of deterministic sky
    /// queried around the observer and therefore avoids silently retuning survey density.
    /// </summary>
    public static Vector2 GetVisibleWorldSize(
        Rect fieldBounds,
        CelestialSkyProjectionSettings settings)
    {
        if (settings == null || !WorldMapCoordinateSpace.IsValidBounds(fieldBounds))
            return Vector2.zero;

        return new Vector2(
            Mathf.Max(0.0001f, fieldBounds.width * settings.horizontalWorldFraction),
            Mathf.Max(0.0001f, fieldBounds.height * settings.verticalWorldFraction));
    }

    /// <summary>
    /// Canonical metric span used by the telescope / fragments. X and Y both use this same world
    /// scale so shapes are preserved. We intentionally anchor this to the existing horizontal span.
    /// </summary>
    public static float GetMetricWorldSpan(
        Rect fieldBounds,
        CelestialSkyProjectionSettings settings)
    {
        Vector2 visible = GetVisibleWorldSize(fieldBounds, settings);
        return Mathf.Max(0.0001f, visible.x);
    }

    public static Rect BuildVisibleWorldRect(
        Rect fieldBounds,
        Vector2 observerWorldPosition,
        CelestialSkyProjectionSettings settings)
    {
        Vector2 size = GetVisibleWorldSize(fieldBounds, settings);
        return Rect.MinMaxRect(
            observerWorldPosition.x - size.x * 0.5f,
            observerWorldPosition.y - size.y * 0.5f,
            observerWorldPosition.x + size.x * 0.5f,
            observerWorldPosition.y + size.y * 0.5f);
    }

    public static Rect BuildQueryWorldRect(
        Rect fieldBounds,
        Vector2 observerWorldPosition,
        CelestialSkyProjectionSettings settings)
    {
        Rect visible = BuildVisibleWorldRect(fieldBounds, observerWorldPosition, settings);
        float padding = settings != null ? Mathf.Max(0f, settings.queryPaddingFraction) : 0f;

        float padX = visible.width * padding;
        float padY = visible.height * padding;

        return Rect.MinMaxRect(
            visible.xMin - padX,
            visible.yMin - padY,
            visible.xMax + padX,
            visible.yMax + padY);
    }

    /// <summary>
    /// Shape-preserving square/instrument projection.
    ///
    /// signedWindowPosition is metric celestial space normalized against ONE common world span:
    /// (-1,0) = half-span west, (+1,0) = half-span east,
    /// (0,-1) = half-span south, (0,+1) = half-span north.
    ///
    /// The authored verticalWorldFraction still controls which objects are eligible/queryable, but it
    /// no longer stretches their geometry. This is deliberate: query coverage and display geometry are
    /// separate concerns now.
    /// </summary>
    public static bool TryProjectToViewport(
        Rect fieldBounds,
        Vector2 observerWorldPosition,
        Vector2 objectWorldPosition,
        CelestialSkyProjectionSettings settings,
        out Vector2 viewportPosition,
        out Vector2 signedWindowPosition)
    {
        viewportPosition = default;
        signedWindowPosition = default;

        if (settings == null || !WorldMapCoordinateSpace.IsValidBounds(fieldBounds))
            return false;

        Vector2 querySize = GetVisibleWorldSize(fieldBounds, settings);
        float metricSpan = GetMetricWorldSpan(fieldBounds, settings);
        if (querySize.x <= 0f || querySize.y <= 0f || metricSpan <= 0f)
            return false;

        Vector2 delta = objectWorldPosition - observerWorldPosition;

        // Preserve the old authored query/cull envelope so this projection cleanup does not also
        // become an accidental star-density retune.
        float querySignedX = delta.x / (querySize.x * 0.5f);
        float querySignedY = delta.y / (querySize.y * 0.5f);

        float xMargin = Mathf.Max(0f, settings.horizontalCullMarginViewport) * 2f;
        float yMargin = Mathf.Max(0f, settings.verticalCullMarginViewport) * 2f;

        if (querySignedX < -1f - xMargin || querySignedX > 1f + xMargin ||
            querySignedY < -1f - yMargin || querySignedY > 1f + yMargin)
        {
            return false;
        }

        float halfMetricSpan = metricSpan * 0.5f;
        float signedX = delta.x / halfMetricSpan;
        float signedY = delta.y / halfMetricSpan;
        signedWindowPosition = new Vector2(signedX, signedY);

        // Square/instrument projection. North is UP and there is no non-linear axis warp.
        viewportPosition = new Vector2(
            signedX * 0.5f + 0.5f,
            signedY * 0.5f + 0.5f);

        return true;
    }

    /// <summary>
    /// Shape-preserving scene-sky projection. X uses the same metric span as the charting instrument.
    /// Y is multiplied by the camera aspect so one world unit occupies the same number of screen
    /// pixels vertically as horizontally.
    ///
    /// The legacy-named north/south viewport fields now define the scene sky band's lower/upper
    /// presentation bounds and center; they no longer encode a north-down convention.
    /// </summary>
    public static bool TryProjectToSceneViewport(
        Rect fieldBounds,
        Vector2 observerWorldPosition,
        Vector2 objectWorldPosition,
        CelestialSkyProjectionSettings settings,
        float viewportAspect,
        out Vector2 viewportPosition,
        out Vector2 signedWindowPosition)
    {
        viewportPosition = default;
        signedWindowPosition = default;

        if (!TryProjectToViewport(
                fieldBounds,
                observerWorldPosition,
                objectWorldPosition,
                settings,
                out _,
                out signedWindowPosition))
        {
            return false;
        }

        float aspect = Mathf.Max(0.01f, viewportAspect);
        float centerY = settings != null ? settings.SkyViewportCenterY : 0.5f;

        viewportPosition = new Vector2(
            signedWindowPosition.x * 0.5f + 0.5f,
            centerY + signedWindowPosition.y * 0.5f * aspect);

        return true;
    }

    /// <summary>
    /// World dimensions represented by the full camera viewport at the shape-preserving metric scale.
    /// This is presentation information, not the authored query rectangle.
    /// </summary>
    public static Vector2 GetSceneMetricWorldSize(
        Rect fieldBounds,
        CelestialSkyProjectionSettings settings,
        float viewportAspect)
    {
        float span = GetMetricWorldSpan(fieldBounds, settings);
        float aspect = Mathf.Max(0.01f, viewportAspect);
        return new Vector2(span, span / aspect);
    }

    public static bool ViewCrossesFieldBounds(
        Rect fieldBounds,
        Vector2 observerWorldPosition,
        CelestialSkyProjectionSettings settings)
    {
        Rect visible = BuildVisibleWorldRect(fieldBounds, observerWorldPosition, settings);

        return visible.xMin < fieldBounds.xMin ||
               visible.xMax > fieldBounds.xMax ||
               visible.yMin < fieldBounds.yMin ||
               visible.yMax > fieldBounds.yMax;
    }
}
