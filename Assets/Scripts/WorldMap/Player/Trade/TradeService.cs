using System;
using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

public sealed class TradeService
{
    public enum FailReason
    {
        None,
        InvalidDraft,
        InvalidLine,
        InsufficientCredits,
        InsufficientItems,
        OfferUnavailable
    }

    [Serializable]
    public sealed class TradeReceipt
    {
        public int version = 1;
        public string nodeId;
        public string vendorId;

        public int creditsDelta;
        public int totalFeesPaid;
        public List<TradeLine> appliedLines = new List<TradeLine>();
    }

    // Back-compat path (WorldMap inventory).
    public bool TryExecute(
        TradeTransactionDraft draft,
        WorldMapPlayerState player,
        IReadOnlyList<NodeMarketOffer> offers,
        MapNodeState nodeState,
        ITradeFeePolicy feePolicy,
        int timeBucket,
        int cooldownSellToPlayerBuckets,
        int cooldownBuyFromPlayerBuckets,
        out TradeReceipt receipt,
        out FailReason failReason,
        out string failNote)
    {
        return TryExecute(
            draft,
            player,
            offers,
            nodeState,
            feePolicy,
            timeBucket,
            cooldownSellToPlayerBuckets,
            cooldownBuyFromPlayerBuckets,
            itemStore: player != null ? (IItemStore)player.inventory : null,
            out receipt,
            out failReason,
            out failNote);
    }

