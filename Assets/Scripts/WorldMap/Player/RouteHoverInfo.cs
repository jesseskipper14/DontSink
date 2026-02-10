using System.Collections.Generic;

public sealed class RouteHoverInfo
{
    public float length;
    public bool isDirectEdge;

    // “Can I travel right now?” blockers
    public readonly List<string> blockers = new();

    // “Even if I can, should I?” risks (later)
    public readonly List<string> risks = new();

    // “Interesting stuff” (later)
    public readonly List<string> notes = new();
}
