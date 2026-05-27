using UnityEngine;

public sealed class WorldMapTopographyField
{
    private readonly float[] _height01;

    public int Seed { get; }
    public int Width { get; }
    public int Height { get; }
    public Rect WorldBounds { get; }

    public float MinRaw { get; }
    public float MaxRaw { get; }

    public WorldMapTopographyField(
        int seed,
        int width,
        int height,
        Rect worldBounds,
        float[] height01,
        float minRaw,
        float maxRaw)
    {
        Seed = seed;
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        WorldBounds = worldBounds;
        _height01 = height01;
        MinRaw = minRaw;
        MaxRaw = maxRaw;
    }

    public bool IsValid =>
        _height01 != null &&
        _height01.Length == Width * Height;

    public float Get01(int x, int y)
    {
        if (!IsValid)
            return 0f;

        x = Mathf.Clamp(x, 0, Width - 1);
        y = Mathf.Clamp(y, 0, Height - 1);

        return _height01[y * Width + x];
    }

    public float Sample01World(Vector2 worldPos)
    {
        if (!IsValid)
            return 0f;

        float u = Mathf.InverseLerp(WorldBounds.xMin, WorldBounds.xMax, worldPos.x);
        float v = Mathf.InverseLerp(WorldBounds.yMin, WorldBounds.yMax, worldPos.y);

        return Sample01UV(u, v);
    }

    public float Sample01UV(float u, float v)
    {
        if (!IsValid)
            return 0f;

        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        float gx = u * (Width - 1);
        float gy = v * (Height - 1);

        int x0 = Mathf.FloorToInt(gx);
        int y0 = Mathf.FloorToInt(gy);
        int x1 = Mathf.Min(x0 + 1, Width - 1);
        int y1 = Mathf.Min(y0 + 1, Height - 1);

        float tx = gx - x0;
        float ty = gy - y0;

        float a = Get01(x0, y0);
        float b = Get01(x1, y0);
        float c = Get01(x0, y1);
        float d = Get01(x1, y1);

        float ab = Mathf.Lerp(a, b, tx);
        float cd = Mathf.Lerp(c, d, tx);

        return Mathf.Lerp(ab, cd, ty);
    }

    public float[] CopyHeight01()
    {
        if (_height01 == null)
            return null;

        var copy = new float[_height01.Length];
        System.Array.Copy(_height01, copy, _height01.Length);
        return copy;
    }
}