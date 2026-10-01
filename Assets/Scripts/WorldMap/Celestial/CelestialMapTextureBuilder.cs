using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 2 rasterizer for displaying deterministic celestial truth on the existing
/// IMGUI world-map cartridge. This is intentionally presentation-only.
/// </summary>
public static class CelestialMapTextureBuilder
{
    public static CelestialMapTextureSet Build(
        CelestialField field,
        CelestialMapOverlaySettings settings)
    {
        if (field == null || !field.IsValid || settings == null)
            return null;

        ResolveTextureSize(field.WorldBounds, settings, out int width, out int height);

        var ambientPixels = new Color32[width * height];
        var landmarkPixels = new Color32[width * height];
        var nebulaPixels = new Color32[width * height];
        var deepSkyPixels = new Color32[width * height];

        var objects = new List<CelestialObject>();
        field.QueryAll(objects);

        int ambientCount = 0;
        int landmarkCount = 0;
        int nebulaCount = 0;
        int deepSkyCount = 0;

        float pixelsPerWorldX = (width - 1) / Mathf.Max(0.0001f, field.WorldBounds.width);
        float pixelsPerWorldY = (height - 1) / Mathf.Max(0.0001f, field.WorldBounds.height);
        float pixelsPerWorld = Mathf.Min(pixelsPerWorldX, pixelsPerWorldY);

        for (int i = 0; i < objects.Count; i++)
        {
            CelestialObject obj = objects[i];
            if (obj == null)
                continue;

            WorldToPixel(field.WorldBounds, obj.WorldPosition, width, height, out int px, out int py);
            Color baseColor = settings.GetColor(obj.ColorClass);

            switch (obj.Kind)
            {
                case CelestialObjectKind.AmbientStar:
                    ambientCount++;
                    DrawAmbientStar(ambientPixels, width, height, px, py, obj, baseColor, settings);
                    break;

                case CelestialObjectKind.LandmarkStar:
                    landmarkCount++;
                    DrawLandmarkStar(landmarkPixels, width, height, px, py, obj, baseColor, settings);
                    break;

                case CelestialObjectKind.Nebula:
                    nebulaCount++;
                    DrawNebula(nebulaPixels, width, height, px, py, obj, baseColor, settings, pixelsPerWorld);
                    break;

                case CelestialObjectKind.DeepSkyObject:
                    deepSkyCount++;
                    DrawDeepSkyObject(deepSkyPixels, width, height, px, py, obj, baseColor, settings, pixelsPerWorld);
                    break;
            }
        }

        Texture2D ambientTexture = CreateTexture(
            width, height, ambientPixels, settings.filterMode, $"CelestialAmbient_{field.WorldSeed}");

        Texture2D landmarkTexture = CreateTexture(
            width, height, landmarkPixels, settings.filterMode, $"CelestialLandmarks_{field.WorldSeed}");

        Texture2D nebulaTexture = CreateTexture(
            width, height, nebulaPixels, settings.filterMode, $"CelestialNebulae_{field.WorldSeed}");

        Texture2D deepSkyTexture = CreateTexture(
            width, height, deepSkyPixels, settings.filterMode, $"CelestialDeepSky_{field.WorldSeed}");

        return new CelestialMapTextureSet(
            field.WorldBounds,
            width,
            height,
            ambientTexture,
            landmarkTexture,
            nebulaTexture,
            deepSkyTexture,
            ambientCount,
            landmarkCount,
            nebulaCount,
            deepSkyCount);
    }

    private static void ResolveTextureSize(
        Rect worldBounds,
        CelestialMapOverlaySettings settings,
        out int width,
        out int height)
    {
        float requestedWidth = Mathf.Max(16f, worldBounds.width * settings.pixelsPerWorldUnit);
        float requestedHeight = Mathf.Max(16f, worldBounds.height * settings.pixelsPerWorldUnit);

        float maxRequestedDimension = Mathf.Max(requestedWidth, requestedHeight);
        float cap = Mathf.Max(256f, settings.maxTextureDimension);
        float scale = maxRequestedDimension > cap ? cap / maxRequestedDimension : 1f;

        width = Mathf.Max(16, Mathf.RoundToInt(requestedWidth * scale));
        height = Mathf.Max(16, Mathf.RoundToInt(requestedHeight * scale));
    }

    private static Texture2D CreateTexture(
        int width,
        int height,
        Color32[] pixels,
        FilterMode filterMode,
        string textureName)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
        {
            name = textureName,
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return texture;
    }

