using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Rolling noise is seed/strip stable; macro knots freeze geographic context on first commit.</summary>
public sealed class BoatTerrainPlan
{
    public readonly int Seed;
    public readonly float Width, WaterLevel, BottomY, InterestRadius, UnloadBuffer;
    public readonly int Segments;
    public float Step => Width / Segments;
    private readonly double _depth, _amplitude, _wavelength;
    private readonly float _maximumDepth;
    private readonly double _macroSlope;
    private readonly double _largeMacroSlope;
    private readonly float _largeDepthThreshold;
    private readonly Func<double, float> _desiredDepth;
    private readonly SortedList<long, float> _depthKnots = new();

    public BoatTerrainPlan(BoatTerrainProfile profile, int seed, Func<double, float> desiredDepth = null)
    {
        if (profile == null) throw new ArgumentException("A streamed terrain profile is required.");
        foreach (float value in new[] { profile.chunkWidth, profile.sampleSpacing, profile.waterLevelY,
            profile.baseDepth, profile.maximumDepth, profile.rollingAmplitude, profile.rollingWavelength,
            profile.maximumSlopeDegrees, profile.largeDepthChangeSlopeDegrees, profile.largeDepthChangeThreshold,
            profile.fillDepth, profile.interestRadius, profile.unloadBuffer })
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Streamed terrain profile contains a non-finite value.");
        Seed = seed;
        Width = Mathf.Clamp(profile.chunkWidth, 8f, 1024f);
        Segments = Mathf.Clamp(Mathf.CeilToInt(Width / Mathf.Max(.1f, profile.sampleSpacing)), 8, 1024);
        WaterLevel = profile.waterLevelY;
        float maximumDepth = Mathf.Clamp(profile.maximumDepth, 2f, 5000f);
        _maximumDepth = maximumDepth;
        _desiredDepth = desiredDepth;
        _depth = Mathf.Clamp(profile.baseDepth, 1f, maximumDepth);
        _wavelength = Mathf.Max(1f, profile.rollingWavelength);
        double slope = Math.Tan(Mathf.Clamp(profile.maximumSlopeDegrees, 1f, 45f) * Math.PI / 180);
        // Reserve half the gradient for macro changes, half for rolling detail.
        _macroSlope = slope * .5 / 1.875;
        double largeSlope = Math.Tan(Mathf.Clamp(Mathf.Max(profile.maximumSlopeDegrees, profile.largeDepthChangeSlopeDegrees), 1f, 45f) * Math.PI / 180);
        _largeMacroSlope = (largeSlope - slope * .5) / 1.875;
        _largeDepthThreshold = Mathf.Max(1f, profile.largeDepthChangeThreshold);
        _amplitude = Math.Min(Math.Max(0, profile.rollingAmplitude), slope * _wavelength / (desiredDepth != null ? 7.5 : 3.75));
        _amplitude = Math.Min(_amplitude, Math.Max(0, Math.Min(_depth - 1, maximumDepth - _depth)));
        BottomY = WaterLevel - maximumDepth - Mathf.Max(1, profile.fillDepth);
        InterestRadius = Mathf.Max(32f, profile.interestRadius);
        UnloadBuffer = Mathf.Max(8f, profile.unloadBuffer);
    }

    public long ChunkIndex(double strip) => (long)Math.Floor(strip / Width);
    public void CommitChunk(long index)
    {
        if (_desiredDepth == null) return;
        CommitKnot(index);
        CommitKnot(index + 1);
    }

    private void CommitKnot(long index)
    {
        if (_depthKnots.ContainsKey(index)) return;
        float target = _desiredDepth(index * (double)Width);
        if (!WorldTopology.IsFinite(target)) target = (float)_depth;
        target = Mathf.Clamp(target, 1f, _maximumDepth);
        double low = 1, high = _maximumDepth;
        int first = 0, last = _depthKnots.Count;
        while (first < last)
        {
            int middle = first + (last - first) / 2;
            if (_depthKnots.Keys[middle] < index) first = middle + 1;
            else last = middle;
        }
        // Constrain against the closest history in BOTH directions, including
        // disjoint swimmer/gear regions. Any later gap can then connect safely.
        for (int pass = 0; pass < 2; pass++)
        {
            low = 1; high = _maximumDepth;
            foreach (int neighbor in new[] { first - 1, first })
            {
                if (neighbor < 0 || neighbor >= _depthKnots.Count) continue;
                double mismatch = Math.Min(1, Math.Abs(target - _depthKnots.Values[neighbor]) / _largeDepthThreshold);
                double rate = pass == 0 ? _macroSlope + (_largeMacroSlope - _macroSlope) * mismatch : _largeMacroSlope;
                double allowance = Math.Abs((double)index - _depthKnots.Keys[neighbor]) * Width * rate;
                low = Math.Max(low, _depthKnots.Values[neighbor] - allowance);
                high = Math.Min(high, _depthKnots.Values[neighbor] + allowance);
            }
            if (low <= high) break;
            // Bridging existing steep history may require its larger safety cap.
        }
        _depthKnots.Add(index, (float)Math.Max(low, Math.Min(high, target)));
    }

    private double MacroDepth(double strip)
    {
        long index = ChunkIndex(strip);
        if (strip == index * (double)Width && _depthKnots.TryGetValue(index, out float boundary))
            return boundary;
        if (_depthKnots.TryGetValue(index, out float a) && _depthKnots.TryGetValue(index + 1, out float b))
        {
            double t = strip / Width - index;
            double smooth = t * t * t * (t * (t * 6 - 15) + 10);
            return a + (b - a) * smooth;
        }
        // Forecast is never built as physical geometry until both knots commit.
        return _depth;
    }

    public float Height(double strip)
    {
        double cell = Math.Floor(strip / _wavelength);
        double t = strip / _wavelength - cell;
        // Quintic interpolation gives matching height and derivative across noise cells.
        double smooth = t * t * t * (t * (t * 6 - 15) + 10);
        double noise = Noise((long)cell) * (1 - smooth) + Noise((long)cell + 1) * smooth;
        double depth = _desiredDepth != null ? MacroDepth(strip) : _depth;
        return (float)(WaterLevel - Math.Max(1, Math.Min(_maximumDepth, depth - noise * _amplitude)));
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
        CommitChunk(index);
        var points = new Vector2[Segments + 1];
        for (int i = 0; i <= Segments; i++)
        {
            double strip = i == Segments ? (index + 1d) * Width : index * (double)Width + i * ((double)Width / Segments);
            points[i] = new Vector2((float)(i * ((double)Width / Segments)), Height(strip));
        }
        return points;
    }
}
