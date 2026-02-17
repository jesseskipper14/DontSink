using System;
using System.Collections.Generic;

namespace WorldMap.Player.Trade
{
    [Serializable]
    public sealed class NodeMarketState
    {
        public int version = 3;

        public string nodeId;
        public int lastRefreshDay; // keep this for now

        public List<NodeMarketOffer> offers = new List<NodeMarketOffer>();

        // -------- Item memory / embargo (2B) --------
        // If an item is embargoed for a side, we don't generate offers for it on that side until timeBucket >= expiry.
        public Dictionary<string, int> embargoSellToPlayerUntil = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> embargoBuyFromPlayerUntil = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public bool IsEmbargoed(MarketOfferKind kind, string itemId, int timeBucket)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;

            Dictionary<string, int> dict =
                kind == MarketOfferKind.SellToPlayer ? embargoSellToPlayerUntil :
                kind == MarketOfferKind.BuyFromPlayer ? embargoBuyFromPlayerUntil :
                null;

            if (dict == null) return false;

            if (!dict.TryGetValue(itemId, out var until)) return false;
            return timeBucket < until;
        }

        public void SetEmbargo(MarketOfferKind kind, string itemId, int untilExclusive)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;

            Dictionary<string, int> dict =
                kind == MarketOfferKind.SellToPlayer ? embargoSellToPlayerUntil :
                kind == MarketOfferKind.BuyFromPlayer ? embargoBuyFromPlayerUntil :
                null;

            if (dict == null) return;

            // Only extend, never shorten.
            if (dict.TryGetValue(itemId, out var cur))
                dict[itemId] = Math.Max(cur, untilExclusive);
            else
                dict[itemId] = untilExclusive;
        }

        public void CleanupEmbargoes(int timeBucket)
        {
            Cleanup(embargoSellToPlayerUntil, timeBucket);
            Cleanup(embargoBuyFromPlayerUntil, timeBucket);
        }

        private static void Cleanup(Dictionary<string, int> dict, int timeBucket)
        {
            if (dict == null || dict.Count == 0) return;

            // Remove expired entries to avoid endless growth.
            var keys = ListPool<string>.Get();
            foreach (var kvp in dict)
                if (timeBucket >= kvp.Value) keys.Add(kvp.Key);

            for (int i = 0; i < keys.Count; i++)
                dict.Remove(keys[i]);

            ListPool<string>.Release(keys);
        }

        // Tiny internal list pool so we don't allocate every refresh (Unity, sigh).
        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> _pool = new Stack<List<T>>();

            public static List<T> Get()
            {
                if (_pool.Count > 0)
                {
                    var l = _pool.Pop();
                    l.Clear();
                    return l;
                }
                return new List<T>(32);
            }

            public static void Release(List<T> l)
            {
                if (l == null) return;
                l.Clear();
                _pool.Push(l);
            }
        }
    }
}
