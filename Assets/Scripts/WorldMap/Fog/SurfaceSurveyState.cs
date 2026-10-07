using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SurfaceSurveySkyPoint
{
    public Vector2 viewport;
    public float brightness;
    public Color color;
    public bool landmark;
    public SurfaceSurveySkyPoint Copy() => (SurfaceSurveySkyPoint)MemberwiseClone();
}

[Serializable]
public sealed class SurfaceSurveyReadingZone
{
    public string id;
    public Vector2 center;
    public float radius;
    public float heading;
    public float roughDistance;
    public bool completed;
    public List<SurfaceSurveySkyPoint> sky = new();
    public SurfaceSurveyReadingZone Copy()
    {
        var copy = (SurfaceSurveyReadingZone)MemberwiseClone();
        copy.sky = new();
        if (sky != null) foreach (var point in sky) if (point != null) copy.sky.Add(point.Copy());
        return copy;
    }
}

/// <summary>Host-owned fixed assignment. Zone coordinates never go into player-facing navigation UI.</summary>
[Serializable]
public sealed class SurfaceSurveyContract
{
    public int version = 1;
    public string id, title, originNodeId, originName, otherNodeId, otherNodeName;
    public int celestialVersion;
    public string celestialConfigHash;
    public List<SurfaceSurveyReadingZone> zones = new();
    public CartographicChartState reward;
    public bool rewardIssued;
    public int CompletedCount { get { int count = 0; if (zones != null) foreach (var zone in zones) if (zone != null && zone.completed) count++; return count; } }
    public bool IsComplete => zones != null && zones.Count > 0 && CompletedCount == zones.Count;
    public bool IsEndpoint(string nodeId) => !string.IsNullOrEmpty(nodeId) && (originNodeId == nodeId || otherNodeId == nodeId);
    public bool IsRegistered(WorldMapTopographyField field)
    {
        if (version != 1 || string.IsNullOrEmpty(id) || reward == null || !reward.CanIntegrate(field, out _) ||
            reward.payload?.sourceId != id || reward.payload.surface?.IsValid != true ||
            reward.payload.surface.worldBounds != reward.worldBounds || zones == null || zones.Count == 0 || zones.Count > 8) return false;
        var topology = new WorldTopology(reward.worldBounds); var ids = new HashSet<string>();
        foreach (var zone in zones)
            if (zone == null || string.IsNullOrEmpty(zone.id) || !ids.Add(zone.id) ||
                !WorldTopology.IsFinite(zone.center.x) || !WorldTopology.IsFinite(zone.center.y) ||
                !topology.ContainsY(zone.center.y) || !WorldTopology.IsFinite(zone.radius) || zone.radius <= 0) return false;
        return true;
    }
    public bool TryRecord(Vector2 position)
    {
        if (rewardIssued || reward == null || zones == null || !WorldTopology.IsFinite(position.x) || !WorldTopology.IsFinite(position.y)) return false;
        var topology = new WorldTopology(reward.worldBounds);
        if (!topology.IsValid || !topology.ContainsY(position.y)) return false;
        foreach (var zone in zones)
            if (zone != null && zone.radius > 0 && topology.Delta(position, zone.center).sqrMagnitude <= zone.radius * zone.radius)
            { zone.completed = true; return true; }
        return false;
    }
    public SurfaceSurveyContract Copy()
    {
        var copy = (SurfaceSurveyContract)MemberwiseClone();
        copy.reward = reward?.Copy(); copy.zones = new();
        if (zones != null) foreach (var zone in zones) if (zone != null) copy.zones.Add(zone.Copy());
        return copy;
    }
}

[Serializable]
public sealed class SurfaceSurveyBook
{
    public int version = 1;
    public List<SurfaceSurveyContract> contracts = new();
    public void EnsureDefaults() { contracts ??= new(); }
    public SurfaceSurveyContract Find(string id)
    {
        EnsureDefaults();
        return contracts.Find(c => c != null && c.id == id);
    }
    public SurfaceSurveyBook Copy()
    {
        var copy = new SurfaceSurveyBook { version = version };
        if (contracts != null) foreach (var contract in contracts) if (contract != null) copy.contracts.Add(contract.Copy());
        return copy;
    }
}

[Serializable]
public sealed class SurfaceSurveySettings
{
    [Min(1)] public float maximumReach = 100;
    [Min(1)] public float preferredDistance = 25;
    [Tooltip("Surface chart corridor radius in map units, independent of the reading acceptance radius.")]
    [Min(1)] public float swathRadius = 28;
    [Min(.1f)] public float readingRadius = 3;
    [Min(1)] public float readingSeparation = 8;
    [Range(16, 128)] public int candidateResolution = 64;
    [Range(1, 5)] public int offerCount = 3;
    [Range(1, 8)] public int maximumActiveContracts = 3;
    [Range(3, 30)] public float observationSeconds = 8;
    public bool IsValid => WorldTopology.IsFinite(maximumReach) && maximumReach > 0 &&
        WorldTopology.IsFinite(preferredDistance) && preferredDistance > 0 &&
        WorldTopology.IsFinite(swathRadius) && swathRadius > 0 &&
        WorldTopology.IsFinite(readingRadius) && readingRadius > 0 &&
        WorldTopology.IsFinite(readingSeparation) && readingSeparation > readingRadius * 2 &&
        WorldTopology.IsFinite(observationSeconds) && observationSeconds > 0;
}
