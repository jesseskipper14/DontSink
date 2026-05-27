using UnityEngine;

public static class WorldGenerationPreviewTextureBuilder
{
    public static Texture2D Build(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings,
        bool drawContours)
    {
        if (field == null || !field.IsValid || settings == null)
            return null;

        int width = Mathf.Max(16, field.Width);
        int height = Mathf.Max(16, field.Height);

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = settings.textureFilterMode,
            wrapMode = TextureWrapMode.Clamp,
            name = $"WorldGenPreview_{field.Seed}_{width}x{height}"
        };

        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = height <= 1 ? 0f : y / (float)(height - 1);

            for (int x = 0; x < width; x++)
            {
                float u = width <= 1 ? 0f : x / (float)(width - 1);
                float h = field.Sample01UV(u, v);

                Color c = EvaluateHeightColor(h, settings);

                if (drawContours)
                    ApplyContourAndCoastline(ref c, h, settings);

                c.a = 1f;
                pixels[y * width + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        return tex;
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

        {
            float t = Mathf.InverseLerp(0.55f, 1f, land01);
            return Color.Lerp(settings.highlandColor, settings.mountainColor, t);
        }
    }

    private static void ApplyContourAndCoastline(
        ref Color c,
        float h,
        WorldMapTopographySettings settings)
    {
        if (IsSeaLevelLine(h, settings))
        {
            Color coast = settings.seaLevelLineColor;
            c = Color.Lerp(c, coast, coast.a);
            c.a = 1f;
        }

        if (settings.drawContours && IsContour(h, settings, out bool major))
        {
            Color contour = major ? settings.majorContourColor : settings.contourColor;
            c = Color.Lerp(c, contour, contour.a);
            c.a = 1f;
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
}
