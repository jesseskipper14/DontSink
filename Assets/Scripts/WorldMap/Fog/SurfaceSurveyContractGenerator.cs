using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded pure candidate generation. No passive discovery and no Unity global RNG use.</summary>
public static class SurfaceSurveyContractGenerator
{
    public static List<SurfaceSurveyContract> Generate(WorldMapTopographyField field, float sea,
        WorldMapKnowledgeState knowledge, SurfaceSurveyBook book, string nodeId, string nodeName,
        Vector2 origin, SurfaceSurveySettings settings)
    {
        var offers = new List<SurfaceSurveyContract>();
        if (field == null || !field.IsValid || knowledge == null || !knowledge.IsValid ||
            knowledge.WorldBounds != field.WorldBounds || settings == null || !settings.IsValid ||
            !WorldTopology.IsFinite(sea) || !WorldTopology.IsFinite(origin.x) ||
            !new WorldTopology(field.WorldBounds).ContainsY(origin.y) || string.IsNullOrEmpty(nodeId)) return offers;
        var topology = new WorldTopology(field.WorldBounds);
        int resolution = Mathf.Clamp(settings.candidateResolution, 16, 128);
        var candidates = new List<(SurfaceSurveyContract contract, double priority)>();
        var rng = new System.Random(StableHash(nodeId) ^ field.Seed);
        for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
        {
            var center = new Vector2(field.WorldBounds.xMin + (x + .5f) * field.WorldBounds.width / resolution,
                field.WorldBounds.yMin + (y + .5f) * field.WorldBounds.height / resolution);
            Vector2 delta = topology.Delta(origin, center);
            float distance = delta.magnitude;
            if (distance < settings.readingSeparation || distance > settings.maximumReach) continue;
            string id = $"surface-survey:v1:{field.Seed}:{field.GenerationVersion}:{nodeId}:{resolution}:{x}:{y}";
            if (book?.Find(id) != null) continue;
            var perpendicular = new Vector2(-delta.y, delta.x).normalized;
            var contract = new SurfaceSurveyContract { id = id, title = $"Waters {Bearing(delta):0}° from {nodeName}",
                originNodeId = nodeId, originName = nodeName };
            bool eligible = true;
            for (int i = 0; i < 3; i++)
            {
                Vector2 point = topology.Normalize(center + perpendicular * ((i - 1) * settings.readingSeparation));
                if (!IsUnknownWater(field, sea, knowledge, point) || topology.Distance(origin, point) > settings.maximumReach)
                { eligible = false; break; }
                var fromOrigin = topology.Delta(origin, point);
                contract.zones.Add(new SurfaceSurveyReadingZone { id = id + ":reading:" + i, center = point,
                    radius = Mathf.Max(.1f, settings.readingRadius), heading = Bearing(fromOrigin),
                    roughDistance = Mathf.Round(fromOrigin.magnitude / 5f) * 5f });
            }
            if (!eligible) continue;
            double weight = 1d / (1d + Math.Pow(distance / Math.Max(1, settings.preferredDistance), 2));
            // Weighted sampling without replacement, deterministic tie/order. Nearby water dominates.
            double priority = -Math.Log(Math.Max(1e-9, rng.NextDouble())) / weight;
            candidates.Add((contract, priority));
        }
        candidates.Sort((a, b) => { int c = a.priority.CompareTo(b.priority); return c != 0 ? c : string.CompareOrdinal(a.contract.id, b.contract.id); });
        int maskAttempts = 0;
        foreach (var candidate in candidates)
        {
            if (offers.Count >= Mathf.Clamp(settings.offerCount, 1, 5)) break;
            var contract = candidate.contract;
            bool overlaps = false;
            foreach (var offer in offers)
                if (topology.Delta(offer.zones[1].center, contract.zones[1].center).magnitude < settings.readingSeparation * 2) overlaps = true;
            if (overlaps) continue;
            if (maskAttempts++ >= 32) break;
            var path = new[] { contract.zones[0].center, contract.zones[1].center, contract.zones[2].center };
            contract.reward = new CartographicChartState { kind = CartographicChartKind.Georeferenced,
                title = contract.title + " — processed survey", referenceText = "Surveyed surface geography only. No soundings, node markers or POI records.",
                worldSeed = field.Seed, topographyVersion = field.GenerationVersion, worldBounds = field.WorldBounds,
                payload = new WorldMapCartographicPayload { sourceId = contract.id,
                    surface = WorldMapCoverageMask.Corridor(knowledge.Width, knowledge.Height, field.WorldBounds, path, Mathf.Max(.1f, settings.swathRadius)) } };
            if (Array.Exists(contract.reward.payload.surface.cells, cell => cell)) offers.Add(contract);
        }
        return offers;
    }

    public static bool IsUnknownWater(WorldMapTopographyField field, float sea, WorldMapKnowledgeState state, Vector2 point)
        => WorldTopology.IsFinite(point.x) && new WorldTopology(field.WorldBounds).ContainsY(point.y) && field.Sample01World(point) < sea &&
           !state.IsRevealed(WorldMapKnowledgeLayer.Surface, point);
    public static float Bearing(Vector2 delta) => Mathf.Repeat(Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, 360);
    public static int StableHash(string text) { unchecked { int hash = 17; foreach (char c in text ?? "") hash = hash * 31 + c; return hash; } }
}
