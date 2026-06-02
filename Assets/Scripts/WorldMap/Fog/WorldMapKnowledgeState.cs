using System;
using UnityEngine;

public enum WorldMapKnowledgeLayer
{
    Surface,
    UnderwaterSurvey
}

[Serializable]
public sealed class WorldMapKnowledgeState
{
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private Rect worldBounds;

    [SerializeField] private bool[] surfaceRevealed;
    [SerializeField] private bool[] underwaterSurveyed;

    public int Width => width;
    public int Height => height;
    public Rect WorldBounds => worldBounds;

    public bool IsValid =>
        width > 0 &&
        height > 0 &&
        surfaceRevealed != null &&
        underwaterSurveyed != null &&
        surfaceRevealed.Length == width * height &&
        underwaterSurveyed.Length == width * height;

    public float SurfaceReveal01 => WorldMapKnowledgeBitCodec.PercentRevealed(surfaceRevealed);
    public float UnderwaterSurvey01 => WorldMapKnowledgeBitCodec.PercentRevealed(underwaterSurveyed);

    public void Initialize(int gridWidth, int gridHeight, Rect bounds)
    {
        width = Mathf.Max(1, gridWidth);
        height = Mathf.Max(1, gridHeight);
        worldBounds = bounds;

        int count = width * height;
        surfaceRevealed = new bool[count];
        underwaterSurveyed = new bool[count];
    }

    public void RevealAll(WorldMapKnowledgeLayer layer)
    {
        bool[] bits = GetBits(layer);
        if (bits == null)
            return;

        for (int i = 0; i < bits.Length; i++)
            bits[i] = true;
    }

    public void ClearLayer(WorldMapKnowledgeLayer layer)
    {
        bool[] bits = GetBits(layer);
        if (bits == null)
            return;

        for (int i = 0; i < bits.Length; i++)
            bits[i] = false;
    }

    public void ClearAll()
    {
        ClearLayer(WorldMapKnowledgeLayer.Surface);
        ClearLayer(WorldMapKnowledgeLayer.UnderwaterSurvey);
    }

