using System.Collections.Generic;

public sealed class WorldMapHoverState
{
    public int HoveredNodeIndex { get; private set; } = -1;

    // Pinned anchors are node indices (graph indices)
    private readonly HashSet<int> _pinned = new HashSet<int>();
    public IReadOnlyCollection<int> Pinned => _pinned;

    public bool SetHovered(int nodeIndex)
    {
        if (HoveredNodeIndex == nodeIndex) return false;
        HoveredNodeIndex = nodeIndex;
        return true;
    }

    public bool ClearHovered()
    {
        if (HoveredNodeIndex == -1) return false;
        HoveredNodeIndex = -1;
        return true;
    }

    public bool Pin(int nodeIndex) => _pinned.Add(nodeIndex);

    public bool ClearPins()
    {
        if (_pinned.Count == 0) return false;
        _pinned.Clear();
        return true;
    }
}
