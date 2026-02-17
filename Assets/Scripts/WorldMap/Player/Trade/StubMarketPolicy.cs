using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldMap.Player.Trade
{
    public sealed class StubMarketPolicy : IMarketPolicy
    {
        private readonly INodeStateLookup _nodes;

        // Tunables (make these ScriptableObject later)
        private const int BaseSlots = 4;
        private const float TradeSlotsScale = 14f;      // TradeRating drives throughput
        private const float ProsperitySlotsScale = 8f;  // Prosperity drives variety

        public StubMarketPolicy(INodeStateLookup nodes)
        {
            _nodes = nodes;
        }

        public void GenerateOffers(string nodeId, int timeBucket, List<NodeMarketOffer> outOffers)
        {
            var sellItems = new HashSet<string>();
            var buyItems = new HashSet<string>();

            outOffers.Clear();

            var node = _nodes?.GetNodeState(nodeId);

            float tradeRating = 1f;
            float prosperity = 1f;

            if (node != null)
            {
                if (node.TryGetStat(NodeStatId.TradeRating, out var tr)) tradeRating = tr.value;
                if (node.TryGetStat(NodeStatId.Prosperity, out var pr)) prosperity = pr.value;
            }

            float trade01 = Mathf.InverseLerp(0f, 4f, tradeRating);
            float pros01 = Mathf.InverseLerp(0f, 4f, prosperity);

            int slots = Mathf.Clamp(
                Mathf.RoundToInt(BaseSlots + trade01 * TradeSlotsScale + pros01 * ProsperitySlotsScale),
                2, 40);

            // Prosperity+low-market -> exotic skew
            float exoticRate = Mathf.Clamp01(Mathf.Pow(pros01, 1.4f) * (1f - trade01 * 0.6f));
            int exoticSlots = Mathf.Clamp(Mathf.RoundToInt(slots * exoticRate), 0, slots);
            int boringSlots = slots - exoticSlots;

            // Deterministic-ish seed per node+bucket
            int seed = (nodeId?.GetHashCode() ?? 0) ^ (timeBucket * 7919);
            var rng = new System.Random(seed);

            // Pools (replace with ResourceDefs later)
            string[] boring = { "fish", "planks", "scrap", "wood", "cloth" };
            string[] exotic = { "medkit", "fuel", "luxuries", "metal" };

            // Mix of sell and buy
            int sellSlots = Mathf.Clamp(Mathf.RoundToInt(slots * 0.55f), 1, slots);
            int buySlots = slots - sellSlots;

            // Generate boring slots first
            int slotIndex = 0;
            for (int i = 0; i < boringSlots; i++)
            {
                bool sell = (slotIndex < sellSlots);
                string item = boring[rng.Next(boring.Length)];

                AddOffer(outOffers, nodeId, timeBucket, slotIndex++, item, sell, pros01, trade01, rng);
            }

            // Then exotic slots
            for (int i = 0; i < exoticSlots; i++)
            {
                bool sell = (slotIndex < sellSlots);
                string item = exotic[rng.Next(exotic.Length)];

                AddOffer(outOffers, nodeId, timeBucket, slotIndex++, item, sell, pros01, trade01, rng, isExotic: true);
            }
        }

        private static void AddOffer(
            List<NodeMarketOffer> offers,
            string nodeId,
            int bucket,
            int slotIndex,
            string itemId,
            bool sellToPlayer,
            float pros01,
            float trade01,
            System.Random rng,
            bool isExotic = false)
        {
            // Prices: prosperity pushes “spendiness”, trade pushes “efficiency”
            int basePrice = itemId switch
            {
                "fish" => 5,
                "scrap" => 3,
                "planks" => 6,
                "wood" => 6,
                "cloth" => 8,
                "fuel" => 12,
                "metal" => 14,
                "medkit" => 18,
                "luxuries" => 22,
                _ => 10
            };

            // Exotic variance: weird pricing bands when prosperous but low-market
            float weirdness = Mathf.Clamp01(Mathf.Pow(pros01, 1.3f) * (1f - trade01));
            int variance = isExotic ? Mathf.RoundToInt(6 * weirdness) + 2 : 2;

            int price = basePrice + rng.Next(-variance, variance + 1);

            // Quantity: trade drives how many stalls / how much volume
            int qBase = isExotic ? 3 : 8;
            int qScale = Mathf.RoundToInt(6f * trade01 + 4f * pros01);
            int quantity = Mathf.Max(1, qBase + qScale + rng.Next(0, 5));

            offers.Add(new NodeMarketOffer
            {
                offerId = $"{nodeId}:{bucket}:{slotIndex}:{(sellToPlayer ? "sell" : "buy")}",
                itemId = itemId,
                unitPrice = Math.Max(0, price),
                quantityRemaining = Math.Max(0, quantity),
                kind = sellToPlayer ? MarketOfferKind.SellToPlayer : MarketOfferKind.BuyFromPlayer
            });
        }
    }
}