    public void RevealCircleWorld(WorldMapKnowledgeLayer layer, Vector2 worldCenter, float radiusWorld, bool surfaceRevealImplied = true)
    {
        if (!IsValid)
            return;

        radiusWorld = Mathf.Max(0.01f, radiusWorld);

        int minX;
        int maxX;
        int minY;
        int maxY;

        WorldCircleToCellBounds(worldCenter, radiusWorld, out minX, out maxX, out minY, out maxY);

        float radiusSqr = radiusWorld * radiusWorld;

        bool[] bits = GetBits(layer);
        bool[] surfaceBits = surfaceRevealed;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 cellCenter = CellCenterWorld(x, y);
                if ((cellCenter - worldCenter).sqrMagnitude > radiusSqr)
                    continue;

                int i = Index(x, y);
                bits[i] = true;

                if (layer == WorldMapKnowledgeLayer.UnderwaterSurvey && surfaceRevealImplied)
                    surfaceBits[i] = true;
            }
        }
    }

    public bool IsRevealed(WorldMapKnowledgeLayer layer, Vector2 worldPosition)
    {
        if (!IsValid)
            return false;

        if (!TryWorldToCell(worldPosition, out int x, out int y))
            return false;

        return IsCellRevealed(layer, x, y);
    }

    public bool IsCellRevealed(WorldMapKnowledgeLayer layer, int x, int y)
    {
        if (!IsValid)
            return false;

        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        bool[] bits = GetBits(layer);
        return bits != null && bits[Index(x, y)];
    }

    public Rect CellWorldRect(int x, int y)
    {
        float cellW = worldBounds.width / Mathf.Max(1, width);
        float cellH = worldBounds.height / Mathf.Max(1, height);

        return new Rect(
            worldBounds.xMin + x * cellW,
            worldBounds.yMin + y * cellH,
            cellW,
            cellH
        );
    }

    public void CopyToSnapshot(WorldMapKnowledgeSaveSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        snapshot.EnsureDefaults();

        snapshot.gridWidth = width;
        snapshot.gridHeight = height;

        snapshot.worldBoundsX = worldBounds.x;
        snapshot.worldBoundsY = worldBounds.y;
        snapshot.worldBoundsWidth = worldBounds.width;
        snapshot.worldBoundsHeight = worldBounds.height;

        snapshot.surfaceEncoding = WorldMapKnowledgeBitCodec.BitBase64Encoding;
        snapshot.surfaceBitsBase64 = WorldMapKnowledgeBitCodec.Encode(surfaceRevealed);
        snapshot.surfaceRevealedCount = WorldMapKnowledgeBitCodec.CountRevealed(surfaceRevealed);

        snapshot.underwaterEncoding = WorldMapKnowledgeBitCodec.BitBase64Encoding;
        snapshot.underwaterBitsBase64 = WorldMapKnowledgeBitCodec.Encode(underwaterSurveyed);
        snapshot.underwaterSurveyedCount = WorldMapKnowledgeBitCodec.CountRevealed(underwaterSurveyed);
    }

    public bool TryRestoreFromSnapshot(WorldMapKnowledgeSaveSnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        int w = Mathf.Max(0, snapshot.gridWidth);
        int h = Mathf.Max(0, snapshot.gridHeight);

        if (w <= 0 || h <= 0)
            return false;

        Rect bounds = new Rect(
            snapshot.worldBoundsX,
            snapshot.worldBoundsY,
            snapshot.worldBoundsWidth,
            snapshot.worldBoundsHeight
        );

        if (bounds.width <= 0f || bounds.height <= 0f)
            return false;

        Initialize(w, h, bounds);

        int count = w * h;

        surfaceRevealed = WorldMapKnowledgeBitCodec.Decode(snapshot.surfaceBitsBase64, count);
        underwaterSurveyed = WorldMapKnowledgeBitCodec.Decode(snapshot.underwaterBitsBase64, count);

        return IsValid;
    }

    public bool TryWorldToCell(Vector2 worldPosition, out int x, out int y)
    {
        x = -1;
        y = -1;

        if (!IsValid)
            return false;

        if (!worldBounds.Contains(worldPosition))
            return false;

        float u = Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, worldPosition.x);
        float v = Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, worldPosition.y);

        x = Mathf.Clamp(Mathf.FloorToInt(u * width), 0, width - 1);
        y = Mathf.Clamp(Mathf.FloorToInt(v * height), 0, height - 1);

        return true;
    }

    private void WorldCircleToCellBounds(Vector2 center, float radius, out int minX, out int maxX, out int minY, out int maxY)
    {
        Vector2 min = center - new Vector2(radius, radius);
        Vector2 max = center + new Vector2(radius, radius);

        WorldToCellClamped(min, out minX, out minY);
        WorldToCellClamped(max, out maxX, out maxY);

        if (minX > maxX)
            (minX, maxX) = (maxX, minX);

        if (minY > maxY)
            (minY, maxY) = (maxY, minY);
    }

    private void WorldToCellClamped(Vector2 worldPosition, out int x, out int y)
    {
        float u = Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, worldPosition.x);
        float v = Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, worldPosition.y);

        x = Mathf.Clamp(Mathf.FloorToInt(u * width), 0, width - 1);
        y = Mathf.Clamp(Mathf.FloorToInt(v * height), 0, height - 1);
    }

    private Vector2 CellCenterWorld(int x, int y)
    {
        Rect r = CellWorldRect(x, y);
        return r.center;
    }

    private int Index(int x, int y)
    {
        return y * width + x;
    }

    private bool[] GetBits(WorldMapKnowledgeLayer layer)
    {
        return layer == WorldMapKnowledgeLayer.Surface
            ? surfaceRevealed
            : underwaterSurveyed;
    }
}
