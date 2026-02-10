using System;
using System.Collections.Generic;

namespace WorldMap.Player.Trade
{
    [Serializable]
    public sealed class MarketCacheState
    {
        public int version = 1;

        public string nodeId;

        /// <summary>
        /// Coarse refresh bucket (e.g. day index). If this matches, cache is valid.
        /// </summary>
        public int timeBucket;

        public List<NodeMarketOffer> offers = new List<NodeMarketOffer>();
    }
}
