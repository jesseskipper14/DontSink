using System;
using UnityEngine;

/// <summary>Canonical global geography: periodic X, finite Y. Never apply to Rigidbody coordinates.</summary>
public readonly struct WorldTopology
{
    public const int GenerationVersion = 3;
    public readonly Rect Bounds;
    public bool IsValid => IsFinite(Bounds.x) && IsFinite(Bounds.y) && IsFinite(Bounds.width) &&
        IsFinite(Bounds.height) && Bounds.width > 0f && Bounds.height > 0f;
    public WorldTopology(Rect bounds) { Bounds = bounds; }
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public float NormalizeX(float x)
    {
        if (!IsValid || !IsFinite(x)) return x;
        double value = ((double)x - Bounds.xMin) % Bounds.width;
        if (value < 0) value += Bounds.width;
        float result = (float)(Bounds.xMin + value);
        return result >= Bounds.xMax ? Bounds.xMin : result;
    }
    public Vector2 Normalize(Vector2 point) => new Vector2(NormalizeX(point.x), point.y);
    public float DeltaX(float from, float to)
    {
        if (!IsValid) return to - from;
        double delta = ((double)NormalizeX(to) - NormalizeX(from)) % Bounds.width;
        if (delta > Bounds.width * .5) delta -= Bounds.width;
        if (delta < -Bounds.width * .5) delta += Bounds.width;
        return (float)delta;
    }
    public Vector2 Delta(Vector2 from, Vector2 to) => new Vector2(DeltaX(from.x, to.x), to.y - from.y);
    public float Distance(Vector2 a, Vector2 b) => Delta(a, b).magnitude;
    public Vector2 Nearest(Vector2 target, Vector2 reference) => new Vector2(reference.x + DeltaX(reference.x, target.x), target.y);
    public bool ContainsY(float y) => IsValid && IsFinite(y) && y >= Bounds.yMin && y <= Bounds.yMax;
    public float ClampY(float y) => Mathf.Clamp(y, Bounds.yMin, Bounds.yMax);
    public float SouthIceStart(float bandFraction = .06f) => Bounds.yMin + Bounds.height * Mathf.Clamp(bandFraction, 0, .5f);
    public float NorthIceStart(float bandFraction = .06f) => Bounds.yMax - Bounds.height * Mathf.Clamp(bandFraction, 0, .5f);
    public static int WrapIndex(int index, int count) => count > 0 ? (index % count + count) % count : 0;
}

/// <summary>Bounds come from persisted geography or the published runtime field, not a guessed camera extent.</summary>
public static class WorldTopologyService
{
    private static Rect generationBounds;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => generationBounds = default;
    public static void UseSnapshot(WorldMapSaveSnapshot snapshot)
    {
        generationBounds = default;
        if (snapshot == null || !snapshot.HasPersistedWorld) return;
        Rect bounds = snapshot.topography != null ? snapshot.topography.ToWorldBounds() : default;
        if (!new WorldTopology(bounds).IsValid && snapshot.graph != null) bounds = snapshot.graph.worldBounds;
        ConfigureGenerationBounds(bounds);
    }
    public static void ConfigureGenerationBounds(Rect bounds) { if (new WorldTopology(bounds).IsValid) generationBounds = bounds; }
    public static WorldTopology Current
    {
        get
        {
            if (new WorldTopology(generationBounds).IsValid) return new WorldTopology(generationBounds);
            var snapshot = GameState.I != null ? GameState.I.worldMapSnapshot : null;
            if (snapshot != null && snapshot.HasPersistedWorld && snapshot.topography != null)
            {
                var saved = new WorldTopology(snapshot.topography.ToWorldBounds());
                if (saved.IsValid) return saved;
            }
            if (snapshot != null && snapshot.HasPersistedWorld && snapshot.graph != null &&
                new WorldTopology(snapshot.graph.worldBounds).IsValid)
                return new WorldTopology(snapshot.graph.worldBounds);
            var cache = WorldMapRuntimeCache.I;
            if (cache != null && cache.HasTopography) return new WorldTopology(cache.Field.WorldBounds);
            return new WorldTopology(generationBounds);
        }
    }
    public static Vector2 Normalize(Vector2 point) => Current.Normalize(point);
    public static Vector2 Delta(Vector2 from, Vector2 to) => Current.Delta(from, to);
    public static float Distance(Vector2 a, Vector2 b) => Current.Distance(a, b);
    public static Vector2 Nearest(Vector2 target, Vector2 reference) => Current.Nearest(target, reference);
}
