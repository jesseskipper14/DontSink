using System.Collections.Generic;
using UnityEngine;

public sealed class WorldMapSimContext
{
    private readonly Dictionary<string, MapNodeRuntime> _nodes;

    public WorldMapSimContext(Dictionary<string, MapNodeRuntime> nodes)
    {
        _nodes = nodes;
    }

    public MapNodeRuntime GetNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            Debug.LogError("[WorldMapSimContext] GetNode called with null/empty nodeId.");
            return null;
        }

        if (_nodes == null)
        {
            Debug.LogError("[WorldMapSimContext] Node dictionary is null.");
            return null;
        }

        if (_nodes.TryGetValue(nodeId, out var node))
            return node;

        // Debug: show seed mismatch clues
        Debug.LogError($"[WorldMapSimContext] Missing nodeId '{nodeId}'. Known={_nodes.Count}. " +
                       $"Example keys: {FirstKeys(_nodes, 5)}");

        return null;
    }

    private static string FirstKeys(Dictionary<string, MapNodeRuntime> dict, int n)
    {
        int i = 0;
        var s = "";
        foreach (var k in dict.Keys)
        {
            if (i++ >= n) break;
            s += k + " ";
        }
        return s.TrimEnd();
    }
}
