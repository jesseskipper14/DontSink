using System;
using System.Collections.Generic;

namespace WorldMap.Player.Trade
{
    /// <summary>
    /// Temporary deterministic offers per node/timeBucket.
    /// Replace later with a policy that uses node stats/buffs/events.
    /// </summary>
    public sealed class StubMarketPolicy : IMarketPolicy
    {
        public void GenerateOffers(string nodeId, int timeBucket, List<NodeMarketOffer> outOffers)
        {
            outOffers.Clear();

            // Deterministic-ish seed per node + time bucket
            int seed = (nodeId?.GetHashCode() ?? 0) ^ (timeBucket * 7919);
            var rng = new Random(seed);

            // Minimal item set for now (replace with ItemDefs later)
            string[] items = { "fish", "scrap", "planks", "fuel", "medkit" };

            // Always sell a couple basics to player
            Add(outOffers, "sell:fuel", "fuel", 12 + rng.Next(-2, 3), 0, MarketOfferKind.SellToPlayer);
            Add(outOffers, "sell:planks", "planks", 8 + rng.Next(-1, 2), 0, MarketOfferKind.SellToPlayer);

            // Buy 2 random items from player
            for (int i = 0; i < 2; i++)
            {
                var item = items[rng.Next(items.Length)];
                int price = item switch
                {
                    "fish" => 5,
                    "scrap" => 3,
                    "planks" => 6,
                    "fuel" => 7,
                    "medkit" => 15,
                    _ => 1
                };

                price += rng.Next(-1, 2);
                Add(outOffers, $"buy:{item}", item, price, 0, MarketOfferKind.BuyFromPlayer);
            }
        }

        private static void Add(List<NodeMarketOffer> offers, string offerId, string itemId, int price, int limit, MarketOfferKind kind)
        {
            offers.Add(new NodeMarketOffer
            {
                offerId = offerId,
                itemId = itemId,
                unitPrice = Math.Max(0, price),
                limitPerVisit = Math.Max(0, limit),
                kind = kind
            });
        }
    }
}
