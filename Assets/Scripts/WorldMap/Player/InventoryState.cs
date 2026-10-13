using System.Collections.Generic;
using WorldMap.Player.Trade;

[System.Serializable]
public sealed class InventoryState : ITransactionalTradeItemStore
{
    public Dictionary<string, int> stacks = new Dictionary<string, int>();

    public int GetCount(string itemId) => stacks.TryGetValue(itemId, out var c) ? c : 0;

    public void Add(string itemId, int amount)
    {
        if (amount <= 0) return;
        stacks[itemId] = GetCount(itemId) + amount;
    }

    public bool Remove(string itemId, int amount)
    {
        if (amount <= 0) return true;
        int have = GetCount(itemId);
        if (have < amount) return false;
        int left = have - amount;
        if (left == 0) stacks.Remove(itemId);
        else stacks[itemId] = left;
        return true;
    }

    public IEnumerable<KeyValuePair<string, int>> Enumerate()
    {
        foreach (var kvp in stacks)
            yield return kvp;
    }

    public bool TryPrepareTrade(IReadOnlyList<TradeLine> lines, out IPreparedTradeItems prepared, out string failNote)
    {
        prepared = null;
        failNote = null;
        var next = new Dictionary<string, int>(stacks, stacks.Comparer);
        // Sales use existing stock, not goods purchased elsewhere in the same basket.
        foreach (var line in lines)
        {
            if (line.direction != TradeDirection.SellToNode) continue;
            next.TryGetValue(line.itemId, out int have);
            if (have < line.quantity)
            {
                failNote = $"Not enough '{line.itemId}' to sell.";
                return false;
            }
            if (have == line.quantity) next.Remove(line.itemId);
            else next[line.itemId] = have - line.quantity;
        }
        foreach (var line in lines)
        {
            if (line.direction != TradeDirection.BuyFromNode) continue;
            next.TryGetValue(line.itemId, out int have);
            next[line.itemId] = checked(have + line.quantity);
        }
        prepared = new PreparedTrade(this, next);
        return true;
    }

    private sealed class PreparedTrade : IPreparedTradeItems
    {
        private readonly InventoryState owner;
        private readonly Dictionary<string, int> original, expected, next;
        private bool applied, completed;

        public PreparedTrade(InventoryState owner, Dictionary<string, int> next)
        {
            this.owner = owner;
            original = owner.stacks;
            expected = new Dictionary<string, int>(original, original.Comparer);
            this.next = next;
        }

        public bool Validate(out string failNote)
        {
            failNote = "Inventory changed while preparing trade.";
            if (owner.stacks != original || original.Count != expected.Count) return false;
            foreach (var entry in expected)
                if (!original.TryGetValue(entry.Key, out int value) || value != entry.Value) return false;
            failNote = null;
            return true;
        }

        public void Apply() { applied = true; owner.stacks = next; }
        public void Complete() => completed = true;
        public void Publish() { }
        public void Dispose() { if (applied && !completed) owner.stacks = original; }
    }
}