    private static void DrawAmbientStar(
        Color32[] pixels,
        int width,
        int height,
        int px,
        int py,
        CelestialObject obj,
        Color baseColor,
        CelestialMapOverlaySettings settings)
    {
        float radius = Mathf.Lerp(
            settings.ambientRadiusPixelsMin,
            settings.ambientRadiusPixelsMax,
            Mathf.Clamp01(obj.Prominence01 * 0.65f + obj.Brightness01 * 0.35f));

        float alpha = settings.ambientAlpha * Mathf.Lerp(0.48f, 1f, obj.Brightness01);
        DrawSoftDisc(pixels, width, height, px, py, radius, WithAlpha(baseColor, alpha));
    }

    private static void DrawLandmarkStar(
        Color32[] pixels,
        int width,
        int height,
        int px,
        int py,
        CelestialObject obj,
        Color baseColor,
        CelestialMapOverlaySettings settings)
    {
        float emphasis = Mathf.Clamp01(obj.Prominence01 * 0.7f + obj.Brightness01 * 0.3f);
        float coreRadius = Mathf.Lerp(
            settings.landmarkCoreRadiusPixelsMin,
            settings.landmarkCoreRadiusPixelsMax,
            emphasis);

        float rayLength = Mathf.Lerp(
            settings.landmarkRayPixelsMin,
            settings.landmarkRayPixelsMax,
            emphasis);

        Color color = WithAlpha(baseColor, settings.landmarkAlpha);
        DrawSoftDisc(pixels, width, height, px, py, coreRadius, color);

        int ray = Mathf.Max(1, Mathf.RoundToInt(rayLength));
        int variant = Mathf.Abs(obj.VisualVariant) % 6;

        if (variant == 0 || variant == 2 || variant == 4 || variant == 5)
        {
            DrawPixelLine(pixels, width, height, px - ray, py, px + ray, py, color);
            DrawPixelLine(pixels, width, height, px, py - ray, px, py + ray, color);
        }

        if (variant == 1 || variant == 2 || variant == 3 || variant == 5)
        {
            int diag = Mathf.Max(1, Mathf.RoundToInt(ray * 0.72f));
            DrawPixelLine(pixels, width, height, px - diag, py - diag, px + diag, py + diag, color);
            DrawPixelLine(pixels, width, height, px - diag, py + diag, px + diag, py - diag, color);
        }

        if (variant == 4)
            DrawDiamond(pixels, width, height, px, py, ray, color);
        else if (variant == 3)
            DrawRing(pixels, width, height, px, py, Mathf.Max(2f, ray * 0.8f), 1f, color);
    }

