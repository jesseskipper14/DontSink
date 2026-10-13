using System;
using System.Collections.Generic;

// Session-owned, saveable stock. UI tabs/scroll positions are deliberately not stored here.
[Serializable]
public sealed class ItemVendorStockState
{
    [Serializable]
    public sealed class Entry
    {
        public string vendorKey;
        public string itemId;
        public int remaining;
    }

    public List<Entry> entries = new();
    [NonSerialized] private Dictionary<(string, string), Entry> lookup;
    [NonSerialized] private HashSet<(string, string)> pending;

    public static string BuildVendorKey(string nodeId, string agentId, string serviceId)
    {
        nodeId ??= "";
        agentId ??= "";
        serviceId ??= "";
        // Length-prefix components so delimiters inside authored IDs cannot cause collisions.
        return $"{nodeId.Length}:{nodeId}{agentId.Length}:{agentId}{serviceId.Length}:{serviceId}";
    }

    private Entry Find(string vendorKey, string itemId)
    {
        if (lookup == null)
        {
            lookup = new();
            if (entries != null)
                foreach (var entry in entries)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.vendorKey) && !string.IsNullOrWhiteSpace(entry.itemId))
                        lookup[(entry.vendorKey, entry.itemId)] = entry;
        }
        return lookup.TryGetValue((vendorKey, itemId), out var found) ? found : null;
    }

    // Reading never initializes/refills an entry. Authored stock is only the initial allowance.
    public int GetRemaining(string vendorKey, string itemId, int initialStock)
    {
        if (initialStock < 0) return -1;
        var entry = Find(vendorKey, itemId);
        return entry != null ? Math.Max(0, entry.remaining) : initialStock;
    }

    public bool IsPurchasePending(string vendorKey, string itemId) => pending?.Contains((vendorKey, itemId)) == true;

    public bool TryReserve(string vendorKey, string itemId, int initialStock, out Reservation reservation)
    {
        reservation = null;
        if (string.IsNullOrWhiteSpace(vendorKey) || string.IsNullOrWhiteSpace(itemId)) return false;
        int remaining = GetRemaining(vendorKey, itemId, initialStock);
        if (remaining == 0 || !(pending ??= new()).Add((vendorKey, itemId))) return false;
        var entry = initialStock < 0 ? null : Find(vendorKey, itemId);
        bool created = initialStock >= 0 && entry == null;
        if (created)
        {
            entry = new Entry { vendorKey = vendorKey, itemId = itemId, remaining = remaining };
            (entries ??= new()).Add(entry);
            lookup[(vendorKey, itemId)] = entry;
        }
        reservation = new Reservation(this, vendorKey, itemId, entry, created);
        return true;
    }

    public sealed class Reservation : IDisposable
    {
        private readonly ItemVendorStockState owner;
        private readonly string vendorKey, itemId;
        private readonly Entry entry;
        private readonly int before;
        private readonly bool created;
        private bool completed, disposed;

        internal Reservation(ItemVendorStockState owner, string vendorKey, string itemId, Entry entry, bool created)
        {
            this.owner = owner; this.vendorKey = vendorKey; this.itemId = itemId;
            this.entry = entry; this.created = created;
            if (entry != null) { before = entry.remaining; entry.remaining = before - 1; }
        }

        public void Complete() => completed = true;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!completed && entry != null)
            {
                if (created) { owner.entries.Remove(entry); owner.lookup.Remove((vendorKey, itemId)); }
                else entry.remaining = before;
            }
            owner.pending.Remove((vendorKey, itemId));
        }
    }
}
