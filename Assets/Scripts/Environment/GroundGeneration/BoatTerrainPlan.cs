using System;
using UnityEngine;

/// <summary>Frozen voyage plan. Sample identity never depends on chunk load order or other RNG channels.</summary>
public sealed class BoatTerrainPlan
{
    public readonly int Seed;
    public readonly float Width, WaterLevel, BottomY, InterestRadius, UnloadBuffer;
    public readonly int Segments;
    public float Step => Width / Segments;
    private readonly double _depth, _amplitude, _wavelength;

    public BoatTerrainPlan(BoatTerrainProfile profile, int seed)
    {
        if (profile == null) throw new ArgumentException("A streamed terrain profile is required.");
        foreach (float value in new[] { profile.chunkWidth, profile.sampleSpacing, profile.waterLevelY,
            profile.baseDepth, profile.maximumDepth, profile.rollingAmplitude, profile.rollingWavelength,
            profile.maximumSlopeDegrees, profile.fillDepth, profile.interestRadius, profile.unloadBuffer })
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Streamed terrain profile contains a non-finite value.");
        Seed = seed;
        Width = Mathf.Clamp(profile.chunkWidth, 8f, 1024f);
        Segments = Mathf.Clamp(Mathf.CeilToInt(Width / Mathf.Max(.1f, profile.sampleSpacing)), 8, 1024);
        WaterLevel = profile.waterLevelY;
        float maximumDepth = Mathf.Clamp(profile.maximumDepth, 2f, 5000f);
        _depth = Mathf.Clamp(profile.baseDepth, 1f, maximumDepth);
        _wavelength = Mathf.Max(1f, profile.rollingWavelength);
        double slope = Math.Tan(Mathf.Clamp(profile.maximumSlopeDegrees, 1f, 45f) * Math.PI / 180);
        _amplitude = Math.Min(Math.Max(0, profile.rollingAmplitude), slope * _wavelength / 3.75);
        _amplitude = Math.Min(_amplitude, Math.Max(0, Math.Min(_depth - 1, maximumDepth - _depth)));
        BottomY = WaterLevel - maximumDepth - Mathf.Max(1, profile.fillDepth);
        InterestRadius = Mathf.Max(32f, profile.interestRadius);
        UnloadBuffer = Mathf.Max(8f, profile.unloadBuffer);
    }

    public long ChunkIndex(double strip) => (long)Math.Floor(strip / Width);
    public float Height(double strip)
    {
        double cell = Math.Floor(strip / _wavelength);
        double t = strip / _wavelength - cell;
        // Quintic interpolation gives matching height and derivative across noise cells.
        double smooth = t * t * t * (t * (t * 6 - 15) + 10);
        double noise = Noise((long)cell) * (1 - smooth) + Noise((long)cell + 1) * smooth;
        return (float)(WaterLevel - _depth + noise * _amplitude);
    }

    private double Noise(long cell)
    {
        unchecked
        {
            ulong h = (ulong)cell ^ (uint)Seed ^ 0x5445525241494eUL;
            h = (h ^ (h >> 30)) * 0xbf58476d1ce4e5b9UL;
            h = (h ^ (h >> 27)) * 0x94d049bb133111ebUL;
            h ^= h >> 31;
            return (h >> 11) * (1d / 9007199254740992d) * 2 - 1;
        }
    }

    public Vector2[] BuildSurface(long index)
    {
        var points = new Vector2[Segments + 1];
        for (int i = 0; i <= Segments; i++)
        {
            double strip = i == Segments ? (index + 1d) * Width : index * (double)Width + i * ((double)Width / Segments);
            points[i] = new Vector2((float)(i * ((double)Width / Segments)), Height(strip));
        }
        return points;
    }
}
