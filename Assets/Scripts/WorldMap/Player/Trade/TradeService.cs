using System;
using System.Collections.Generic;
using WorldMap.Player.Trade; // draft + line types live here

public sealed class TradeService
{
    public enum FailReason
    {
        None,
        InvalidDraft,
        InvalidLine,
        InsufficientCredits,
        InsufficientItems
    }

    [Serializable]
    public sealed class TradeReceipt
    {
        public int version = 1;
        public string nodeId;
        public string vendorId;

        public int creditsDelta; // + means player gained credits, - means player paid credits
        public List<TradeLine> appliedLines = new List<TradeLine>();
    }

    /// <summary>
    /// Validates and applies a transaction draft atomically to a player's state.
    /// Node inventory is NOT mutated. This is purely player-facing trade execution.
    /// </summary>
    public bool TryExecute(
        TradeTransactionDraft draft,
        WorldMapPlayerState player,
        out TradeReceipt receipt,
        out FailReason failReason,
        out string failNote)
    {
        receipt = null;
        failReason = FailReason.None;
        failNote = null;

        if (draft == null || player == null || player.inventory == null)
        {
            failReason = FailReason.InvalidDraft;
            failNote = "Missing draft/player/inventory.";
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

        // 1) Validate lines and compute totals + required item deltas WITHOUT mutating.
        int creditsDelta = 0;
        var needRemove = new Dictionary<string, int>(); // items player must give (sell)
        var needAdd = new Dictionary<string, int>();    // items player must receive (buy)

        foreach (var line in draft.lines)
        {
            if (line == null)
            {
                failReason = FailReason.InvalidLine;
                failNote = "Null line.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(line.itemId))
            {
                failReason = FailReason.InvalidLine;
                failNote = "Line missing itemId.";
                return false;
            }

            if (line.quantity <= 0)
            {
                failReason = FailReason.InvalidLine;
                failNote = $"Invalid quantity for {line.itemId}.";
                return false;
            }

            if (line.unitPrice < 0)
            {
                failReason = FailReason.InvalidLine;
                failNote = $"Invalid unitPrice for {line.itemId}.";
                return false;
            }

            checked
            {
                int lineTotal = line.unitPrice * line.quantity;

                switch (line.direction)
                {
                    case TradeDirection.BuyFromNode:
                        // Player pays credits, receives items
                        creditsDelta -= lineTotal;
                        Accumulate(needAdd, line.itemId, line.quantity);
                        break;

                    case TradeDirection.SellToNode:
                        // Player gives items, receives credits
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

        // 2) Validate player can afford credits and items.
        int creditsAfter = player.credits + creditsDelta;
        if (creditsAfter < 0)
        {
            failReason = FailReason.InsufficientCredits;
            failNote = $"Not enough credits. Need {-creditsDelta}, have {player.credits}.";
            return false;
        }

        foreach (var kvp in needRemove)
        {
            int have = player.inventory.GetCount(kvp.Key);
            if (have < kvp.Value)
            {
                failReason = FailReason.InsufficientItems;
                failNote = $"Not enough {kvp.Key}. Need {kvp.Value}, have {have}.";
                return false;
            }
        }

        // 3) Apply atomically (safe now because validated).
        player.credits = creditsAfter;

        foreach (var kvp in needRemove)
        {
            // Remove cannot fail after validation
            player.inventory.Remove(kvp.Key, kvp.Value);
        }

        foreach (var kvp in needAdd)
        {
            player.inventory.Add(kvp.Key, kvp.Value);
        }

        // 4) Receipt
        receipt = new TradeReceipt
        {
            nodeId = draft.nodeId,
            vendorId = draft.vendorId,
            creditsDelta = creditsDelta,
            appliedLines = new List<TradeLine>(draft.lines)
        };

        return true;
    }

    private static void Accumulate(Dictionary<string, int> dict, string itemId, int amount)
    {
        if (dict.TryGetValue(itemId, out var cur)) dict[itemId] = cur + amount;
        else dict[itemId] = amount;
    }
}
