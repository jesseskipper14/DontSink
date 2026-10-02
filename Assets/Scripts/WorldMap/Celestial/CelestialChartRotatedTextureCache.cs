using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Board-local presentation cache that bakes placement rotation into ordinary axis-aligned
/// textures. This avoids IMGUI's rotated-matrix clipping leak, where a zoomed scrap could
/// escape the board viewport and draw across the rest of the screen.
///
/// Only one rotated presentation per fragment is retained. During free rotation the cache
/// replaces that one presentation as the preview angle changes; committed placements then
/// become effectively free to draw.
/// </summary>
public sealed class CelestialChartRotatedTextureCache : IDisposable
{
    private const int MaxRotatedTextureDimension = 1536;
    private const float AngleQuantizationDegrees = 0.25f;

    public sealed class RotatedVisual : IDisposable
    {
        public Texture2D PaperTexture { get; internal set; }
        public Texture2D InkTexture { get; internal set; }
        public Vector2 WorldSize { get; internal set; }
        public float RotationDegrees { get; internal set; }
        public bool UsesSourceTextures { get; internal set; }

        public void Dispose()
        {
            if (UsesSourceTextures)
                return;

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

    private sealed class Entry
    {
        public float angleKey;
        public CelestialChartFragmentVisual source;
        public RotatedVisual visual;
    }

    private readonly Dictionary<string, Entry> _entries =
        new(StringComparer.Ordinal);

    public RotatedVisual Get(
        string fragmentId,
        CelestialChartFragmentVisual source,
        float rotationDegrees)
    {
        if (source == null || string.IsNullOrWhiteSpace(fragmentId))
            return null;

        float angleKey = QuantizeAngle(rotationDegrees);

        if (_entries.TryGetValue(fragmentId, out Entry existing) &&
            existing != null &&
            ReferenceEquals(existing.source, source) &&
            Mathf.Abs(Mathf.DeltaAngle(existing.angleKey, angleKey)) <= 0.001f &&
            existing.visual != null)
        {
            return existing.visual;
        }

        if (existing != null)
            existing.visual?.Dispose();

        RotatedVisual built = Build(source, angleKey);
        _entries[fragmentId] = new Entry
        {
            angleKey = angleKey,
            source = source,
            visual = built
        };

        return built;
    }

    public void Dispose()
    {
        foreach (KeyValuePair<string, Entry> pair in _entries)
            pair.Value?.visual?.Dispose();

        _entries.Clear();
    }

    private static RotatedVisual Build(
        CelestialChartFragmentVisual source,
        float rotationDegrees)
    {
        float sourceWorldW = Mathf.Max(0.001f, source.RotatedCelestialBounds.width);
        float sourceWorldH = Mathf.Max(0.001f, source.RotatedCelestialBounds.height);

        float radians = rotationDegrees * Mathf.Deg2Rad;
        float absC = Mathf.Abs(Mathf.Cos(radians));
        float absS = Mathf.Abs(Mathf.Sin(radians));

        float outputWorldW = sourceWorldW * absC + sourceWorldH * absS;
        float outputWorldH = sourceWorldW * absS + sourceWorldH * absC;

        if (Mathf.Abs(Mathf.DeltaAngle(0f, rotationDegrees)) <= 0.001f)
        {
            return new RotatedVisual
            {
                PaperTexture = source.PaperTexture,
                InkTexture = source.InkTexture,
                WorldSize = new Vector2(sourceWorldW, sourceWorldH),
                RotationDegrees = 0f,
                UsesSourceTextures = true
            };
        }

        float pxPerWorld = Mathf.Max(0.25f, source.PixelsPerCelestialWorldUnit);
        int desiredW = Mathf.Max(8, Mathf.CeilToInt(outputWorldW * pxPerWorld));
        int desiredH = Mathf.Max(8, Mathf.CeilToInt(outputWorldH * pxPerWorld));

        float scale = 1f;
        if (desiredW > MaxRotatedTextureDimension || desiredH > MaxRotatedTextureDimension)
        {
            scale = Mathf.Min(
                MaxRotatedTextureDimension / (float)desiredW,
                MaxRotatedTextureDimension / (float)desiredH);
        }

        int outputW = Mathf.Max(8, Mathf.CeilToInt(desiredW * scale));
        int outputH = Mathf.Max(8, Mathf.CeilToInt(desiredH * scale));

        return new RotatedVisual
        {
            PaperTexture = RotateTexture(
                source.PaperTexture,
                sourceWorldW,
                sourceWorldH,
                outputWorldW,
                outputWorldH,
                rotationDegrees,
                outputW,
                outputH,
                $"RotChartPaper_{rotationDegrees:0.00}"),
            InkTexture = RotateTexture(
                source.InkTexture,
                sourceWorldW,
                sourceWorldH,
                outputWorldW,
                outputWorldH,
                rotationDegrees,
                outputW,
                outputH,
                $"RotChartInk_{rotationDegrees:0.00}"),
            WorldSize = new Vector2(outputWorldW, outputWorldH),
            RotationDegrees = rotationDegrees,
            UsesSourceTextures = false
        };
    }

    private static Texture2D RotateTexture(
        Texture2D source,
        float sourceWorldW,
        float sourceWorldH,
        float outputWorldW,
        float outputWorldH,
        float angleDegrees,
        int outputW,
        int outputH,
        string name)
    {
        if (source == null)
            return null;

        Color32[] sourcePixels = source.GetPixels32();
        int sourceW = source.width;
        int sourceH = source.height;
        Color32[] output = new Color32[outputW * outputH];

        // GUI/screen Y grows downward, so positive board rotation is visually clockwise.
        // Texture sampling uses Y-up UV space; inverse-map with +angle to reproduce
        // the same on-screen direction as GUIUtility.RotateAroundPivot.
        float radians = angleDegrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(radians);
        float s = Mathf.Sin(radians);

        for (int y = 0; y < outputH; y++)
        {
            float outY = (((y + 0.5f) / outputH) - 0.5f) * outputWorldH;

            for (int x = 0; x < outputW; x++)
            {
                float outX = (((x + 0.5f) / outputW) - 0.5f) * outputWorldW;

                float srcXWorld = outX * c - outY * s;
                float srcYWorld = outX * s + outY * c;

                float u = srcXWorld / sourceWorldW + 0.5f;
                float v = srcYWorld / sourceWorldH + 0.5f;

                if (u < 0f || u > 1f || v < 0f || v > 1f)
                    continue;

                int sx = Mathf.Clamp(Mathf.RoundToInt(u * (sourceW - 1)), 0, sourceW - 1);
                int sy = Mathf.Clamp(Mathf.RoundToInt(v * (sourceH - 1)), 0, sourceH - 1);
                output[y * outputW + x] = sourcePixels[sy * sourceW + sx];
            }
        }

        Texture2D result = new Texture2D(outputW, outputH, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        result.SetPixels32(output);
        result.Apply(false, false);
        return result;
    }

    private static float QuantizeAngle(float angleDegrees)
    {
        float normalized = Mathf.DeltaAngle(0f, angleDegrees);
        return Mathf.Round(normalized / AngleQuantizationDegrees) * AngleQuantizationDegrees;
    }
}
