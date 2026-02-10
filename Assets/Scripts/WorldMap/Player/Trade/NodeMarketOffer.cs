using System;
using System.Collections.Generic;

namespace WorldMap.Player.Trade
{
    public enum MarketOfferKind
    {
        SellToPlayer,
        BuyFromPlayer
    }

    [Serializable]
    public sealed class NodeMarketOffer
    {
        public string offerId;   // stable id for this offer (for limits/refresh)
        public string itemId;
        public int unitPrice;
        public int limitPerVisit; // optional; 0 = unlimited
        public MarketOfferKind kind;
    }

    [Serializable]
    public sealed class NodeMarketState
    {
        public int version = 1;
        public string nodeId;
        public int lastRefreshDay; // or tick/time bucket later
        public List<NodeMarketOffer> offers = new List<NodeMarketOffer>();
    }
}
