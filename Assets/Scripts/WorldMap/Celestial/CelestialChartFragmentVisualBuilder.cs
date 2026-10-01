using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only generated representation of one persistent chart fragment.
/// Paper and ink are intentionally separate textures so Phase 6 can render ALL
/// paper below ALL celestial marks even when multiple scraps overlap.
/// </summary>
public sealed class CelestialChartFragmentVisual : IDisposable
{
    public Texture2D PaperTexture { get; internal set; }
    public Texture2D InkTexture { get; internal set; }
    public Rect RotatedCelestialBounds { get; internal set; }
    public float PixelsPerCelestialWorldUnit { get; internal set; }
    public Vector2Int PixelSize { get; internal set; }

    public void Dispose()
    {
        DestroyTexture(PaperTexture);
        DestroyTexture(InkTexture);
        PaperTexture = null;
        InkTexture = null;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(texture);
        else
            UnityEngine.Object.DestroyImmediate(texture);
    }
}

public static class CelestialChartFragmentVisualBuilder
{
    private const int MaxGeneratedTextureDimension = 2048;

    private readonly struct Style
    {
        public readonly float pixelsPerWorldUnit;
        public readonly float marginWorld;
        public readonly float minWidthWorld;
        public readonly float minHeightWorld;
        public readonly float tearDepthPixels;
        public readonly float tearFeaturePixels;
        public readonly float grainStrength;
        public readonly Color paperColor;
        public readonly Color inkColor;

        public Style(CelestialChartFragmentVisualSettings settings)
        {
            pixelsPerWorldUnit = settings != null
                ? Mathf.Max(0.25f, settings.pixelsPerCelestialWorldUnit)
                : 4f;

            marginWorld = settings != null
                ? Mathf.Max(0f, settings.paperMarginWorldUnits)
                : 3.5f;

            minWidthWorld = settings != null
                ? Mathf.Max(1f, settings.minimumPaperWidthWorld)
                : 22f;

            minHeightWorld = settings != null
                ? Mathf.Max(1f, settings.minimumPaperHeightWorld)
                : 16f;

            tearDepthPixels = settings != null
                ? Mathf.Clamp(settings.tornEdgeDepthPixels, 0f, 18f)
                : 7f;

            tearFeaturePixels = settings != null
                ? Mathf.Clamp(settings.tornEdgeFeatureSizePixels, 2f, 32f)
                : 11f;

            grainStrength = settings != null
                ? Mathf.Clamp(settings.paperGrainStrength, 0f, 0.20f)
                : 0.055f;

            paperColor = settings != null
                ? settings.paperColor
                : new Color(0.78f, 0.69f, 0.50f, 1f);

            inkColor = settings != null
                ? settings.inkColor
                : new Color(0.10f, 0.105f, 0.10f, 0.92f);
        }
    }

