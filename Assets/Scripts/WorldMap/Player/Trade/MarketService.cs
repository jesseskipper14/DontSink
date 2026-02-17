using System;
using System.Collections.Generic;

namespace WorldMap.Player.Trade
{
    /// <summary>
    /// Resolves offers using:
    /// - Node-owned market state (authoritative storage)
    /// - Node-owned policy (authoritative generator)
    ///
    /// Player cache can remain as a UI memory later, but is not truth.
    /// </summary>
    public sealed class MarketService
    {
        private readonly WorldMapPlayerState _player; // optional now; can remove later
        private readonly IMarketPolicy _policy;
        private readonly INodeStateLookup _nodes;

        public MarketService(WorldMapPlayerState player, IMarketPolicy policy, INodeStateLookup nodes)
        {
            _player = player;
            _policy = policy;
            _nodes = nodes;
        }

        public IReadOnlyList<NodeMarketOffer> GetOffers(string nodeId, int timeBucket)
        {
            var node = _nodes.GetNodeState(nodeId);
            if (node == null) return Array.Empty<NodeMarketOffer>();

            var market = node.MarketMutable;
            if (market == null) return Array.Empty<NodeMarketOffer>();

            if (string.IsNullOrWhiteSpace(market.nodeId))
                market.nodeId = nodeId;

            // Refresh once per bucket
            if (market.lastRefreshDay != timeBucket)
            {
                market.lastRefreshDay = timeBucket;
                market.offers.Clear();
                _policy.GenerateOffers(nodeId, timeBucket, market.offers);
            }

            return market.offers;
        }

        public void Invalidate(string nodeId)
        {
            // Node-authoritative: invalidation means "force refresh next request"
            var node = _nodes.GetNodeState(nodeId);
            if (node == null) return;

            var market = node.MarketMutable;
            if (market == null) return;

            market.lastRefreshDay = int.MinValue;
        }
    }
}
