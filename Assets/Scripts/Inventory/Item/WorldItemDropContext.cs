using UnityEngine;

/// <summary>
/// Request context for an inventory-driven world-item drop.
///
/// The requester is the local gameplay actor that initiated the drop. This is a
/// transport seam, not a trusted network identity: a future multiplayer transport
/// should authenticate the sender before constructing/applying this context on
/// authority.
/// </summary>
public readonly struct WorldItemDropContext
{
    public GameObject Requester { get; }
    public Vector2 Origin { get; }

    public bool HasRequester =>
        Requester != null;

    public WorldItemDropContext(
        GameObject requester,
        Vector2 origin)
    {
        Requester = requester;
        Origin = origin;
    }
}
