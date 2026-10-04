using System;
using UnityEngine;

[Serializable]
public sealed class HarborTravelSettings
{
    public BoatTerrainProfile terrainProfile;
    [Min(.01f)] public float nominalLocalTravelDistance = 2000;
    [Range(.01f, .5f)] public float coastalDepthBand = .12f;
    [Min(1)] public float berthLengthMultiplier = 2;
    [Min(.1f)] public float berthWidthMultiplier = .8f;
    [Min(2)] public float departureLengthMultiplier = 6;
    [Min(.1f)] public float depthClearance = 2;
    [Min(1)] public float guidanceRange = 8;
    [Min(1)] public float visibilityRange = 20;
}

/// <summary>Deterministic node-owned approach, independent of the visiting boat.</summary>
public readonly struct HarborDefinition
{
    public readonly Vector2 Position, Waterward;
    public readonly float WaterStart, CorridorEnd;
    public bool IsValid => Waterward.sqrMagnitude > .9f && CorridorEnd > WaterStart;
    public HarborDefinition(Vector2 position, Vector2 direction, float start, float end)
    { Position = position; Waterward = direction; WaterStart = start; CorridorEnd = end; }
}

public readonly struct HarborBerth
{
    public readonly HarborDefinition Harbor;
    public readonly Vector2 Center, Departure;
    public readonly float HalfLength, HalfWidth, RequiredDepth;
    public HarborBerth(HarborDefinition harbor, Vector2 center, Vector2 departure,
        float halfLength, float halfWidth, float requiredDepth)
    { Harbor = harbor; Center = center; Departure = departure; HalfLength = halfLength; HalfWidth = halfWidth; RequiredDepth = requiredDepth; }
    public bool Contains(Vector2 world, WorldTopology topology)
    {
        Vector2 delta = topology.Delta(Center, world);
        Vector2 right = new Vector2(Harbor.Waterward.y, -Harbor.Waterward.x);
        return Mathf.Abs(Vector2.Dot(delta, Harbor.Waterward)) <= HalfLength && Mathf.Abs(Vector2.Dot(delta, right)) <= HalfWidth;
    }
}

/// <summary>Full corridor water checks; no random search, navigation writes or scene dependencies.</summary>
public sealed class HarborGeometryQuery
{
    private readonly WorldMapTopographyField _field;
    private readonly float _sea;
    private readonly WorldTopology _topology;
    private readonly BoatGeographicObstructionQuery _sweep;
    public readonly float Step, SearchDistance;
    public HarborGeometryQuery(WorldMapTopographyField field, float sea)
    {
        if (field == null || !field.IsValid || sea <= 0 || sea >= 1 || !WorldTopology.IsFinite(sea))
            throw new ArgumentException("Harbor planning requires valid topography and sea level.");
        _field = field; _sea = sea; _topology = new WorldTopology(field.WorldBounds);
        float grid = Mathf.Min(field.WorldBounds.width / (field.Width - 1), field.WorldBounds.height / (field.Height - 1));
        SearchDistance = Mathf.Min(Mathf.Min(field.WorldBounds.width, field.WorldBounds.height) * .25f, Mathf.Max(16, grid * 48));
        Step = Mathf.Max(grid * .25f, SearchDistance / 2048);
        _sweep = new BoatGeographicObstructionQuery(field, sea);
    }
    private bool Water(Vector2 p) => _topology.ContainsY(p.y) && _field.Sample01World(p) < _sea - .0001f;

    public bool TrySolve(Vector2 nodePosition, out HarborDefinition harbor)
    {
        harbor = default;
        if (!_topology.ContainsY(nodePosition.y) || !WorldTopology.IsFinite(nodePosition.x)) return false;
        Vector2 position = _topology.Normalize(nodePosition);
        float best = float.NegativeInfinity;
        for (int heading = 0; heading < 72; heading++)
        {
            float angle = heading * 5 * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2 right = new Vector2(direction.y, -direction.x);
            float start = -1, end = 0, depthScore = 0;
            for (float distance = 0; distance <= SearchDistance; distance += Step)
            {
                Vector2 center = position + direction * distance;
                bool water = Water(center) && Water(center + right * (Step * 2)) && Water(center - right * (Step * 2));
                if (start < 0)
                {
                    if (water) start = distance;
                    // Existing saved nodes may lie slightly inland: allow a bounded
                    // initial shoreline connector, never a later land crossing.
                    else if (distance > SearchDistance * .5f) break;
                }
                else if (!water) break;
                if (start < 0) continue;
                end = distance;
                depthScore += Mathf.Clamp01((_sea - _field.Sample01World(center)) / _sea) * Step;
            }
            if (start < 0 || end - start < Step * 8) continue;
            for (int side = -1; side <= 1; side++)
            {
                Vector2 waterStart = position + direction * start + right * (side * Step * 2);
                var result = _sweep.Sweep(waterStart, direction * (end - start), 0);
                if (!result.HasGeography) { end = start; break; }
                if (result.Blocked) end = Mathf.Min(end, start + (end - start) * result.AllowedFraction - Step);
            }
            float score = end - start * 2 + depthScore * .1f;
            if (end - start >= Step * 8 && score > best)
            { best = score; harbor = new HarborDefinition(position, direction, start, end); }
        }
        return harbor.IsValid;
    }