    /// <summary>
    /// NEW: Executes trade using an injected IItemStore (inventory OR physical crates).
    /// </summary>
    public bool TryExecute(
        TradeTransactionDraft draft,
        WorldMapPlayerState player,
        IReadOnlyList<NodeMarketOffer> offers,
        MapNodeState nodeState,
        ITradeFeePolicy feePolicy,
        int timeBucket,
        int cooldownSellToPlayerBuckets,
        int cooldownBuyFromPlayerBuckets,
        IItemStore itemStore,
        out TradeReceipt receipt,
        out FailReason failReason,
        out string failNote)
    {
        receipt = null;
        failReason = FailReason.None;
        failNote = null;

        if (draft == null || player == null || itemStore == null)
        {
            failReason = FailReason.InvalidDraft;
            failNote = "Missing draft/player/itemStore.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(draft.nodeId))
        {
            failReason = FailReason.InvalidDraft;
            failNote = "Draft missing nodeId.";
            return false;
        }

        if (draft.lines == null || draft.lines.Count == 0)
        {
            failReason = FailReason.InvalidDraft;
            failNote = "Draft has no lines.";
            return false;
        }

        int totalFeesPaid = 0;
        int creditsDelta = 0;

        var needRemove = new Dictionary<string, int>();
        var needAdd = new Dictionary<string, int>();

        foreach (var line in draft.lines)
        {
            if (line == null)
            {
                failReason = FailReason.InvalidLine;
                failNote = "Null line.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(line.offerId))
            {
                failReason = FailReason.InvalidLine;
                failNote = $"Line missing offerId for {line.itemId}.";
                return false;
            }

            var offer = FindOffer(offers, line.offerId);
            if (offer == null)
            {
                failReason = FailReason.OfferUnavailable;
                failNote = $"Offer '{line.offerId}' no longer exists.";
                return false;
            }

            if (!string.Equals(offer.itemId, line.itemId, StringComparison.Ordinal))
            {
                failReason = FailReason.OfferUnavailable;
                failNote = $"Offer '{line.offerId}' item mismatch.";
                return false;
            }

            bool wantBuyFromNode = line.direction == TradeDirection.BuyFromNode;
            bool offerIsSellToPlayer = offer.kind == MarketOfferKind.SellToPlayer;

            if (wantBuyFromNode != offerIsSellToPlayer)
            {
                failReason = FailReason.OfferUnavailable;
                failNote = $"Offer '{line.offerId}' direction mismatch.";
                return false;
            }

            if (offer.unitPrice != line.unitPrice)
            {
                failReason = FailReason.OfferUnavailable;
                failNote = $"Offer '{line.offerId}' price changed.";
                return false;
            }

            if (line.quantity > offer.quantityRemaining)
            {
                failReason = FailReason.OfferUnavailable;
                failNote = $"Offer '{offer.itemId}' has only {offer.quantityRemaining} remaining.";
                return false;
            }

            checked
            {
                int lineTotal = line.unitPrice * line.quantity;

                int fee = (feePolicy != null && nodeState != null)
                    ? Mathf.Max(0, feePolicy.ComputeFeeCredits(nodeState, line, lineTotal, timeBucket))
                    : 0;

                creditsDelta -= fee;
                totalFeesPaid += fee;

                switch (line.direction)
                {
                    case TradeDirection.BuyFromNode:
                        creditsDelta -= lineTotal;
                        Accumulate(needAdd, line.itemId, line.quantity);
                        break;

                    case TradeDirection.SellToNode:
                        creditsDelta += lineTotal;
                        Accumulate(needRemove, line.itemId, line.quantity);
                        break;

                    default:
                        failReason = FailReason.InvalidLine;
                        failNote = $"Unknown direction for {line.itemId}.";
                        return false;
                }
            }
        }

        int creditsAfter = player.credits + creditsDelta;
        if (creditsAfter < 0)
        {
            failReason = FailReason.InsufficientCredits;
            failNote = $"Not enough credits. Need {-creditsDelta}, have {player.credits}.";
            return false;
        }

        foreach (var kvp in needRemove)
        {
            int have = itemStore.GetCount(kvp.Key);
            if (have < kvp.Value)
            {
                failReason = FailReason.InsufficientItems;
                failNote = $"Not enough {kvp.Key}. Need {kvp.Value}, have {have}.";
                return false;
            }
        }

        // APPLY
        Debug.Log($"[TradeExec] APPLY store={(itemStore == null ? "<null>" : itemStore.GetType().Name)} creditsAfter={creditsAfter} needAdd={needAdd.Count} needRemove={needRemove.Count}");
        player.credits = creditsAfter;

        ApplyFeeMarketUpgrade(nodeState, totalFeesPaid);

        // Deplete offers
        if (offers != null)
        {
            foreach (var line in draft.lines)
            {
                var offer = FindOffer(offers, line.offerId);
                if (offer != null)
                    offer.quantityRemaining = Mathf.Max(0, offer.quantityRemaining - line.quantity);
            }
        }


        foreach (var kvp in needRemove)
        {
            Debug.Log($"[TradeExec] REMOVE {kvp.Key} x{kvp.Value}");
            itemStore.Remove(kvp.Key, kvp.Value);

        }

        foreach (var kvp in needAdd)
        {
            Debug.Log($"[TradeApply] store={(itemStore == null ? "<null>" : itemStore.GetType().Name)} node={(player != null ? player.currentNodeId : "<no-player>")}");
            itemStore.Add(kvp.Key, kvp.Value);
        }

        ApplyItemEmbargoes(nodeState, draft.lines, timeBucket, cooldownSellToPlayerBuckets, cooldownBuyFromPlayerBuckets);

        receipt = new TradeReceipt
        {
            nodeId = draft.nodeId,
            vendorId = draft.vendorId,
            creditsDelta = creditsDelta,
            totalFeesPaid = totalFeesPaid,
            appliedLines = new List<TradeLine>(draft.lines)
        };

        return true;
    }

    private static void ApplyItemEmbargoes(
        MapNodeState nodeState,
        List<TradeLine> lines,
        int timeBucket,
        int cooldownSellToPlayerBuckets,
        int cooldownBuyFromPlayerBuckets)
    {
        if (nodeState == null) return;
        var market = nodeState.MarketMutable;
        if (market == null) return;
        if (lines == null || lines.Count == 0) return;

        cooldownSellToPlayerBuckets = Mathf.Max(0, cooldownSellToPlayerBuckets);
        cooldownBuyFromPlayerBuckets = Mathf.Max(0, cooldownBuyFromPlayerBuckets);

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line == null) continue;
            if (line.quantity <= 0) continue;
            if (string.IsNullOrWhiteSpace(line.itemId)) continue;

            if (line.direction == TradeDirection.SellToNode)
            {
                int until = timeBucket + cooldownSellToPlayerBuckets;
                if (cooldownSellToPlayerBuckets > 0)
                    market.SetEmbargo(MarketOfferKind.SellToPlayer, line.itemId, until);
            }
            else if (line.direction == TradeDirection.BuyFromNode)
            {
                int until = timeBucket + cooldownBuyFromPlayerBuckets;
                if (cooldownBuyFromPlayerBuckets > 0)
                    market.SetEmbargo(MarketOfferKind.BuyFromPlayer, line.itemId, until);
            }
        }
    }

    private static void Accumulate(Dictionary<string, int> dict, string itemId, int amount)
    {
        if (dict.TryGetValue(itemId, out var cur)) dict[itemId] = cur + amount;
        else dict[itemId] = amount;
    }

    private static NodeMarketOffer FindOffer(IReadOnlyList<NodeMarketOffer> offers, string offerId)
    {
        if (offers == null) return null;
        for (int i = 0; i < offers.Count; i++)
        {
            var o = offers[i];
            if (o != null && o.offerId == offerId) return o;
        }
        return null;
    }

    private static void ApplyFeeMarketUpgrade(MapNodeState node, int totalFeesPaid)
    {
        if (node == null) return;
        if (totalFeesPaid <= 0) return;

        float impulse = Mathf.Sqrt(totalFeesPaid) * 0.01f;
        impulse = Mathf.Clamp(impulse, 0f, 0.15f);

        if (node.TryGetStat(NodeStatId.TradeRating, out var trade))
        {
            trade.value = Mathf.Clamp(trade.value + impulse, trade.minValue, trade.maxValue);
            node.SetStatPreserveVelocity(NodeStatId.TradeRating, trade);
        }
    }
}
