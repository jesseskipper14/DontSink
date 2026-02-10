using System.Collections.Generic;
using WorldMap.Player.StarMap;
using WorldMap.Player.Trade;

[System.Serializable]
public sealed class WorldMapPlayerState
{
    public string currentNodeId;
    public int credits;
    public InventoryState inventory = new InventoryState();

    // Legacy / phase-1 route unlocking (still in use)
    public HashSet<string> unlockedRoutes = new HashSet<string>(); // e.g. "nodeA|nodeB"

    // Optional cluster gating
    public HashSet<int> unlockedClusters = new HashSet<int>();

    // Star Map knowledge (new system, additive for now)
    public PlayerStarMapState starMap = new PlayerStarMapState();

    // Player-owned cache of what markets looked like when this player last checked.
    // (Unity won't serialize Dictionary; fine for JSON save later.)
    public Dictionary<string, MarketCacheState> marketCacheByNodeId =
        new Dictionary<string, MarketCacheState>();
}
