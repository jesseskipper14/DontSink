using System;
using UnityEngine;

/// <summary>Continuous center-path checks against bilinear geography and sampled water distance.
/// Detection has no dependency on boat physics, visuals, nodes, or cameras.</summary>
public sealed class BoatGeographicObstructionQuery
{
    private readonly WorldMapTopographyField _field;
    private readonly WorldTopology _topology;
    private readonly float _sea;
    private readonly int _nx, _ny;
    private readonly double _dx, _dy;
    private readonly float[] _distance;
    private readonly bool _hasWater;
    private readonly double[] _cuts = new double[6];

    public BoatGeographicObstructionQuery(WorldMapTopographyField field, float seaLevel)
    {
        if (field == null || !field.IsValid || field.Width < 2 || field.Height < 2 ||
            !new WorldTopology(field.WorldBounds).IsValid || !WorldTopology.IsFinite(seaLevel) || seaLevel <= 0 || seaLevel > 1)
            throw new ArgumentException("Obstruction requires valid topography and sea level.");
        _field = field; _sea = seaLevel; _topology = new WorldTopology(field.WorldBounds);
        _nx = field.Width - 1; _ny = field.Height;
        _dx = field.WorldBounds.width / (double)_nx; _dy = field.WorldBounds.height / (double)(_ny - 1);
        _distance = new float[_nx * _ny];
        double far = 16 * ((double)field.WorldBounds.width * field.WorldBounds.width +
            (double)field.WorldBounds.height * field.WorldBounds.height) + 1;
        var vertical = new double[_distance.Length];
        int capacity = Math.Max(_nx * 3, _ny);
        var input = new double[capacity]; var output = new double[capacity];
        var sites = new int[capacity]; var cuts = new double[capacity + 1];
        bool water = false;
        for (int x = 0; x < _nx; x++)
        {
            for (int y = 0; y < _ny; y++)
            {
                float height = field.Get01(x, y);
                if (!WorldTopology.IsFinite(height)) throw new ArgumentException("Topography contains invalid heights.");
                bool wet = height < seaLevel; water |= wet; input[y] = wet ? 0 : far;
            }
            Transform(input, output, _ny, _dy * _dy, sites, cuts);
            for (int y = 0; y < _ny; y++) vertical[y * _nx + x] = output[y];
        }
        _hasWater = water;
        // Three copies yield the shortest periodic X distance, without wrapping finite Y.
        for (int y = 0; y < _ny; y++)
        {
            for (int x = 0; x < _nx * 3; x++) input[x] = vertical[y * _nx + x % _nx];
            Transform(input, output, _nx * 3, _dx * _dx, sites, cuts);
            for (int x = 0; x < _nx; x++) _distance[y * _nx + x] = (float)Math.Sqrt(output[_nx + x]);
        }
    }

    // Linear-time squared Euclidean distance transform: lower envelope of parabolas.
    private static void Transform(double[] input, double[] output, int count, double spacingSquared, int[] sites, double[] cuts)
    {
        int top = 0; sites[0] = 0; cuts[0] = double.NegativeInfinity; cuts[1] = double.PositiveInfinity;
        for (int q = 1; q < count; q++)
        {
            double cross;
            while (true)
            {
                int p = sites[top];
                cross = ((input[q] - input[p]) / spacingSquared + (double)q * q - (double)p * p) / (2d * (q - p));
                if (cross > cuts[top] || top == 0) break;
                top--;
            }
            sites[++top] = q; cuts[top] = cross; cuts[top + 1] = double.PositiveInfinity;
        }
        top = 0;
        for (int q = 0; q < count; q++)
        {
            while (cuts[top + 1] < q) top++;
            double delta = q - sites[top]; output[q] = delta * delta * spacingSquared + input[sites[top]];
        }
    }

    public float Penetration(Vector2 point)
    {
        double gx = (_topology.NormalizeX(point.x) - _topology.Bounds.xMin) / _dx;
        double gy = Mathf.Clamp((point.y - _topology.Bounds.yMin) / (float)_dy, 0, _ny - 1);
        int x = (int)Math.Floor(gx), y = (int)Math.Floor(gy), y1 = Math.Min(y + 1, _ny - 1);
        int x0 = WorldTopology.WrapIndex(x, _nx), x1 = WorldTopology.WrapIndex(x + 1, _nx);
        float tx = (float)(gx - x), ty = (float)(gy - y);
        return Mathf.Lerp(Mathf.Lerp(_distance[y * _nx + x0], _distance[y * _nx + x1], tx),
            Mathf.Lerp(_distance[y1 * _nx + x0], _distance[y1 * _nx + x1], tx), ty);
    }

