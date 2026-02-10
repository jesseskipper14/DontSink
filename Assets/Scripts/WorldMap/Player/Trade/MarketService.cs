using System.Collections.Generic;

namespace WorldMap.Player.Trade
{
    /// <summary>
    /// Resolves player-visible offers using:
    /// - Node-owned policy (authoritative generator)
    /// - Player-owned cache (what this player last saw)
    /// </summary>
    public sealed class MarketService
    {
        private readonly WorldMapPlayerState _player;
        private readonly IMarketPolicy _policy;

        public MarketService(WorldMapPlayerState player, IMarketPolicy policy)
        {
            _player = player;
            _policy = policy;
        }

        public IReadOnlyList<NodeMarketOffer> GetOffers(string nodeId, int timeBucket)
        {
            if (!_player.marketCacheByNodeId.TryGetValue(nodeId, out var cache) ||
                cache == null || cache.timeBucket != timeBucket)
            {
                cache = new MarketCacheState
                {
                    nodeId = nodeId,
                    timeBucket = timeBucket
                };

                cache.offers.Clear();
                _policy.GenerateOffers(nodeId, timeBucket, cache.offers);

                _player.marketCacheByNodeId[nodeId] = cache;
            }

            return cache.offers;
        }

        public void Invalidate(string nodeId)
        {
            _player.marketCacheByNodeId.Remove(nodeId);
        }
    }
}