    private static void DrawNebula(
        Color32[] pixels,
        int width,
        int height,
        int px,
        int py,
        CelestialObject obj,
        Color baseColor,
        CelestialMapOverlaySettings settings,
        float pixelsPerWorld)
    {
        float major = Mathf.Max(2f, obj.FootprintRadiusWorld * pixelsPerWorld);
        float variant01 = (Mathf.Abs(obj.VisualVariant) % 4) / 3f;
        float aspect = Mathf.Lerp(settings.nebulaMinimumAspect, 0.86f, variant01);
        float minor = Mathf.Max(1f, major * aspect);

        float radians = obj.RotationDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        int radius = Mathf.CeilToInt(major);
        float alphaBase = settings.nebulaAlpha * Mathf.Lerp(0.55f, 1f, obj.Brightness01);

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                float localX = cos * dx + sin * dy;
                float localY = -sin * dx + cos * dy;

                float nx = localX / Mathf.Max(0.0001f, major);
                float ny = localY / Mathf.Max(0.0001f, minor);
                float d = nx * nx + ny * ny;

                if (d > 1f)
                    continue;

                float edge = 1f - Mathf.Sqrt(d);
                float alpha = alphaBase * edge * edge;

                BlendPixel(
                    pixels,
                    width,
                    height,
                    px + dx,
                    py + dy,
                    WithAlpha(baseColor, alpha));
            }
        }

        DrawSoftDisc(
            pixels,
            width,
            height,
            px,
            py,
            Mathf.Max(1f, major * 0.08f),
            WithAlpha(baseColor, Mathf.Min(1f, alphaBase * 1.6f)));
    }

    private static void DrawDeepSkyObject(
        Color32[] pixels,
        int width,
        int height,
        int px,
        int py,
        CelestialObject obj,
        Color baseColor,
        CelestialMapOverlaySettings settings,
        float pixelsPerWorld)
    {
        float radius = Mathf.Max(
            settings.deepSkyMinimumRadiusPixels,
            obj.FootprintRadiusWorld * pixelsPerWorld);

        Color color = WithAlpha(
            baseColor,
            settings.deepSkyAlpha * Mathf.Lerp(0.68f, 1f, obj.Brightness01));

        int variant = Mathf.Abs(obj.VisualVariant) % 4;

        switch (variant)
        {
            case 0:
                DrawRing(pixels, width, height, px, py, radius, settings.deepSkyRingThicknessPixels, color);
                DrawSoftDisc(pixels, width, height, px, py, 1.25f, color);
                break;

            case 1:
                DrawDiamond(pixels, width, height, px, py, Mathf.RoundToInt(radius), color);
                DrawSoftDisc(pixels, width, height, px, py, 1f, color);
                break;

            case 2:
            {
                int ray = Mathf.Max(2, Mathf.RoundToInt(radius));
                DrawPixelLine(pixels, width, height, px - ray, py, px + ray, py, color);
                DrawPixelLine(pixels, width, height, px, py - ray, px, py + ray, color);
                DrawRing(pixels, width, height, px, py, Mathf.Max(2f, radius * 0.55f), 1f, color);
                break;
            }

            default:
                DrawRing(pixels, width, height, px, py, radius, settings.deepSkyRingThicknessPixels, color);
                DrawRing(pixels, width, height, px, py, Mathf.Max(1.5f, radius * 0.55f), 1f, color);
                break;
        }
    }

    private static void WorldToPixel(
        Rect worldBounds,
        Vector2 worldPosition,
        int width,
        int height,
        out int px,
        out int py)
    {
        Vector2 uv = WorldMapCoordinateSpace.WorldToNormalized(worldBounds, worldPosition, clamp01: true);
        px = Mathf.Clamp(Mathf.RoundToInt(uv.x * (width - 1)), 0, width - 1);
        py = Mathf.Clamp(Mathf.RoundToInt(uv.y * (height - 1)), 0, height - 1);
    }

    private static void DrawSoftDisc(
        Color32[] pixels,
        int width,
        int height,
        int cx,
        int cy,
        float radius,
        Color color)
    {
        radius = Mathf.Max(0.35f, radius);
        int r = Mathf.Max(1, Mathf.CeilToInt(radius));
        float invRadius = 1f / radius;

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                float dist = Mathf.Sqrt(x * x + y * y);
                if (dist > radius + 0.5f)
                    continue;

                float edge = Mathf.Clamp01((radius + 0.5f - dist) * invRadius);
                float coverage = Mathf.Clamp01(edge * 1.8f);

                BlendPixel(
                    pixels,
                    width,
                    height,
                    cx + x,
                    cy + y,
                    WithAlpha(color, color.a * coverage));
            }
        }
    }

    private static void DrawRing(
        Color32[] pixels,
        int width,
        int height,
        int cx,
        int cy,
        float radius,
        float thickness,
        Color color)
    {
        radius = Mathf.Max(1f, radius);
        thickness = Mathf.Max(0.5f, thickness);

        int r = Mathf.CeilToInt(radius + thickness);
        float halfThickness = thickness * 0.5f;

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                float dist = Mathf.Sqrt(x * x + y * y);
                if (Mathf.Abs(dist - radius) > halfThickness + 0.55f)
                    continue;

                float coverage = 1f - Mathf.Clamp01(
                    (Mathf.Abs(dist - radius) - halfThickness) / 0.55f);

                BlendPixel(
                    pixels,
                    width,
                    height,
                    cx + x,
                    cy + y,
                    WithAlpha(color, color.a * coverage));
            }
        }
    }

    private static void DrawDiamond(
        Color32[] pixels,
        int width,
        int height,
        int cx,
        int cy,
        int radius,
        Color color)
    {
        radius = Mathf.Max(1, radius);

        for (int i = 0; i <= radius; i++)
        {
            BlendPixel(pixels, width, height, cx + i, cy + (radius - i), color);
            BlendPixel(pixels, width, height, cx + i, cy - (radius - i), color);
            BlendPixel(pixels, width, height, cx - i, cy + (radius - i), color);
            BlendPixel(pixels, width, height, cx - i, cy - (radius - i), color);
        }
    }

    private static void DrawPixelLine(
        Color32[] pixels,
        int width,
        int height,
        int x0,
        int y0,
        int x1,
        int y1,
        Color color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            BlendPixel(pixels, width, height, x0, y0, color);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void BlendPixel(
        Color32[] pixels,
        int width,
        int height,
        int x,
        int y,
        Color source)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        source.r = Mathf.Clamp01(source.r);
        source.g = Mathf.Clamp01(source.g);
        source.b = Mathf.Clamp01(source.b);
        source.a = Mathf.Clamp01(source.a);

        int index = y * width + x;
        Color destination = pixels[index];

        float sourceAlpha = source.a;
        float destinationAlpha = destination.a;
        float outAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);

        if (outAlpha <= 0.0001f)
        {
            pixels[index] = new Color32(0, 0, 0, 0);
            return;
        }

        float destinationContribution = destinationAlpha * (1f - sourceAlpha);

        Color output = new Color(
            (source.r * sourceAlpha + destination.r * destinationContribution) / outAlpha,
            (source.g * sourceAlpha + destination.g * destinationContribution) / outAlpha,
            (source.b * sourceAlpha + destination.b * destinationContribution) / outAlpha,
            outAlpha);

        pixels[index] = output;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
