using System.Collections.Generic;
using UnityEngine;

public readonly struct HarborVisualObservation
{
    public readonly MapNode Node;
    public readonly HarborDefinition Harbor;
    public readonly float Distance;
    public HarborVisualObservation(MapNode node, HarborDefinition harbor, float distance)
    { Node = node; Harbor = harbor; Distance = distance; }

    /// <summary>Physical visibility only: route and cartographic knowledge are deliberately not consulted.</summary>
    public static void Collect(Vector2 observer, float range, List<HarborVisualObservation> output)
    {
        output.Clear();
        var graph = HarborTravelService.CurrentGraph;
        if (graph == null || range <= 0) return;
        foreach (var node in graph.nodes)
        {
            if (node == null) continue;
            float distance = WorldTopologyService.Distance(observer, node.position);
            if (distance > range || !HarborTravelService.TryDefinition(node, out var harbor, out _)) continue;
            output.Add(new HarborVisualObservation(node, harbor, distance));
        }
    }
}
