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

    // Only stores with reversible, notification-free writes can participate in market trades.
    public interface ITransactionalTradeItemStore : IItemStore
    {
        bool TryPrepareTrade(IReadOnlyList<TradeLine> lines, out IPreparedTradeItems prepared, out string failNote);
    }

    public interface IPreparedTradeItems : System.IDisposable
    {
        // Preparation must not mutate existing stock or expose newly spawned cargo.
        bool Validate(out string failNote);
        // Apply performs reversible writes without callbacks. Dispose rolls them back
        // until Complete; Complete must only mark success and must not throw or notify.
        void Apply();
        void Complete();
        // Called after every transaction participant has completed its writes.
        void Publish();
    }
}
