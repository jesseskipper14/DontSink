using MiniGames;
using System;
using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

public static class TradeEffectApplier
{
    // Back-compat (WorldMap inventory).
    public static bool TryApply(
        MiniGameEffect effect,
        WorldMapPlayerState player,
        IReadOnlyList<NodeMarketOffer> offers,
        MapNodeState nodeState,
        ITradeFeePolicy feePolicy,
        int timeBucket,
        int cooldownSellToPlayerBuckets,
        int cooldownBuyFromPlayerBuckets,
        out TradeService.TradeReceipt receipt,
        out string failNote)
    {
        return TryApply(
            effect,
            player,
            offers,
            nodeState,
            feePolicy,
            timeBucket,
            cooldownSellToPlayerBuckets,
            cooldownBuyFromPlayerBuckets,
            itemStore: player != null ? (IItemStore)player.inventory : null,
            out receipt,
            out failNote);
    }

    /// <summary>
    /// NEW: Apply trade using an injected IItemStore.
    /// </summary>
    public static bool TryApply(
        MiniGameEffect effect,
        WorldMapPlayerState player,
        IReadOnlyList<NodeMarketOffer> offers,
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
            failNote = "Effect is not a Transaction.";
            return false;
        }

        if (effect.system != "Trade")
        {
            failNote = $"Unhandled transaction system '{effect.system}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(effect.payloadJson))
        {
            failNote = "Transaction effect missing payloadJson.";
            return false;
        }

        TradeTransactionDraft draft;
        try
        {
            draft = JsonUtility.FromJson<TradeTransactionDraft>(effect.payloadJson);
        }
        catch (Exception ex)
        {
            failNote = $"Failed to parse trade payload: {ex.Message}";
            return false;
        }

        if (draft == null)
        {
            failNote = "Parsed trade draft is null.";
            return false;
        }
        Debug.Log($"[TradeApply] store={(itemStore == null ? "<null>" : itemStore.GetType().Name)} node={(player != null ? player.currentNodeId : "<no-player>")}");
        var svc = new TradeService();

        if (!svc.TryExecute(
            draft,
            player,
            offers,
            nodeState,
            feePolicy,
            timeBucket,
            cooldownSellToPlayerBuckets,
            cooldownBuyFromPlayerBuckets,
            itemStore,
            out receipt,
            out var reason,
            out var note))
        {
            failNote = note ?? reason.ToString();
            return false;
        }

        return true;
    }
}
