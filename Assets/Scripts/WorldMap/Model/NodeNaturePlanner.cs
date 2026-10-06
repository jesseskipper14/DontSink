using System;
using System.Collections.Generic;
using UnityEngine;

public enum NodeTreeKind { Palm, Deciduous, Pine }
public enum NodePlantKind { Fern, Flowers, Bush }

[Serializable]
public sealed class NodeFoliageCandidate
{
    public string id;
    public Vector2 position;
    public bool populated;
    public int variant;
    public float scale;
}

/// <summary>Cosmetic plan derived from saved settlement identity, never global random state.</summary>
public sealed class NodeNaturePlan
{
    public float latitude;
    public NodeTreeKind treeKind;
    public readonly List<NodeFoliageCandidate> plants = new();
    public readonly List<NodeFoliageCandidate> trees = new();
    public readonly List<Vector2[]> hills = new();
}

public static class NodeNaturePlanner
{
    public static NodeTreeKind TreeKind(float latitude) => Mathf.Abs(latitude) <= .25f ?
        NodeTreeKind.Palm : Mathf.Abs(latitude) <= .65f ? NodeTreeKind.Deciduous : NodeTreeKind.Pine;

    // Independent hashed channels keep candidates stable when other decoration is added.
    public static float Sample(int seed, string id, int channel)
    {
        unchecked
        {
            uint h = (uint)seed ^ 2166136261u;
            foreach (char c in id) h = (h ^ c) * 16777619u;
            h = (h ^ (uint)channel) * 16777619u;
            h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15;
            h *= 0x846ca68bu; h ^= h >> 16;
            return (h & 0xffffff) / 16777216f;
        }
    }

    public static bool PlantSiteClear(NodeSettlementManifest layout, Vector2 point, float radius = .65f)
    {
        foreach (var plot in layout.plots)
        {
            bool reservedFront = plot.role == SettlementRole.EventReserve || (!plot.IsOpen && plot.role != SettlementRole.Residence);
            if (reservedFront && point.y >= plot.position.y - .1f && point.y < plot.position.y + plot.height + .5f &&
                Mathf.Abs(point.x - plot.position.x) < plot.width / 2 + .3f + radius) return false;
            // Ordinary facades can carry decoration, but the rendered door is at width * 0.23.
            if (plot.role == SettlementRole.Residence && point.y >= plot.position.y - .1f && point.y < plot.position.y + 2.6f &&
                Mathf.Abs(point.x - (plot.position.x + plot.width * .23f)) < .575f + .3f + radius) return false;
            if (layout.plots.Exists(p => p.parentPlotId == plot.id) &&
                point.y >= plot.position.y - .1f && point.y <= plot.position.y + plot.height + .1f &&
                Mathf.Abs(point.x - (plot.position.x + plot.width / 2 + .55f)) < 1.2f + radius) return false;
        }
        foreach (var link in layout.connections)
        {
            float low = link.fromTerrace < 0 ? 0 : layout.terraces[link.fromTerrace].y;
            if (Mathf.Abs(point.x - link.x) < 1.2f + radius && point.y >= low - .1f && point.y <= layout.terraces[link.toTerrace].y + .1f)
                return false;
        }
        return true;
    }

    public static NodeNaturePlan Generate(NodeSettlementManifest layout, float latitude)
    {
        var plan = new NodeNaturePlan { latitude = Mathf.Clamp(latitude, -1, 1), treeKind = TreeKind(latitude) };
        void AddPlants(string row, float left, float right, float y)
        {
            for (int i = 0; left + i * 2.5f <= right; i++)
            {
                string id = "plant/" + row + "/" + i;
                var at = new Vector2(left + i * 2.5f + (Sample(layout.seed, id, 0) - .5f) * 1.2f, y);
                int variant = (int)(Sample(layout.seed, id, 2) * 3);
                float scale = .7f + Sample(layout.seed, id, 3) * .5f;
                plan.plants.Add(new NodeFoliageCandidate { id = id, position = at,
                    populated = PlantSiteClear(layout, at, (variant == 2 ? .83f : .6f) * scale) && Sample(layout.seed, id, 1) < .48f,
                    variant = variant, scale = scale });
            }
        }
        AddPlants("ground", -16, layout.harborArrival.x - 9, 0);
        foreach (var terrace in layout.terraces)
            if (terrace.y > .1f) AddPlants(terrace.id, terrace.left + 1, terrace.right - 1, terrace.y);
        for (int i = 0; -16 + i * 6 < layout.harborArrival.x - 8; i++)
        {
            string id = "tree/" + i;
            plan.trees.Add(new NodeFoliageCandidate { id = id,
                position = new Vector2(-16 + i * 6 + Sample(layout.seed, id, 0) * 2, 0),
                populated = Sample(layout.seed, id, 1) < .72f, variant = i % 3,
                scale = .75f + Sample(layout.seed, id, 2) * .55f });
        }
        for (int band = 0; band < 2; band++)
        {
            var points = new Vector2[19];
            for (int i = 0; i < points.Length; i++)
            {
                float u = i / (float)(points.Length - 1);
                float envelope = Mathf.Sin(u * Mathf.PI);
                float wave = .65f + .2f * Mathf.Sin(u * Mathf.PI * 4 + Sample(layout.seed, "hill", band) * 6);
                points[i] = new Vector2(Mathf.Lerp(-22, layout.harborArrival.x, u),
                    envelope * wave * (band == 0 ? 23 : 12));
            }
            plan.hills.Add(points);
        }
        return plan;
    }
}
