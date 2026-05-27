using UnityEngine;

public sealed class WorldMapBiomeLayer
{
    private readonly int[] _biomeIndices;

    public int Width { get; }
    public int Height { get; }
    public Rect WorldBounds { get; }
    public WorldMapBiomeCatalog Catalog { get; }

    public bool IsValid =>
        Width > 0 &&
        Height > 0 &&
        _biomeIndices != null &&
        _biomeIndices.Length == Width * Height &&
        Catalog != null;

    public WorldMapBiomeLayer(
        int width,
        int height,
        Rect worldBounds,
        WorldMapBiomeCatalog catalog,
        int[] biomeIndices)
    {
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        WorldBounds = worldBounds;
        Catalog = catalog;
        _biomeIndices = biomeIndices;
    }

    public int GetBiomeIndex(int x, int y)
    {
        if (!IsValid)
            return -1;

        x = Mathf.Clamp(x, 0, Width - 1);
        y = Mathf.Clamp(y, 0, Height - 1);

        return _biomeIndices[y * Width + x];
    }

    public int GetBiomeIndexUV(float u, float v)
    {
        if (!IsValid)
            return -1;

        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        int x = Mathf.RoundToInt(u * (Width - 1));
        int y = Mathf.RoundToInt(v * (Height - 1));

        return GetBiomeIndex(x, y);
    }

    public int GetBiomeIndexWorld(Vector2 worldPos)
    {
        if (!IsValid)
            return -1;

        float u = Mathf.InverseLerp(WorldBounds.xMin, WorldBounds.xMax, worldPos.x);
        float v = Mathf.InverseLerp(WorldBounds.yMin, WorldBounds.yMax, worldPos.y);

        return GetBiomeIndexUV(u, v);
    }

    public WorldMapBiomeDef GetBiomeDef(int x, int y)
    {
        return Catalog != null ? Catalog.GetBiome(GetBiomeIndex(x, y)) : null;
    }

    public WorldMapBiomeDef GetBiomeDefWorld(Vector2 worldPos)
    {
        return Catalog != null ? Catalog.GetBiome(GetBiomeIndexWorld(worldPos)) : null;
    }
}