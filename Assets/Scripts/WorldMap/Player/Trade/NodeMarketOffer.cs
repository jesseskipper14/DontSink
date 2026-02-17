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
        public string offerId;     // stable for this refresh window
        public string itemId;
        public int unitPrice;
        public int quantityRemaining; // NEW: how much is left in this offer
        public MarketOfferKind kind;
    }

    //[Serializable]
    //public sealed class NodeMarketState
    //{
    //    public int version = 1;
    //    public string nodeId;
    //    public int lastRefreshBucket; // or tick/time bucket later
    //    public List<NodeMarketOffer> offers = new List<NodeMarketOffer>();
    //}
}
