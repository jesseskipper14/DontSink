using System;
using System.Collections.Generic;
using UnityEngine;
using MiniGames;
using WorldMap.Player.Trade;

public static class TradeEffectApplier
{
    public static bool TryApply(
        MiniGameEffect effect,
        WorldMapPlayerState player,
        IReadOnlyList<NodeMarketOffer> activeOffers,
        MapNodeState nodeState,
        ITradeFeePolicy feePolicy,
        int timeBucket,
        int cooldownSellToPlayerBuckets,
        int cooldownBuyFromPlayerBuckets,
        IItemStore itemStore,
        out TradeService.TradeReceipt receipt,
        out string failNote)
    {
        receipt = null;
        failNote = null;

        if (effect.kind != MiniGameEffectKind.Transaction)
        {
            failNote = $"Wrong effect kind: {effect.kind}";
            return false;
        }

        if (!string.Equals(effect.system, "Trade", StringComparison.Ordinal))
        {
            failNote = $"Wrong effect system: {effect.system}";
            return false;
        }

        if (itemStore == null)
        {
            failNote = "Missing item store.";
            return false;
        }

        if (activeOffers == null)
        {
            failNote = "Missing active offers.";
            return false;
        }

        TradeTransactionDraft draft;

        try
        {
            draft = JsonUtility.FromJson<TradeTransactionDraft>(effect.payloadJson);
        }
        catch (Exception ex)
        {
            failNote = $"Could not parse trade payload: {ex.Message}";
            return false;
        }

        if (draft == null || draft.lines == null || draft.lines.Count == 0)
        {
            failNote = "Trade payload has no lines.";
            return false;
        }

        if (!HasActiveMoneyChest())
        {
            failNote = "No active money chest.";
            return false;
        }

        List<TradeLine> validatedLines = new List<TradeLine>(draft.lines.Count);

        int tradeBalance = 0;

        for (int i = 0; i < draft.lines.Count; i++)
        {
            TradeLine line = draft.lines[i];

            if (line == null)
                continue;

            if (line.quantity <= 0)
                continue;

            NodeMarketOffer offer = FindOffer(activeOffers, line.offerId);
            if (offer == null)
            {
                failNote = $"Offer no longer exists: '{line.offerId}'.";
                return false;
            }

            if (offer.quantityRemaining <= 0)
            {
                failNote = $"Offer is sold out: '{line.offerId}'.";
                return false;
            }

            int qty = Mathf.Min(line.quantity, offer.quantityRemaining);
            if (qty <= 0)
                continue;

            if (!string.Equals(line.itemId, offer.itemId, StringComparison.Ordinal))
            {
                failNote = $"Line item mismatch. line='{line.itemId}', offer='{offer.itemId}'.";
                return false;
            }

            TradeDirection expectedDirection =
                offer.kind == MarketOfferKind.SellToPlayer
                    ? TradeDirection.BuyFromNode
                    : TradeDirection.SellToNode;

            if (line.direction != expectedDirection)
            {
                failNote =
                    $"Line direction mismatch for offer '{offer.offerId}'. " +
                    $"Expected {expectedDirection}, got {line.direction}.";
                return false;
            }

            if (line.direction == TradeDirection.SellToNode)
            {
                int have = itemStore.GetCount(line.itemId);
                if (have < qty)
                {
                    failNote = $"Not enough '{line.itemId}' to sell. Have {have}, need {qty}.";
                    return false;
                }
            }

            int unitPrice = Mathf.Max(0, offer.unitPrice);
            int lineTotal = unitPrice * qty;

            if (line.direction == TradeDirection.BuyFromNode)
                tradeBalance -= lineTotal;
            else if (line.direction == TradeDirection.SellToNode)
                tradeBalance += lineTotal;

            validatedLines.Add(new TradeLine
            {
                offerId = offer.offerId,
                itemId = offer.itemId,
                quantity = qty,
                unitPrice = unitPrice,
                direction = line.direction
            });
        }

        if (validatedLines.Count == 0)
        {
            failNote = "No valid trade lines.";
            return false;
        }

        int fee = ComputeFeeTotal(
            feePolicy,
            nodeState,
            validatedLines,
            timeBucket);

        int finalDelta = tradeBalance - fee;

        if (finalDelta < 0)
        {
            int cost = -finalDelta;

            if (!MoneyService.CanSpend(cost))
            {
                failNote = $"Not enough money in active chest. Need {cost}, have {MoneyService.Balance}.";
                return false;
            }
        }

        // All validation is done. Now mutate.
        // If this still breaks, congratulations, we have invented transactional cargo banking.
        for (int i = 0; i < validatedLines.Count; i++)
        {
            TradeLine line = validatedLines[i];
            NodeMarketOffer offer = FindOffer(activeOffers, line.offerId);

            if (offer == null)
            {
                failNote = $"Offer disappeared during apply: '{line.offerId}'.";
                return false;
            }

            if (line.direction == TradeDirection.BuyFromNode)
            {
                itemStore.Add(line.itemId, line.quantity);
            }
            else if (line.direction == TradeDirection.SellToNode)
            {
                bool removed = itemStore.Remove(line.itemId, line.quantity);
                if (!removed)
                {
                    failNote = $"Failed to remove '{line.itemId}' x{line.quantity} during apply.";
                    return false;
                }
            }

            offer.quantityRemaining = Mathf.Max(0, offer.quantityRemaining - line.quantity);

            ApplyEmbargo(
                nodeState,
                offer,
                line,
                timeBucket,
                cooldownSellToPlayerBuckets,
                cooldownBuyFromPlayerBuckets);
        }

        bool moneyApplied = true;

        if (finalDelta < 0)
            moneyApplied = MoneyService.TrySpend(-finalDelta);
        else if (finalDelta > 0)
            moneyApplied = MoneyService.AddMoney(finalDelta);

        if (!moneyApplied)
        {
            failNote =
                $"Trade items/offers were applied, but MoneyService failed finalDelta={finalDelta}. " +
                "This should be rare because validation already checked money. Annoying.";
            return false;
        }

        receipt = BuildReceipt(
            draft.nodeId,
            validatedLines,
            tradeBalance,
            fee,
            finalDelta);

        return true;
    }

