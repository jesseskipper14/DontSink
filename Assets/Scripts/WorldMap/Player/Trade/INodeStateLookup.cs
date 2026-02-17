public interface INodeStateLookup
{
    MapNodeState GetNodeState(string nodeId); // nodeId == stableId
}
