using System;
using System.Collections.Generic;
using UnityEngine;
using MiniGames;
using WorldMap.Player.Trade;

public static class TradeEffectApplier
{
    private static bool applyingTrade;
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
        if (applyingTrade)
        {
            failNote = "Another trade is being applied.";
            return false;
        }
        if (!GameplayAuthority.CanRun(GameplayAuthorityMode.SinglePlayerOrAuthoritative))
        {
            failNote = "Trade requires gameplay authority.";
            return false;
        }
        applyingTrade = true;
        try
        {
            if (effect.kind != MiniGameEffectKind.Transaction || effect.system != "Trade")
            {
                failNote = "Invalid trade effect.";
                return false;
            }
            if (!(itemStore is ITransactionalTradeItemStore transactionalStore) || activeOffers == null)
            {
                failNote = "Trade requires an atomic item store and active offers.";
                return false;
            }
            var draft = JsonUtility.FromJson<TradeTransactionDraft>(effect.payloadJson);
            if (draft?.lines == null || draft.lines.Count == 0)
            {
                failNote = "Trade payload has no lines.";
                return false;
            }

            var validatedLines = new List<TradeLine>(draft.lines.Count);
            var originalStock = new Dictionary<NodeMarketOffer, int>();
            var remainingStock = new Dictionary<NodeMarketOffer, int>();
            int tradeBalance = 0;
            foreach (var line in draft.lines)
            {
                if (line == null || line.quantity <= 0) continue;
                var offer = FindOffer(activeOffers, line.offerId);
                if (offer == null || offer.quantityRemaining <= 0)
                {
                    failNote = $"Offer is missing or sold out: '{line.offerId}'.";
                    return false;
                }
                var direction = offer.kind == MarketOfferKind.SellToPlayer
                    ? TradeDirection.BuyFromNode : TradeDirection.SellToNode;
                if (!string.Equals(line.itemId, offer.itemId, StringComparison.Ordinal) || line.direction != direction)
                {
                    failNote = $"Item or direction mismatch for offer '{line.offerId}'.";
                    return false;
                }
                if (!remainingStock.TryGetValue(offer, out int available))
                {
                    available = offer.quantityRemaining;
                    originalStock.Add(offer, available);
                }
                // Preserve clamping, but share one allowance across duplicate lines.
                int quantity = Mathf.Min(line.quantity, available);
                if (quantity == 0) continue;
                remainingStock[offer] = available - quantity;
                int unitPrice = Mathf.Max(0, offer.unitPrice);
                int total = checked(unitPrice * quantity);
                tradeBalance = checked(tradeBalance + (direction == TradeDirection.BuyFromNode ? -total : total));
                validatedLines.Add(new TradeLine
                {
                    offerId = offer.offerId, itemId = offer.itemId,
                    quantity = quantity, unitPrice = unitPrice, direction = direction
                });
            }
            if (validatedLines.Count == 0)
            {
                failNote = "No valid trade lines.";
                return false;
            }
            int fee = ComputeFeeTotal(feePolicy, nodeState, validatedLines, timeBucket);
            int finalDelta = checked(tradeBalance - fee);

            var treasury = MoneyChestTreasuryService.Instance;
            if (treasury == null)
            {
                failNote = "No active money chest.";
                return false;
            }
            if (!treasury.TryPrepareTradeBalance(finalDelta, out var money, out failNote)) return false;
            using (money)
            {
                if (!transactionalStore.TryPrepareTrade(validatedLines, out var items, out failNote)) return false;
                if (items == null)
                {
                    failNote = "Item store did not prepare the trade.";
                    return false;
                }
                using (items)
                {
                    var market = nodeState?.MarketMutable;
                    var oldSellEmbargo = market?.embargoSellToPlayerUntil;
                    var oldBuyEmbargo = market?.embargoBuyFromPlayerUntil;
                    var plannedMarket = market == null ? null : new NodeMarketState
                    {
                        embargoSellToPlayerUntil = oldSellEmbargo == null ? null :
                            new Dictionary<string, int>(oldSellEmbargo, oldSellEmbargo.Comparer),
                        embargoBuyFromPlayerUntil = oldBuyEmbargo == null ? null :
                            new Dictionary<string, int>(oldBuyEmbargo, oldBuyEmbargo.Comparer)
                    };
                    foreach (var line in validatedLines)
                        ApplyEmbargo(plannedMarket, FindOffer(activeOffers, line.offerId), line, timeBucket,
                            cooldownSellToPlayerBuckets, cooldownBuyFromPlayerBuckets);
                    var preparedReceipt = BuildReceipt(draft.nodeId, validatedLines, tradeBalance, fee, finalDelta);

                    foreach (var entry in originalStock)
                    {
                        if (FindOffer(activeOffers, entry.Key.offerId) != entry.Key ||
                            entry.Key.quantityRemaining != entry.Value)
                        {
                            failNote = "Market offers changed while preparing trade.";
                            return false;
                        }
                    }
                    foreach (var line in validatedLines)
                    {
                        var offer = FindOffer(activeOffers, line.offerId);
                        if (offer.itemId != line.itemId || Mathf.Max(0, offer.unitPrice) != line.unitPrice ||
                            (offer.kind == MarketOfferKind.SellToPlayer) != (line.direction == TradeDirection.BuyFromNode))
                        {
                            failNote = "Market prices or items changed while preparing trade.";
                            return false;
                        }
                    }
                    if (!items.Validate(out failNote) || !money.Validate(out failNote)) return false;

                    // No callbacks, object destruction, or money notifications until all writes succeed.
                    // Disposing an incomplete preparation restores exact items/quantities and chest state.
                    try
                    {
                        items.Apply();
                        money.Apply();
                        foreach (var entry in remainingStock) entry.Key.quantityRemaining = entry.Value;
                        if (market != null)
                        {
                            market.embargoSellToPlayerUntil = plannedMarket.embargoSellToPlayerUntil;
                            market.embargoBuyFromPlayerUntil = plannedMarket.embargoBuyFromPlayerUntil;
                        }
                    }
                    catch
                    {
                        foreach (var entry in originalStock) entry.Key.quantityRemaining = entry.Value;
                        if (market != null)
                        {
                            market.embargoSellToPlayerUntil = oldSellEmbargo;
                            market.embargoBuyFromPlayerUntil = oldBuyEmbargo;
                        }
                        throw;
                    }
                    items.Complete();
                    money.Complete();
                    receipt = preparedReceipt;
                    // Observer failures cannot undo the committed transaction or report it as failed.
                    try { items.Publish(); } catch (Exception ex) { Debug.LogException(ex); }
                    try { money.Publish(); } catch (Exception ex) { Debug.LogException(ex); }
                    try { GameMessageTradeReporter.Report(receipt, null); } catch (Exception ex) { Debug.LogException(ex); }
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            failNote = ex is OverflowException ? "Trade amounts exceed supported limits." : $"Could not apply trade: {ex.Message}";
            return false;
        }
        finally
        {
            applyingTrade = false;
        }
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
        NodeMarketState market,
        NodeMarketOffer offer,
        TradeLine line,
        int timeBucket,
        int cooldownSellToPlayerBuckets,
        int cooldownBuyFromPlayerBuckets)
    {
        if (market == null)
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

        market.SetEmbargo(
            offer.kind,
            line.itemId,
            checked(timeBucket + cooldown));
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
            int lineTotal = checked(unitPrice * quantity);

            if (lineTotal <= 0)
                continue;

            int fee = feePolicy.ComputeFeeCredits(
                nodeState,
                line,
                lineTotal,
                timeBucket);

            total = checked(total + Mathf.Max(0, fee));
        }

        return Mathf.Max(0, total);
    }
}
