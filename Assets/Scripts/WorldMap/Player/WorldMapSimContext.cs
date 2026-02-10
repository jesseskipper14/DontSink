using System.Collections.Generic;

public sealed class WorldMapSimContext
{
    private readonly Dictionary<string, MapNodeRuntime> _nodes;

    public WorldMapSimContext(Dictionary<string, MapNodeRuntime> nodes)
    {
        _nodes = nodes;
    }

    public MapNodeRuntime GetNode(string nodeId)
    {
        return _nodes[nodeId];
    }
}
