using System;
using UnityEngine;

/// <summary>Explicit coverage in the shared world's grid; arbitrary masks are unioned, never replaced.</summary>
[Serializable]
public sealed class WorldMapCoverageMask
{
    public int width, height;
    public Rect worldBounds;
    public bool[] cells;
    public bool IsValid => width > 0 && height > 0 && (long)width * height <= 1048576 &&
        new WorldTopology(worldBounds).IsValid && cells != null && cells.Length == (long)width * height;

    public static WorldMapCoverageMask Rasterize(int width, int height, Rect bounds, Func<Vector2, bool> contains)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 1048576 || !new WorldTopology(bounds).IsValid || contains == null)
            throw new ArgumentException("Invalid cartographic mask dimensions, bounds or shape.");
        var mask = new WorldMapCoverageMask { width = width, height = height, worldBounds = bounds, cells = new bool[width * height] };
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            mask.cells[y * width + x] = contains(new Vector2(bounds.xMin + (x + .5f) * bounds.width / width, bounds.yMin + (y + .5f) * bounds.height / height));
        return mask;
    }

    public static WorldMapCoverageMask Circle(int w, int h, Rect bounds, Vector2 center, float radius)
    {
        if (!Finite(center) || !WorldTopology.IsFinite(radius) || radius <= 0) throw new ArgumentException("Invalid circle.");
        var topology = new WorldTopology(bounds);
        return Rasterize(w, h, bounds, p => topology.Delta(center, p).sqrMagnitude <= radius * radius);
    }
    public static WorldMapCoverageMask Rectangle(int w, int h, Rect bounds, Rect region)
    {
        if (!new WorldTopology(region).IsValid) throw new ArgumentException("Invalid rectangle.");
        var topology = new WorldTopology(bounds);
        return Rasterize(w, h, bounds, p => p.y >= region.yMin && p.y <= region.yMax &&
            (region.width >= bounds.width || Mathf.Abs(topology.DeltaX(region.center.x, p.x)) <= region.width * .5f));
    }
    public static WorldMapCoverageMask Polygon(int w, int h, Rect bounds, Vector2[] vertices)
    {
        if (vertices == null || vertices.Length < 3) throw new ArgumentException("Polygon needs three vertices.");
        var topology = new WorldTopology(bounds);
        var points = (Vector2[])vertices.Clone();
        for (int i = 0; i < points.Length; i++) { if (!Finite(points[i])) throw new ArgumentException("Invalid vertex."); if (i > 0) points[i] = topology.Nearest(points[i], points[i - 1]); }
        return Rasterize(w, h, bounds, p => {
            p = topology.Nearest(p, points[0]); bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                if ((points[i].y > p.y) != (points[j].y > p.y) &&
                    p.x < (points[j].x - points[i].x) * (p.y - points[i].y) / (points[j].y - points[i].y) + points[i].x) inside = !inside;
            return inside;
        });
    }
    public static WorldMapCoverageMask Corridor(int w, int h, Rect bounds, Vector2[] path, float radius)
    {
        if (path == null || path.Length < 2 || !WorldTopology.IsFinite(radius) || radius <= 0) throw new ArgumentException("Invalid corridor.");
        var points = (Vector2[])path.Clone();
        foreach (var point in points) if (!Finite(point)) throw new ArgumentException("Invalid path point.");
        var topology = new WorldTopology(bounds);
        return Rasterize(w, h, bounds, p => {
            for (int i = 1; i < points.Length; i++) {
                Vector2 a = points[i - 1], b = topology.Nearest(points[i], a), q = topology.Nearest(p, a), ab = b - a;
                float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0;
                if ((q - (a + t * ab)).sqrMagnitude <= radius * radius) return true;
            }
            return false;
        });
    }
    public bool Contains(Vector2 p)
    {
        if (!IsValid || !Finite(p) || !new WorldTopology(worldBounds).ContainsY(p.y)) return false;
        p = new WorldTopology(worldBounds).Normalize(p);
        int x = Mathf.Clamp((int)((p.x - worldBounds.xMin) / worldBounds.width * width), 0, width - 1);
        int y = Mathf.Clamp((int)((p.y - worldBounds.yMin) / worldBounds.height * height), 0, height - 1);
        return cells[y * width + x];
    }
    private static bool Finite(Vector2 p) => WorldTopology.IsFinite(p.x) && WorldTopology.IsFinite(p.y);
}

/// <summary>Trusted authoritative source payload. No chart inventory consumption in this foundation.</summary>
[Serializable]
public sealed class WorldMapCartographicPayload
{
    public string sourceId;
    public WorldMapCoverageMask surface, bathymetry;
    public string[] nodeIds, surfacePoiIds, underwaterPoiIds;
}
