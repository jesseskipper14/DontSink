using System;
using UnityEngine;

/// <summary>Read-only geographic truth plus sampled landmass identity. Never a physical collider.</summary>
public sealed class BoatGeographicLandQuery
{
    private readonly WorldMapTopographyField _field;
    private readonly WorldTopology _topology;
    private readonly float _seaLevel, _stepX, _stepY;
    private readonly int _width, _height;
    private readonly int[] _ids;
    private readonly float[] _areas;
    public int LandmassCount { get; private set; }
    public float SampleSpacing => Mathf.Max(_stepX, _stepY);

    public BoatGeographicLandQuery(WorldMapTopographyField field, float seaLevel)
    {
        if (field == null || !field.IsValid || field.Width < 2 || field.Height < 2 ||
            !new WorldTopology(field.WorldBounds).IsValid || !WorldTopology.IsFinite(seaLevel) || seaLevel <= 0 || seaLevel > 1)
            throw new ArgumentException("Land queries require valid topography, bounds and sea level.");
        _field = field; _topology = new WorldTopology(field.WorldBounds); _seaLevel = seaLevel;
        // Cell centers avoid counting the duplicated periodic endpoint twice, including legacy fields.
        _width = field.Width - 1; _height = field.Height - 1;
        _stepX = field.WorldBounds.width / _width; _stepY = field.WorldBounds.height / _height;
        _ids = new int[_width * _height]; _areas = new float[_ids.Length + 1];
        for (int i = 0; i < _ids.Length; i++)
        {
            float value = field.Sample01World(Point(i % _width, i / _width));
            if (!WorldTopology.IsFinite(value)) throw new ArgumentException("Topography contains invalid heights.");
            _ids[i] = value >= seaLevel ? -1 : 0;
        }
        var queue = new int[_ids.Length];
        for (int start = 0; start < _ids.Length; start++)
        {
            if (_ids[start] != -1) continue;
            // Stable for this field: minimum cell index + 1, independent of boat/heading/camera.
            int id = start + 1, head = 0, tail = 1;
            _ids[start] = id; queue[0] = start; LandmassCount++;
            while (head < tail)
            {
                int cell = queue[head++], x = cell % _width, y = cell / _width;
                Enqueue(WorldTopology.WrapIndex(x - 1, _width) + y * _width);
                Enqueue(WorldTopology.WrapIndex(x + 1, _width) + y * _width);
                if (y > 0) Enqueue(cell - _width);
                if (y + 1 < _height) Enqueue(cell + _width);
            }
            _areas[id] = tail * _stepX * _stepY;
            void Enqueue(int cell)
            {
                if (_ids[cell] != -1) return;
                _ids[cell] = id; queue[tail++] = cell;
            }
        }
    }

    private Vector2 Point(int x, int y) => new Vector2(
        _topology.Bounds.xMin + (x + .5f) * _stepX, _topology.Bounds.yMin + (y + .5f) * _stepY);

    /// <summary>Exact classification using the same bilinear topography as depth. Invalid geography is unknown.</summary>
    public bool TryClassify(Vector2 position, out bool isLand)
    {
        isLand = false;
        if (!WorldTopology.IsFinite(position.x) || !_topology.ContainsY(position.y)) return false;
        float height = _field.Sample01World(position);
        if (!WorldTopology.IsFinite(height)) return false;
        isLand = height >= _seaLevel;
        return true;
    }

    /// <summary>Nearest sampled land, with size-based visibility and distance hysteresis. Map units throughout.</summary>
    public BoatLandEncounter Query(Vector2 position, float baseRange, float maximumRange, float sizeFactor,
        int previousId = 0, float switchMargin = 0)
    {
        if (!TryClassify(position, out bool onLand)) return default;
        if (!WorldTopology.IsFinite(baseRange) || !WorldTopology.IsFinite(maximumRange) ||
            !WorldTopology.IsFinite(sizeFactor) || !WorldTopology.IsFinite(switchMargin)) return default;
        maximumRange = Mathf.Max(0, maximumRange); baseRange = Mathf.Clamp(baseRange, 0, maximumRange);
        sizeFactor = Mathf.Max(0, sizeFactor); switchMargin = Mathf.Max(0, switchMargin);
        position = _topology.Normalize(position);
        int centerX = Mathf.FloorToInt((position.x - _topology.Bounds.xMin) / _stepX);
        // Clamp before integer conversion; even a huge caller range can only visit the finite grid once.
        int radiusX = Mathf.CeilToInt(Mathf.Min(maximumRange / _stepX + 1, _width));
        int firstX = centerX - radiusX, countX = Mathf.Min(_width, radiusX * 2 + 1);
        int firstY = Mathf.FloorToInt(Mathf.Max(0, (position.y - maximumRange - _topology.Bounds.yMin) / _stepY));
        int lastY = Mathf.FloorToInt(Mathf.Min(_height - 1, (position.y + maximumRange - _topology.Bounds.yMin) / _stepY));
        BoatLandEncounter best = new BoatLandEncounter(position, onLand), retained = best;
        for (int y = firstY; y <= lastY; y++)
        for (int column = 0; column < countX; column++)
        {
            int x = WorldTopology.WrapIndex(firstX + column, _width), id = _ids[y * _width + x];
            if (id == 0) continue;
            Vector2 point = Point(x, y), offset = _topology.Delta(position, point);
            float distance = offset.magnitude, diameter = 2 * Mathf.Sqrt(_areas[id] / Mathf.PI);
            float range = Mathf.Min(maximumRange, baseRange + diameter * sizeFactor);
            if (distance > range) continue;
            var candidate = new BoatLandEncounter(position, onLand, id, point, offset, distance, _areas[id], diameter, range);
            if (!best.HasLandmass || distance < best.Distance || distance == best.Distance && id < best.LandmassId) best = candidate;
            if (id == previousId && (!retained.HasLandmass || distance < retained.Distance)) retained = candidate;
        }
        if (retained.HasLandmass && retained.Distance <= best.Distance + switchMargin) return retained;
        return best;
    }
}

/// <summary>Transient encounter snapshot. IDs are scoped to the topography field, never node/save IDs.</summary>
public readonly struct BoatLandEncounter
{
    public readonly bool HasGeography, IsOnLand;
    public readonly int LandmassId;
    public readonly Vector2 BoatWorldPosition, NearestLandSample, OffsetToLand;
    public readonly float Distance, ApproximateArea, EquivalentDiameter, VisibilityRange;
    public bool HasLandmass => HasGeography && LandmassId > 0;
    public BoatLandEncounter(Vector2 position, bool onLand, int id = 0, Vector2 nearest = default,
        Vector2 offset = default, float distance = 0, float area = 0, float diameter = 0, float range = 0)
    {
        HasGeography = true; IsOnLand = onLand; BoatWorldPosition = position; LandmassId = id;
        NearestLandSample = nearest; OffsetToLand = offset; Distance = distance;
        ApproximateArea = area; EquivalentDiameter = diameter; VisibilityRange = range;
    }
}
