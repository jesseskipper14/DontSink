public sealed class RuntimeRegistryNodeLookup : INodeStateLookup
{
    private readonly WorldMapRuntimeRegistry _registry;

    public RuntimeRegistryNodeLookup(WorldMapRuntimeRegistry registry)
    {
        _registry = registry;
    }

    public MapNodeState GetNodeState(string nodeId)
    {
        if (_registry == null || string.IsNullOrWhiteSpace(nodeId)) return null;
        var rt = _registry.GetByStableId(nodeId); // whatever your API is
        return rt?.State;
    }
}