    /// <param name="delta">Explicit unwrapped attempted displacement, not shortest endpoint displacement.</param>
    public BoatObstructionResult Sweep(Vector2 start, Vector2 delta, float coastTolerance)
    {
        if (!WorldTopology.IsFinite(start.x) || !WorldTopology.IsFinite(delta.x) || !WorldTopology.IsFinite(delta.y) ||
            !_topology.ContainsY(start.y) || !_topology.ContainsY(start.y + delta.y) ||
            !WorldTopology.IsFinite(coastTolerance) || coastTolerance < 0) return default;
        start = _topology.Normalize(start);
        float initial = Penetration(start);
        if (delta.sqrMagnitude == 0) return new BoatObstructionResult(false, 1, initial);
        if (!_hasWater) return new BoatObstructionResult(true, 0, initial);
        double limit = Math.Max(coastTolerance, _field.Sample01World(start) >= _sea ? initial : 0) + .000001;
        double t = 0;
        var cuts = _cuts; // Single-owner runtime query: no per-sweep allocation.
        for (int cell = 0; cell < 8192 && t < 1; cell++)
        {
            double next = Math.Min(1, Math.Min(NextGrid(start.x, delta.x, _topology.Bounds.xMin, _dx, t),
                NextGrid(start.y, delta.y, _topology.Bounds.yMin, _dy, t)));
            if (next <= t) return new BoatObstructionResult(true, (float)t, initial);
            Vector2 a = start + delta * (float)t, b = start + delta * (float)next;
            Vector2 middle = start + delta * (float)((t + next) * .5);
            double h0 = _field.Sample01World(a) - _sea, hm = _field.Sample01World(middle) - _sea, h1 = _field.Sample01World(b) - _sea;
            double p0 = Penetration(a) - limit, pm = Penetration(middle) - limit, p1 = Penetration(b) - limit;
            double ha = 2 * (h1 + h0 - 2 * hm), hb = h1 - h0 - ha;
            double pa = 2 * (p1 + p0 - 2 * pm), pb = p1 - p0 - pa;
            int count = 2; cuts[0] = 0; cuts[1] = 1;
            Roots(ha, hb, h0, cuts, ref count); Roots(pa, pb, p0, cuts, ref count);
            Array.Sort(cuts, 0, count);
            for (int i = 0; i < count - 1; i++)
            {
                double u = (cuts[i] + cuts[i + 1]) * .5;
                if ((ha * u + hb) * u + h0 >= 0 && (pa * u + pb) * u + p0 > 0)
                    return new BoatObstructionResult(true, (float)(t + (next - t) * cuts[i]), initial);
            }
            t = next;
        }
        return new BoatObstructionResult(t < 1, (float)t, initial); // Path-budget overflow blocks conservatively.
    }

    private static double NextGrid(double start, double delta, double origin, double step, double t)
    {
        if (delta == 0) return double.PositiveInfinity;
        double grid = (start + delta * t - origin) / step;
        double next = delta > 0 ? Math.Floor(grid + 1e-9) + 1 : Math.Ceiling(grid - 1e-9) - 1;
        return (origin + next * step - start) / delta;
    }

    private static void Roots(double a, double b, double c, double[] cuts, ref int count)
    {
        if (Math.Abs(a) < 1e-10) { if (Math.Abs(b) > 1e-10) Add(-c / b, cuts, ref count); return; }
        double discriminant = b * b - 4 * a * c;
        if (discriminant < 0) return;
        double root = Math.Sqrt(discriminant);
        Add((-b - root) / (2 * a), cuts, ref count); Add((-b + root) / (2 * a), cuts, ref count);
    }
    private static void Add(double value, double[] cuts, ref int count) { if (value > 0 && value < 1) cuts[count++] = value; }
}

public readonly struct BoatObstructionResult
{
    public readonly bool HasGeography, Blocked;
    public readonly float AllowedFraction, InitialPenetration;
    public BoatObstructionResult(bool blocked, float allowedFraction, float initialPenetration)
    { HasGeography = true; Blocked = blocked; AllowedFraction = allowedFraction; InitialPenetration = initialPenetration; }
}
