public interface IWorldMapEventResolver
{
    // Returns true if event resolved this tick.
    bool TryResolve(ref WorldMapEventInstance ev, WorldMapGraphGenerator generator, out WorldMapEventResolved resolved);

    // Optional: used for player actions (trade delivery, etc.)
    bool TryPlayerComplete(ref WorldMapEventInstance ev, WorldMapGraphGenerator generator, out WorldMapEventResolved resolved);
}