    private static bool HasActiveMoneyChest()
    {
        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        return treasury != null && treasury.HasActiveChest;
    }

    private static NodeMarketOffer FindOffer(
        IReadOnlyList<NodeMarketOffer> offers,
        string offerId)
    {
        if (offers == null || string.IsNullOrWhiteSpace(offerId))
            return null;

        for (int i = 0; i < offers.Count; i++)
        {
            NodeMarketOffer offer = offers[i];
            if (offer == null)
                continue;

            if (string.Equals(offer.offerId, offerId, StringComparison.Ordinal))
                return offer;
        }

        return null;
    }

    private static void ApplyEmbargo(
        MapNodeState nodeState,
        NodeMarketOffer offer,
        TradeLine line,
        int timeBucket,
        int cooldownSellToPlayerBuckets,
        int cooldownBuyFromPlayerBuckets)
    {
        if (nodeState == null || nodeState.MarketMutable == null)
            return;

        if (offer == null || line == null)
            return;

        if (string.IsNullOrWhiteSpace(line.itemId))
            return;

        int cooldown =
            offer.kind == MarketOfferKind.SellToPlayer
                ? cooldownSellToPlayerBuckets
                : cooldownBuyFromPlayerBuckets;

        if (cooldown <= 0)
            return;

        nodeState.MarketMutable.SetEmbargo(
            offer.kind,
            line.itemId,
            timeBucket + cooldown);
    }

    private static TradeService.TradeReceipt BuildReceipt(
        string nodeId,
        List<TradeLine> appliedLines,
        int tradeBalance,
        int fee,
        int finalDelta)
    {
        TradeService.TradeReceipt receipt = new TradeService.TradeReceipt();

        receipt.nodeId = nodeId;
        receipt.creditsDelta = finalDelta;
        receipt.totalFeesPaid = fee;

        if (receipt.appliedLines == null)
            receipt.appliedLines = new List<TradeLine>();

        receipt.appliedLines.Clear();

        for (int i = 0; i < appliedLines.Count; i++)
            receipt.appliedLines.Add(appliedLines[i]);

        return receipt;
    }

    private static int ComputeFeeTotal(
    ITradeFeePolicy feePolicy,
    MapNodeState nodeState,
    List<TradeLine> lines,
    int timeBucket)
    {
        if (feePolicy == null || nodeState == null || lines == null || lines.Count == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            TradeLine line = lines[i];
            if (line == null)
                continue;

            int quantity = Mathf.Max(0, line.quantity);
            int unitPrice = Mathf.Max(0, line.unitPrice);
            int lineTotal = unitPrice * quantity;

            if (lineTotal <= 0)
                continue;

            int fee = feePolicy.ComputeFeeCredits(
                nodeState,
                line,
                lineTotal,
                timeBucket);

            total += Mathf.Max(0, fee);
        }

        return Mathf.Max(0, total);
    }
}