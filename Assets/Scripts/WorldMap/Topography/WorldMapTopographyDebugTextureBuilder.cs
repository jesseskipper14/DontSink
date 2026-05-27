using UnityEngine;

public static class WorldMapTopographyDebugTextureBuilder
{
    public static Texture2D BuildTexture(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings)
    {
        // Backward-compatible old behavior:
        // "debug texture" means full visual with contours baked in.
        return BuildDebugTexture(field, settings);
    }

    public static Texture2D BuildBaseTexture(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings)
    {
        if (field == null || !field.IsValid || settings == null)
            return null;

        int size = Mathf.Max(16, settings.textureResolution);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = settings.textureFilterMode,
            wrapMode = TextureWrapMode.Clamp,
            name = $"TopographyBase_{field.Seed}"
        };

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = size <= 1 ? 0f : y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float u = size <= 1 ? 0f : x / (float)(size - 1);
                float h = field.Sample01UV(u, v);

                Color c = EvaluateHeightColor(h, settings);

                if (settings.drawContours && settings.drawContoursIntoBaseTexture)
                    ApplyContourAndCoastline(ref c, h, settings, includeContours: true, includeSeaLevelLine: true);
                else
                    ApplyContourAndCoastline(ref c, h, settings, includeContours: false, includeSeaLevelLine: true);

                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        return tex;
    }

    public static Texture2D BuildContourTexture(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings)
    {
        if (field == null || !field.IsValid || settings == null)
            return null;

        int size = Mathf.Max(16, settings.textureResolution);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = settings.textureFilterMode,
            wrapMode = TextureWrapMode.Clamp,
            name = $"TopographyContours_{field.Seed}"
        };

        Color32[] pixels = new Color32[size * size];
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < size; y++)
        {
            float v = size <= 1 ? 0f : y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float u = size <= 1 ? 0f : x / (float)(size - 1);
                float h = field.Sample01UV(u, v);

                Color c = clear;

                if (settings.drawContours && IsContour(h, settings, out bool major))
                {
                    c = major ? settings.majorContourColor : settings.contourColor;
                }

                // Keep coastline visible as part of the contour overlay too.
                // Later we may split coastline into yet another overlay if we get fancy.
                if (IsSeaLevelLine(h, settings))
                {
                    Color coast = settings.seaLevelLineColor;

                    if (c.a <= 0.001f)
                        c = coast;
                    else
                        c = AlphaComposite(c, coast);
                }

                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        return tex;
    }

    public static Texture2D BuildDebugTexture(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings)
    {
        if (field == null || !field.IsValid || settings == null)
            return null;

        int size = Mathf.Max(16, settings.textureResolution);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = settings.textureFilterMode,
            wrapMode = TextureWrapMode.Clamp,
            name = $"TopographyDebug_{field.Seed}"
        };

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = size <= 1 ? 0f : y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float u = size <= 1 ? 0f : x / (float)(size - 1);
                float h = field.Sample01UV(u, v);

                Color c = EvaluateHeightColor(h, settings);
                ApplyContourAndCoastline(ref c, h, settings, includeContours: true, includeSeaLevelLine: true);

                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        return tex;
    }

    private static void ApplyContourAndCoastline(
        ref Color c,
        float h,
        WorldMapTopographySettings settings,
        bool includeContours,
        bool includeSeaLevelLine)
    {
        if (includeSeaLevelLine && IsSeaLevelLine(h, settings))
        {
            Color coast = settings.seaLevelLineColor;
            c = Color.Lerp(c, coast, coast.a);
            c.a = 1f;
        }

        if (includeContours && settings.drawContours && IsContour(h, settings, out bool major))
        {
            Color contour = major ? settings.majorContourColor : settings.contourColor;
            c = Color.Lerp(c, contour, contour.a);
            c.a = 1f;
        }
    }

    private static Color EvaluateHeightColor(float h, WorldMapTopographySettings settings)
    {
        h = Mathf.Clamp01(h);

        float sea = Mathf.Clamp01(settings.seaLevel01);

        if (h < sea)
        {
            float depth = sea - h;

            float shallow01 = Mathf.InverseLerp(
                settings.shallowDepth01,
                0f,
                depth
            );

            if (shallow01 > 0f)
            {
                return Color.Lerp(
                    settings.shelfWaterColor,
                    settings.shallowWaterColor,
                    shallow01
                );
            }

            float deep01 = Mathf.InverseLerp(
                1f,
                settings.shallowDepth01,
                depth
            );

            return Color.Lerp(
                settings.deepWaterColor,
                settings.shelfWaterColor,
                deep01
            );
        }

        float landHeight = h - sea;

        if (landHeight <= settings.beachHeight01)
        {
            float t = Mathf.InverseLerp(0f, settings.beachHeight01, landHeight);
            return Color.Lerp(settings.beachColor, settings.lowlandColor, t);
        }

        float land01 = Mathf.InverseLerp(
            settings.beachHeight01,
            Mathf.Max(settings.beachHeight01 + 0.001f, 1f - sea),
            landHeight
        );

        if (land01 < 0.55f)
        {
            float t = Mathf.InverseLerp(0f, 0.55f, land01);
            return Color.Lerp(settings.lowlandColor, settings.highlandColor, t);
        }
        else
        {
            float t = Mathf.InverseLerp(0.55f, 1f, land01);
            return Color.Lerp(settings.highlandColor, settings.mountainColor, t);
        }
    }

    private static bool IsSeaLevelLine(float h, WorldMapTopographySettings settings)
    {
        float sea = Mathf.Clamp01(settings.seaLevel01);
        float thickness = Mathf.Max(0.0001f, settings.seaLevelLineThickness);

        return Mathf.Abs(h - sea) <= thickness;
    }

    private static bool IsContour(
        float h,
        WorldMapTopographySettings settings,
        out bool major)
    {
        major = false;

        int contourCount = Mathf.Max(1, settings.contourCount);
        float scaled = h * contourCount;

        float nearest = Mathf.Abs(scaled - Mathf.Round(scaled));
        float thickness = Mathf.Max(0.0001f, settings.contourThickness);

        if (nearest > thickness)
            return false;

        int contourIndex = Mathf.RoundToInt(scaled);
        int majorEvery = Mathf.Max(1, settings.majorContourEvery);

        major = contourIndex % majorEvery == 0;
        return true;
    }

    private static Color AlphaComposite(Color bottom, Color top)
    {
        float a = top.a + bottom.a * (1f - top.a);

        if (a <= 0.0001f)
            return new Color(0f, 0f, 0f, 0f);

        float r = (top.r * top.a + bottom.r * bottom.a * (1f - top.a)) / a;
        float g = (top.g * top.a + bottom.g * bottom.a * (1f - top.a)) / a;
        float b = (top.b * top.a + bottom.b * bottom.a * (1f - top.a)) / a;

        return new Color(r, g, b, a);
    }
}