using System.Collections.Generic;

namespace WorldMap.Player.Trade
{
    /// <summary>
    /// Minimal seam for "where items live" during trade.
    /// WorldMap: InventoryState implements this.
    /// BoatScene: PhysicalCrateItemStore implements this (sell-zone / physical crates).
    /// </summary>
    public interface IItemStore
    {
        int GetCount(string itemId);
        void Add(string itemId, int amount);
        bool Remove(string itemId, int amount);
        IEnumerable<KeyValuePair<string, int>> Enumerate();
    }
}