    private bool Lane(HarborDefinition harbor, float from, float to, float halfWidth)
    {
        Vector2 right = new Vector2(harbor.Waterward.y, -harbor.Waterward.x);
        int lanes = Mathf.Max(2, Mathf.CeilToInt(halfWidth * 2 / Step));
        if (lanes > 256) return false;
        for (int lane = 0; lane <= lanes; lane++)
        {
            Vector2 start = harbor.Position + harbor.Waterward * from + right * Mathf.Lerp(-halfWidth, halfWidth, lane / (float)lanes);
            if (!Water(start)) return false;
            var sweep = _sweep.Sweep(start, harbor.Waterward * (to - from), 0);
            if (!sweep.HasGeography || sweep.Blocked) return false;
        }
        return true;
    }
    private bool Patch(HarborDefinition harbor, float offset, float halfLength, float halfWidth,
        float draft, HarborTravelSettings settings)
    {
        Vector2 right = new Vector2(harbor.Waterward.y, -harbor.Waterward.x);
        int along = Mathf.Max(2, Mathf.CeilToInt(halfLength * 2 / Step));
        int across = Mathf.Max(2, Mathf.CeilToInt(halfWidth * 2 / Step));
        if (along > 256 || across > 256) return false;
        for (int i = 0; i <= along; i++) for (int j = 0; j <= across; j++)
        {
            Vector2 p = harbor.Position + harbor.Waterward * (offset + Mathf.Lerp(-halfLength, halfLength, i / (float)along)) +
                right * Mathf.Lerp(-halfWidth, halfWidth, j / (float)across);
            if (!Water(p)) return false;
            float h = _field.Sample01World(p), normalized = Mathf.Clamp01((_sea - h) / _sea);
            float depth = settings.terrainProfile.geographicDepth.Evaluate(normalized);
            float y = BoatCoastalSurface.Target(h, _sea, 0, -depth, settings.terrainProfile.geographicDepth, 40, settings.coastalDepthBand);
            if (!WorldTopology.IsFinite(y) || -y < draft) return false;
        }
        return Lane(harbor, offset - halfLength, offset + halfLength, halfWidth);
    }

    public bool TryScale(HarborDefinition harbor, float boatLength, float physicalDraft, float mapScale,
        HarborTravelSettings settings, out HarborBerth berth, out string reason)
    {
        berth = default; reason = "No safe berth/departure corridor for this boat.";
        if (!harbor.IsValid || settings == null || settings.terrainProfile == null || settings.terrainProfile.geographicDepth == null ||
            settings.terrainProfile.geographicDepth.length == 0 ||
            boatLength <= 0 || mapScale <= 0 || !WorldTopology.IsFinite(boatLength) || !WorldTopology.IsFinite(mapScale) ||
            !WorldTopology.IsFinite(physicalDraft) || physicalDraft < 0) return false;
        foreach (float value in new[] { settings.berthLengthMultiplier, settings.berthWidthMultiplier, settings.departureLengthMultiplier,
            settings.depthClearance, settings.coastalDepthBand, settings.terrainProfile.rollingAmplitude })
            if (!WorldTopology.IsFinite(value)) return false;
        foreach (var key in settings.terrainProfile.geographicDepth.keys)
            if (!WorldTopology.IsFinite(key.time) || !WorldTopology.IsFinite(key.value)) return false;
        if (settings.coastalDepthBand < .01f || settings.coastalDepthBand > .5f || !settings.terrainProfile.useGeographicDepth) return false;
        float length = boatLength * mapScale;
        float halfLength = Mathf.Max(Step * 2, length * Mathf.Max(1, settings.berthLengthMultiplier) * .5f);
        float halfWidth = Mathf.Max(Step * 2, length * Mathf.Max(.1f, settings.berthWidthMultiplier) * .5f);
        float draft = physicalDraft + Mathf.Max(.1f, settings.depthClearance) + Mathf.Max(0, settings.terrainProfile.rollingAmplitude);
        for (float offset = harbor.WaterStart + halfLength + Step; offset + halfLength <= harbor.CorridorEnd; offset += Step)
        {
            if (!Patch(harbor, offset, halfLength, halfWidth, draft, settings)) continue;
            float departureOffset = offset + Mathf.Max(length * Mathf.Max(2, settings.departureLengthMultiplier), halfLength * 3);
            if (departureOffset + halfLength > harbor.CorridorEnd) break;
            if (!Patch(harbor, departureOffset, halfLength, halfWidth, draft, settings) ||
                !Lane(harbor, harbor.WaterStart, departureOffset + halfLength, halfWidth)) continue;
            berth = new HarborBerth(harbor, _topology.Normalize(harbor.Position + harbor.Waterward * offset),
                _topology.Normalize(harbor.Position + harbor.Waterward * departureOffset), halfLength, halfWidth, draft);
            reason = null; return true;
        }
        return false;
    }
}
