using System;
using System.Collections.Generic;

[Serializable]
public sealed class WorldMapSimState
{
    public Dictionary<string, MapNodeState> byNodeStableId = new();
}