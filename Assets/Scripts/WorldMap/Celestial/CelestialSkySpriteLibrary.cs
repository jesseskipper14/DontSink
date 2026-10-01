using System;
using UnityEngine;

/// <summary>
/// Tiny runtime-generated white sprite set used by CelestialSkyRenderer.
/// Color and scale are supplied by each SpriteRenderer, so no project art assets
/// are required for the Phase 3 proof-of-concept.
/// </summary>
public sealed class CelestialSkySpriteLibrary : IDisposable
{
    private const int AmbientResolution = 32;
    private const int SymbolResolution = 64;

    private Texture2D _ambientTexture;
    private Texture2D _nebulaTexture;
    private readonly Texture2D[] _landmarkTextures = new Texture2D[6];
    private readonly Texture2D[] _deepSkyTextures = new Texture2D[4];

    private Sprite _ambient;
    private Sprite _nebula;
    private readonly Sprite[] _landmarks = new Sprite[6];
    private readonly Sprite[] _deepSky = new Sprite[4];

    public CelestialSkySpriteLibrary()
    {
        Build();
    }

    public Sprite Resolve(CelestialObject obj)
    {
        if (obj == null)
            return _ambient;

        switch (obj.Kind)
        {
            case CelestialObjectKind.LandmarkStar:
                return _landmarks[Mathf.Abs(obj.VisualVariant) % _landmarks.Length];

            case CelestialObjectKind.Nebula:
                return _nebula;

            case CelestialObjectKind.DeepSkyObject:
                return _deepSky[Mathf.Abs(obj.VisualVariant) % _deepSky.Length];

            default:
                return _ambient;
        }
    }

    private void Build()
    {
        _ambientTexture = CreateTexture(AmbientResolution, "CelestialSky_Ambient", DrawSoftDisc);
        _ambient = CreateSprite(_ambientTexture, "CelestialSky_AmbientSprite");

        _nebulaTexture = CreateTexture(SymbolResolution, "CelestialSky_Nebula", DrawNebula);
        _nebula = CreateSprite(_nebulaTexture, "CelestialSky_NebulaSprite");

        for (int i = 0; i < _landmarks.Length; i++)
        {
            int variant = i;
            _landmarkTextures[i] = CreateTexture(
                SymbolResolution,
                $"CelestialSky_Landmark_{i}",
                (pixels, size) => DrawLandmark(pixels, size, variant));

            _landmarks[i] = CreateSprite(
                _landmarkTextures[i],
                $"CelestialSky_LandmarkSprite_{i}");
        }

        for (int i = 0; i < _deepSky.Length; i++)
        {
            int variant = i;
            _deepSkyTextures[i] = CreateTexture(
                SymbolResolution,
                $"CelestialSky_DeepSky_{i}",
                (pixels, size) => DrawDeepSky(pixels, size, variant));

            _deepSky[i] = CreateSprite(
                _deepSkyTextures[i],
                $"CelestialSky_DeepSkySprite_{i}");
        }
    }

    private static Texture2D CreateTexture(
        int size,
        string name,
        Action<Color32[], int> draw)
    {
        var pixels = new Color32[size * size];
        draw?.Invoke(pixels, size);

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return texture;
    }