    public static CelestialChartFragmentVisual Build(
        CelestialChartFragmentSnapshot fragment,
        CelestialChartFragmentVisualSettings settings = null)
    {
        if (fragment == null)
            return null;

        fragment.EnsureDefaults();
        Style style = new Style(settings);

        float rotationDegrees = NormalizeSignedDegrees(fragment.recordedInstrumentRotationDegrees);
        List<Vector2> transformedMarks = new List<Vector2>(fragment.marks.Count);

        bool hasPoint = false;
        float minX = 0f;
        float maxX = 0f;
        float minY = 0f;
        float maxY = 0f;

        for (int i = 0; i < fragment.marks.Count; i++)
        {
            CelestialChartFragmentMark mark = fragment.marks[i];
            if (mark == null)
            {
                transformedMarks.Add(Vector2.zero);
                continue;
            }

            Vector2 p = Rotate(mark.celestialWorldPosition, rotationDegrees);
            transformedMarks.Add(p);
            Encapsulate(ref hasPoint, ref minX, ref maxX, ref minY, ref maxY, p);
        }

        Vector2 transformedDatum = Rotate(fragment.observationDatumWorldPosition, rotationDegrees);
        Encapsulate(ref hasPoint, ref minX, ref maxX, ref minY, ref maxY, transformedDatum);

        if (!hasPoint)
        {
            minX = minY = -0.5f;
            maxX = maxY = 0.5f;
        }

        minX -= style.marginWorld;
        maxX += style.marginWorld;
        minY -= style.marginWorld;
        maxY += style.marginWorld;

        EnsureMinimumSpan(ref minX, ref maxX, style.minWidthWorld);
        EnsureMinimumSpan(ref minY, ref maxY, style.minHeightWorld);

        Rect bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        float effectivePixelsPerWorldUnit = style.pixelsPerWorldUnit;

        int width = Mathf.Max(32, Mathf.CeilToInt(bounds.width * effectivePixelsPerWorldUnit));
        int height = Mathf.Max(32, Mathf.CeilToInt(bounds.height * effectivePixelsPerWorldUnit));

        if (width > MaxGeneratedTextureDimension || height > MaxGeneratedTextureDimension)
        {
            float scale = Mathf.Min(
                MaxGeneratedTextureDimension / (float)width,
                MaxGeneratedTextureDimension / (float)height);

            effectivePixelsPerWorldUnit *= Mathf.Clamp01(scale);
            width = Mathf.Max(32, Mathf.CeilToInt(bounds.width * effectivePixelsPerWorldUnit));
            height = Mathf.Max(32, Mathf.CeilToInt(bounds.height * effectivePixelsPerWorldUnit));

            Debug.LogWarning(
                $"[CelestialChartFragmentVisualBuilder] Fragment '{fragment.fragmentId}' exceeded the " +
                $"{MaxGeneratedTextureDimension}px safety limit. Preview scale was reduced to " +
                $"{effectivePixelsPerWorldUnit:0.###} px/world-unit for this fragment.");
        }

        int seed = fragment.visualSeed;
        Texture2D paper = BuildPaperTexture(width, height, seed, style);
        Texture2D ink = BuildInkTexture(
            fragment,
            transformedMarks,
            transformedDatum,
            bounds,
            effectivePixelsPerWorldUnit,
            width,
            height,
            style);

        return new CelestialChartFragmentVisual
        {
            PaperTexture = paper,
            InkTexture = ink,
            RotatedCelestialBounds = bounds,
            PixelsPerCelestialWorldUnit = effectivePixelsPerWorldUnit,
            PixelSize = new Vector2Int(width, height)
        };
    }

