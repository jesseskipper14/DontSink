using MiniGames;
using System;
using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

public static class TradeEffectApplier
{
    /// <summary>
    /// Applies a Transaction MiniGameEffect to player state using TradeService.
    /// Returns true if applied successfully.
    /// </summary>
    public static bool TryApply(
        MiniGameEffect effect,
        WorldMapPlayerState player,
        IReadOnlyList<NodeMarketOffer> offers,

        // NEW inputs for fees + item-memory (2B)
        MapNodeState nodeState,
        ITradeFeePolicy feePolicy,
        int timeBucket,
        int cooldownSellToPlayerBuckets,
        int cooldownBuyFromPlayerBuckets,

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
            out receipt,
            out var reason,         // <-- YOU WERE MISSING THIS
            out var note))
        {
            failNote = note ?? reason.ToString();
            return false;
        }

        return true;
    }
}