    private static Sprite CreateSprite(Texture2D texture, string name)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width,
            0,
            SpriteMeshType.FullRect);

        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static void DrawSoftDisc(Color32[] pixels, int size)
    {
        float center = (size - 1) * 0.5f;
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                if (d > 1f)
                    continue;

                float alpha = Mathf.Pow(1f - d, 0.75f);
                SetMaxAlpha(pixels, size, x, y, alpha);
            }
        }
    }

    private static void DrawNebula(Color32[] pixels, int size)
    {
        float center = (size - 1) * 0.5f;
        float radius = size * 0.49f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - center) / radius;
                float ny = (y - center) / radius;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                if (d > 1f)
                    continue;

                float cloud = Mathf.Pow(1f - d, 1.8f);
                float inner = Mathf.Clamp01(1f - d * 2.6f) * 0.28f;
                float alpha = Mathf.Clamp01(cloud * 0.72f + inner);
                SetMaxAlpha(pixels, size, x, y, alpha);
            }
        }
    }

    private static void DrawLandmark(Color32[] pixels, int size, int variant)
    {
        DrawSoftDiscCore(pixels, size, size * 0.075f, 1f);

        int c = size / 2;
        int longRay = Mathf.RoundToInt(size * 0.43f);
        int shortRay = Mathf.RoundToInt(size * 0.29f);

        switch (variant % 6)
        {
            case 0:
                DrawLine(pixels, size, c - longRay, c, c + longRay, c, 0.78f);
                DrawLine(pixels, size, c, c - longRay, c, c + longRay, 0.78f);
                break;

            case 1:
                DrawLine(pixels, size, c - shortRay, c - shortRay, c + shortRay, c + shortRay, 0.82f);
                DrawLine(pixels, size, c - shortRay, c + shortRay, c + shortRay, c - shortRay, 0.82f);
                break;

            case 2:
                DrawLine(pixels, size, c - longRay, c, c + longRay, c, 0.72f);
                DrawLine(pixels, size, c, c - longRay, c, c + longRay, 0.72f);
                DrawLine(pixels, size, c - shortRay, c - shortRay, c + shortRay, c + shortRay, 0.58f);
                DrawLine(pixels, size, c - shortRay, c + shortRay, c + shortRay, c - shortRay, 0.58f);
                break;

            case 3:
                DrawRing(pixels, size, c, c, size * 0.25f, 1.2f, 0.88f);
                break;

            case 4:
                DrawDiamond(pixels, size, c, c, Mathf.RoundToInt(size * 0.29f), 0.86f);
                break;

            default:
                DrawLine(pixels, size, c - longRay, c, c + longRay, c, 0.67f);
                DrawLine(pixels, size, c, c - longRay, c, c + longRay, 0.67f);
                DrawRing(pixels, size, c, c, size * 0.21f, 1.1f, 0.72f);
                break;
        }
    }

    private static void DrawDeepSky(Color32[] pixels, int size, int variant)
    {
        int c = size / 2;
        float outer = size * 0.34f;
        float inner = size * 0.19f;

        switch (variant % 4)
        {
            case 0:
                DrawRing(pixels, size, c, c, outer, 1.5f, 0.92f);
                DrawSoftDiscCore(pixels, size, 2.0f, 0.95f);
                break;

            case 1:
                DrawDiamond(pixels, size, c, c, Mathf.RoundToInt(outer), 0.92f);
                DrawSoftDiscCore(pixels, size, 1.6f, 0.95f);
                break;

            case 2:
                DrawLine(pixels, size, c - Mathf.RoundToInt(outer), c, c + Mathf.RoundToInt(outer), c, 0.88f);
                DrawLine(pixels, size, c, c - Mathf.RoundToInt(outer), c, c + Mathf.RoundToInt(outer), 0.88f);
                DrawRing(pixels, size, c, c, inner, 1.2f, 0.75f);
                break;

            default:
                DrawRing(pixels, size, c, c, outer, 1.4f, 0.87f);
                DrawRing(pixels, size, c, c, inner, 1.0f, 0.68f);
                break;
        }
    }

    private static void DrawSoftDiscCore(Color32[] pixels, int size, float radius, float alpha)
    {
        int c = size / 2;
        int r = Mathf.Max(1, Mathf.CeilToInt(radius));

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                float d = Mathf.Sqrt(x * x + y * y) / Mathf.Max(0.001f, radius);
                if (d > 1f)
                    continue;

                SetMaxAlpha(pixels, size, c + x, c + y, alpha * (1f - d * 0.45f));
            }
        }
    }

    private static void DrawLine(Color32[] pixels, int size, int x0, int y0, int x1, int y1, float alpha)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            SetMaxAlpha(pixels, size, x0, y0, alpha);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = err * 2;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private static void DrawRing(Color32[] pixels, int size, int cx, int cy, float radius, float thickness, float alpha)
    {
        int r = Mathf.CeilToInt(radius + thickness + 1f);

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float edge = Mathf.Abs(d - radius);
                if (edge > thickness)
                    continue;

                float a = alpha * (1f - edge / Mathf.Max(0.0001f, thickness));
                SetMaxAlpha(pixels, size, cx + x, cy + y, a);
            }
        }
    }

    private static void DrawDiamond(Color32[] pixels, int size, int cx, int cy, int radius, float alpha)
    {
        radius = Mathf.Max(1, radius);
        for (int y = -radius; y <= radius; y++)
        {
            int x = radius - Mathf.Abs(y);
            SetMaxAlpha(pixels, size, cx - x, cy + y, alpha);
            SetMaxAlpha(pixels, size, cx + x, cy + y, alpha);
        }
    }

    private static void SetMaxAlpha(Color32[] pixels, int size, int x, int y, float alpha01)
    {
        if (x < 0 || y < 0 || x >= size || y >= size)
            return;

        int index = y * size + x;
        byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha01) * 255f);
        if (alpha <= pixels[index].a)
            return;

        pixels[index] = new Color32(255, 255, 255, alpha);
    }

    public void Dispose()
    {
        DestroySprite(ref _ambient);
        DestroySprite(ref _nebula);

        for (int i = 0; i < _landmarks.Length; i++)
            DestroySprite(ref _landmarks[i]);

        for (int i = 0; i < _deepSky.Length; i++)
            DestroySprite(ref _deepSky[i]);

        DestroyTexture(ref _ambientTexture);
        DestroyTexture(ref _nebulaTexture);

        for (int i = 0; i < _landmarkTextures.Length; i++)
            DestroyTexture(ref _landmarkTextures[i]);

        for (int i = 0; i < _deepSkyTextures.Length; i++)
            DestroyTexture(ref _deepSkyTextures[i]);
    }

    private static void DestroySprite(ref Sprite sprite)
    {
        if (sprite != null)
            UnityEngine.Object.Destroy(sprite);
        sprite = null;
    }

    private static void DestroyTexture(ref Texture2D texture)
    {
        if (texture != null)
            UnityEngine.Object.Destroy(texture);
        texture = null;
    }
}