    private static Texture2D BuildPaperTexture(
        int width,
        int height,
        int seed,
        Style style)
    {
        Texture2D texture = NewTexture(width, height, $"ChartPaper_{seed:X8}");
        Color32[] pixels = new Color32[width * height];

        float tearDepth = Mathf.Max(0f, style.tearDepthPixels);
        float feature = Mathf.Max(2f, style.tearFeaturePixels);
        Color baseColor = style.paperColor;

        for (int y = 0; y < height; y++)
        {
            float leftInset = 1f + EdgeNoise(y, seed ^ 0x154A35, feature) * tearDepth;
            float rightInset = 1f + EdgeNoise(y, seed ^ 0x51C3A7, feature) * tearDepth;

            for (int x = 0; x < width; x++)
            {
                float bottomInset = 1f + EdgeNoise(x, seed ^ 0x2C1B3D, feature) * tearDepth;
                float topInset = 1f + EdgeNoise(x, seed ^ 0x73A1E5, feature) * tearDepth;

                bool inside =
                    x >= leftInset &&
                    x < width - rightInset &&
                    y >= bottomInset &&
                    y < height - topInset;

                int index = y * width + x;
                if (!inside)
                {
                    pixels[index] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float grain = (Hash01(x, y, seed ^ 0x6E624EB7) - 0.5f) * 2f * style.grainStrength;
                float edgeDarken = ComputeEdgeDarken(
                    x,
                    y,
                    width,
                    height,
                    leftInset,
                    rightInset,
                    bottomInset,
                    topInset);

                float multiplier = Mathf.Clamp01(1f + grain - edgeDarken * 0.10f);
                Color c = new Color(
                    Mathf.Clamp01(baseColor.r * multiplier),
                    Mathf.Clamp01(baseColor.g * multiplier),
                    Mathf.Clamp01(baseColor.b * multiplier),
                    baseColor.a);

                pixels[index] = (Color32)c;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildInkTexture(
        CelestialChartFragmentSnapshot fragment,
        List<Vector2> transformedMarks,
        Vector2 transformedDatum,
        Rect bounds,
        float pixelsPerWorldUnit,
        int width,
        int height,
        Style style)
    {
        Texture2D texture = NewTexture(width, height, $"ChartInk_{fragment.visualSeed:X8}");
        Color32[] pixels = new Color32[width * height];
        Color ink = style.inkColor;

        for (int i = 0; i < fragment.marks.Count && i < transformedMarks.Count; i++)
        {
            CelestialChartFragmentMark mark = fragment.marks[i];
            if (mark == null)
                continue;

            Vector2Int p = ToPixel(transformedMarks[i], bounds, pixelsPerWorldUnit, width, height);
            float strength = Mathf.Clamp01(Mathf.Lerp(0.45f, 1f, Mathf.Max(mark.brightness01, mark.prominence01)));
            Color markInk = new Color(ink.r, ink.g, ink.b, ink.a * strength);

            switch (mark.kind)
            {
                case CelestialObjectKind.AmbientStar:
                    DrawDisc(pixels, width, height, p.x, p.y, mark.brightness01 > 0.72f ? 2 : 1, markInk);
                    break;

                case CelestialObjectKind.LandmarkStar:
                {
                    int radius = Mathf.RoundToInt(Mathf.Lerp(2f, 4f, mark.prominence01));
                    DrawDisc(pixels, width, height, p.x, p.y, radius, markInk);
                    DrawCross(pixels, width, height, p.x, p.y, radius + 3, markInk);
                    if (mark.isPatternAnchor)
                        DrawRing(pixels, width, height, p.x, p.y, radius + 5, 1, new Color(ink.r, ink.g, ink.b, ink.a * 0.70f));
                    break;
                }

                case CelestialObjectKind.Nebula:
                    DrawEllipseRing(pixels, width, height, p.x, p.y, 7, 4, markInk);
                    break;

                case CelestialObjectKind.DeepSkyObject:
                    DrawDiamond(pixels, width, height, p.x, p.y, 5, markInk);
                    break;
            }
        }

        Vector2Int datum = ToPixel(transformedDatum, bounds, pixelsPerWorldUnit, width, height);
        // Observation datum deliberately has no heading/orientation glyph. It is only a circular registration mark.
        DrawRing(pixels, width, height, datum.x, datum.y, 4, 1, new Color(ink.r, ink.g, ink.b, ink.a * 0.82f));
        DrawDisc(pixels, width, height, datum.x, datum.y, 1, new Color(ink.r, ink.g, ink.b, ink.a * 0.62f));

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D NewTexture(int width, int height, string name)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        return texture;
    }

    private static Vector2Int ToPixel(
        Vector2 world,
        Rect bounds,
        float pixelsPerWorldUnit,
        int width,
        int height)
    {
        int x = Mathf.Clamp(
            Mathf.RoundToInt((world.x - bounds.xMin) * pixelsPerWorldUnit),
            0,
            width - 1);

        int y = Mathf.Clamp(
            Mathf.RoundToInt((world.y - bounds.yMin) * pixelsPerWorldUnit),
            0,
            height - 1);

        return new Vector2Int(x, y);
    }

    private static void Encapsulate(
        ref bool hasPoint,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY,
        Vector2 p)
    {
        if (!hasPoint)
        {
            hasPoint = true;
            minX = maxX = p.x;
            minY = maxY = p.y;
            return;
        }

        minX = Mathf.Min(minX, p.x);
        maxX = Mathf.Max(maxX, p.x);
        minY = Mathf.Min(minY, p.y);
        maxY = Mathf.Max(maxY, p.y);
    }

    private static void EnsureMinimumSpan(ref float min, ref float max, float minimumSpan)
    {
        float span = max - min;
        if (span >= minimumSpan)
            return;

        float center = (min + max) * 0.5f;
        float half = minimumSpan * 0.5f;
        min = center - half;
        max = center + half;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(radians);
        float s = Mathf.Sin(radians);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private static float NormalizeSignedDegrees(float degrees)
    {
        if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            return 0f;

        return Mathf.DeltaAngle(0f, degrees);
    }

    private static float EdgeNoise(float coordinate, int seed, float featureSize)
    {
        float u = coordinate / Mathf.Max(1f, featureSize);
        int i0 = Mathf.FloorToInt(u);
        int i1 = i0 + 1;
        float t = u - i0;
        t = t * t * (3f - 2f * t);

        float a = Hash01(i0, seed, seed ^ 0x45D9F3B);
        float b = Hash01(i1, seed, seed ^ 0x27D4EB2);
        return Mathf.Lerp(a, b, t);
    }

    private static float ComputeEdgeDarken(
        int x,
        int y,
        int width,
        int height,
        float leftInset,
        float rightInset,
        float bottomInset,
        float topInset)
    {
        float d = Mathf.Min(
            x - leftInset,
            width - rightInset - x,
            y - bottomInset,
            height - topInset - y);

        return 1f - Mathf.Clamp01(d / 5f);
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x9E3779B9u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0x85EBCA6Bu;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static void DrawDisc(
        Color32[] pixels,
        int width,
        int height,
        int cx,
        int cy,
        int radius,
        Color color)
    {
        int r = Mathf.Max(1, radius);
        int r2 = r * r;
        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y <= r2)
                    Blend(pixels, width, height, cx + x, cy + y, color);
            }
        }
    }

    private static void DrawCross(
        Color32[] pixels,
        int width,
        int height,
        int cx,
        int cy,
        int arm,
        Color color)
    {
        int a = Mathf.Max(1, arm);
        for (int d = -a; d <= a; d++)
        {
            Blend(pixels, width, height, cx + d, cy, color);
            Blend(pixels, width, height, cx, cy + d, color);
        }
    }

    private static void DrawRing(
        Color32[] pixels,
        int width,
        int height,
        int cx,
        int cy,
        int radius,
        int thickness,
        Color color)
    {
        int r = Mathf.Max(1, radius);
        int t = Mathf.Max(1, thickness);
        int outer2 = r * r;
        int inner = Mathf.Max(0, r - t);
        int inner2 = inner * inner;

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                int d2 = x * x + y * y;
                if (d2 <= outer2 && d2 >= inner2)
                    Blend(pixels, width, height, cx + x, cy + y, color);
            }
        }
    }

    private static void DrawEllipseRing(
        Color32[] pixels,
        int width,
        int height,
        int cx,
        int cy,
        int rx,
        int ry,
        Color color)
    {
        rx = Mathf.Max(2, rx);
        ry = Mathf.Max(2, ry);
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                float d = (x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry);
                if (d >= 0.72f && d <= 1.18f)
                    Blend(pixels, width, height, cx + x, cy + y, new Color(color.r, color.g, color.b, color.a * 0.58f));
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
        int r = Mathf.Max(2, radius);
        for (int y = -r; y <= r; y++)
        {
            int x = r - Mathf.Abs(y);
            Blend(pixels, width, height, cx - x, cy + y, color);
            Blend(pixels, width, height, cx + x, cy + y, color);
        }
    }

    private static void Blend(
        Color32[] pixels,
        int width,
        int height,
        int x,
        int y,
        Color color)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        int index = y * width + x;
        Color dst = pixels[index];
        float a = Mathf.Clamp01(color.a);
        float outA = a + dst.a * (1f - a);

        if (outA <= 0.0001f)
            return;

        Color result = new Color(
            (color.r * a + dst.r * dst.a * (1f - a)) / outA,
            (color.g * a + dst.g * dst.a * (1f - a)) / outA,
            (color.b * a + dst.b * dst.a * (1f - a)) / outA,
            outA);

        pixels[index] = (Color32)result;
    }
}
