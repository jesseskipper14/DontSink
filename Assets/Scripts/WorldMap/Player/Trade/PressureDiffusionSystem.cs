using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PressureDiffusionSystem
{
    [Serializable]
    public struct Settings
    {
        [Header("Conductance")]
        [Min(0f)] public float baseConductancePerHour;     // baseline transfer speed per hour
        [Min(0f)] public float intraClusterMultiplier;     // intra edges transfer faster
        [Min(0f)] public float interClusterMultiplier;     // inter edges transfer slower

        [Header("Logistics influence")]
        [Range(0f, 1f)] public float dockTradeInfluence01; // 0=no effect, 1=full effect

        [Header("Loss + safety")]
        [Range(0f, 0.5f)] public float edgeLoss01;         // 0.04 = 4% loss per hop
        [Min(0.0001f)] public float maxAbsFlowPerEdge;     // clamp per edge per tick (pressure units)

        [Header("Optional: cap throughput by degree (smooth, not a hard cutoff)")]
        [Min(0)] public int targetDegree;                  // 0 disables normalization (recommended start: 4)
    }

    private readonly Settings _s;

    // Accumulate deltas here so edge iteration order doesn't bias results.
    // Key: nodeStableId + "|" + itemId  -> deltaPressure
    private readonly Dictionary<string, float> _delta = new Dictionary<string, float>(1024);

    public PressureDiffusionSystem(Settings settings)
    {
        _s = settings;
    }

    /// <summary>
    /// Apply one diffusion pass across the graph for all resources.
    /// This does NOT tick baseline drift; do that separately on each node.
    /// </summary>
    public void Tick(
        MapGraph graph,
        WorldMapRuntimeRegistry registry,
        ResourceCatalog resources,
        float dtHours)
    {
        if (graph == null || registry == null || resources == null) return;
        if (dtHours <= 0f) return;

        var list = resources.Resources;
        if (list == null || list.Count == 0) return;

        _delta.Clear();

        float eff = Mathf.Clamp01(1f - _s.edgeLoss01);

        for (int ei = 0; ei < graph.edges.Count; ei++)
        {
            var e = graph.edges[ei];

            if (!registry.TryGetByIndex(e.a, out var ra) || ra == null) continue;
            if (!registry.TryGetByIndex(e.b, out var rb) || rb == null) continue;

            var sa = ra.State;
            var sb = rb.State;
            if (sa == null || sb == null) continue;

            bool intra = (ra.ClusterId == rb.ClusterId);
            float mult = intra ? _s.intraClusterMultiplier : _s.interClusterMultiplier;

            float conductance = _s.baseConductancePerHour * mult;

            // Logistics influence (dock/trade)
            conductance *= EdgeLogisticsFactor(sa, sb, _s.dockTradeInfluence01);

            // Optional: smooth throughput cap by degree (avoids hub superhighways)
            if (_s.targetDegree > 0)
            {
                int degA = EstimateDegree(graph, ra.NodeIndex);
                int degB = EstimateDegree(graph, rb.NodeIndex);
                float normA = DegreeNormalization(degA, _s.targetDegree);
                float normB = DegreeNormalization(degB, _s.targetDegree);
                conductance *= Mathf.Sqrt(normA * normB);
            }

            // Per tick
            float k = conductance * dtHours;
            if (k <= 0f) continue;

            for (int ri = 0; ri < list.Count; ri++)
            {
                var def = list[ri];
                if (def == null || string.IsNullOrWhiteSpace(def.itemId)) continue;

                float pa = sa.GetPressure(def.itemId);
                float pb = sb.GetPressure(def.itemId);

                float diff = pa - pb;
                if (Mathf.Abs(diff) < 0.0001f) continue;

                float flow = diff * k;

                // Safety clamp per edge per tick
                flow = Mathf.Clamp(flow, -_s.maxAbsFlowPerEdge, _s.maxAbsFlowPerEdge);

                if (def.itemId == "fish" && Mathf.Abs(flow) > 0.0001f)
                {
                    //Debug.Log($"[Diffusion] fish {ra.DisplayName}({pa:0.00}) -> {rb.DisplayName}({pb:0.00}) flow={flow:0.000}");
                }

                // A loses full flow, B gains reduced flow (loss sinks to “sea”)
                AddDelta(ra.StableId, def.itemId, -flow);
                AddDelta(rb.StableId, def.itemId, +flow * eff);
            }
        }

        // Apply accumulated deltas
        foreach (var kvp in _delta)
        {
            string key = kvp.Key;
            int split = key.IndexOf('|');
            if (split <= 0) continue;

            string nodeId = key.Substring(0, split);
            string itemId = key.Substring(split + 1);

            var st = registry.GetNodeState(nodeId);
            if (st == null) continue;

            st.AddPressureImpulse(itemId, kvp.Value);
        }
    }

    private void AddDelta(string nodeId, string itemId, float delta)
    {
        string k = nodeId + "|" + itemId;
        if (_delta.TryGetValue(k, out var cur)) _delta[k] = cur + delta;
        else _delta[k] = delta;
    }

    private static float EdgeLogisticsFactor(MapNodeState a, MapNodeState b, float influence01)
    {
        influence01 = Mathf.Clamp01(influence01);
        if (influence01 <= 0f) return 1f;

        float dockA = Stat01(a, NodeStatId.DockRating);
        float tradeA = Stat01(a, NodeStatId.TradeRating);
        float dockB = Stat01(b, NodeStatId.DockRating);
        float tradeB = Stat01(b, NodeStatId.TradeRating);

        // nodeFactor: 0.5..1.0 based on dock+trade
        float nodeA = 0.5f + 0.5f * ((dockA + tradeA) * 0.5f);
        float nodeB = 0.5f + 0.5f * ((dockB + tradeB) * 0.5f);

        float edge = Mathf.Sqrt(nodeA * nodeB);

        // Blend toward 1.0 so you can dial the effect in/out.
        return Mathf.Lerp(1f, edge, influence01);
    }

    private static float Stat01(MapNodeState s, NodeStatId id)
    {
        if (s != null && s.TryGetStat(id, out var st))
            return Mathf.Clamp01(st.value / 4f);
        return 0.5f;
    }

    // Degree helpers (simple + deterministic; can optimize later if needed)
    private static int EstimateDegree(MapGraph graph, int nodeIndex)
    {
        if (graph == null || graph.edges == null) return 0;

        int deg = 0;
        for (int i = 0; i < graph.edges.Count; i++)
        {
            var e = graph.edges[i];
            if (e.a == nodeIndex || e.b == nodeIndex) deg++;
        }
        return deg;
    }

    private static float DegreeNormalization(int degree, int targetDegree)
    {
        if (targetDegree <= 0) return 1f;
        if (degree <= targetDegree) return 1f;

        // If degree is double target, halve conductance per edge (smooth throughput cap).
        return (float)targetDegree / degree;
    }
}
