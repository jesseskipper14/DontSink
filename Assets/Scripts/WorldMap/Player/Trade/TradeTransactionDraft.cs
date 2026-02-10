using System;
using System.Collections.Generic;

namespace WorldMap.Player.Trade
{
    public enum TradeDirection
    {
        BuyFromNode, // player pays credits, receives items
        SellToNode   // player gives items, receives credits
    }

    [Serializable]
    public sealed class TradeLine
    {
        public string itemId;
        public int quantity;
        public int unitPrice; // credits per item
        public TradeDirection direction;
    }

    /// <summary>
    /// Player-intended transaction to be validated and applied by TradeService.
    /// This is a draft payload (not authoritative) suitable for JSON serialization.
    /// </summary>
    [Serializable]
    public sealed class TradeTransactionDraft
    {
        public int version = 1;

        /// <summary>Which node market this is occurring at (stableId).</summary>
        public string nodeId;

        /// <summary>Optional: specific vendor stall / UI source.</summary>
        public string vendorId;

        /// <summary>Lines the player wants to execute.</summary>
        public List<TradeLine> lines = new List<TradeLine>();
    }
}
