using System.Collections.Generic;

public sealed class WorldMapRuntimeRegistry : INodeStateLookup
{
    private readonly Dictionary<int, MapNodeRuntime> _byIndex = new();
    private readonly Dictionary<string, MapNodeRuntime> _byStableId = new();

    public void Clear()
    {
        _byIndex.Clear();
        _byStableId.Clear();
    }

    public void Add(int nodeIndex, string stableId, MapNodeRuntime runtime)
    {
        _byIndex[nodeIndex] = runtime;
        _byStableId[stableId] = runtime;
    }

    public MapNodeRuntime GetByIndex(int nodeIndex) => _byIndex[nodeIndex];
    public MapNodeRuntime GetByStableId(string stableId) => _byStableId[stableId];
    public IEnumerable<MapNodeRuntime> AllRuntimes => _byIndex.Values;

    public bool TryGetByIndex(int nodeIndex, out MapNodeRuntime rt) => _byIndex.TryGetValue(nodeIndex, out rt);
    public bool TryGetByStableId(string stableId, out MapNodeRuntime rt) => _byStableId.TryGetValue(stableId, out rt);

    public MapNodeState GetNodeState(string nodeId)
    {
        if (_byStableId.TryGetValue(nodeId, out var rt))
            return rt.State;

        return null;
    }
}
