using System;
using UnityEngine;
using WorldMap.Player.Trade;

/// <summary>
/// Converts successful authoritative trade receipts into concise player-facing
/// log entries.
///
/// The explicit ResourceCatalog is preferred when available. When the caller is
/// a central authority that does not own a catalog reference, the reporter can
/// resolve already-loaded ResourceDef assets before falling back to a humanized
/// item ID.
/// </summary>
public static class GameMessageTradeReporter
{
    public static void Report(
        TradeService.TradeReceipt receipt,
        ResourceCatalog resourceCatalog)
    {
        if (receipt == null ||
            receipt.appliedLines == null)
        {
            return;
        }

        for (int i = 0;
             i < receipt.appliedLines.Count;
             i++)
        {
            TradeLine line =
                receipt.appliedLines[i];

            if (line == null ||
                line.quantity <= 0)
            {
                continue;
            }

            string itemName =
                ResolveItemName(
                    line.itemId,
                    resourceCatalog);

            string crateWord =
                line.quantity == 1
                    ? "crate"
                    : "crates";

            switch (line.direction)
            {
                case TradeDirection.BuyFromNode:
                    GameMessageService.PostInfo(
                        $"Purchased {line.quantity} {crateWord} of {itemName}.");
                    break;

                case TradeDirection.SellToNode:
                    GameMessageService.PostInfo(
                        $"Sold {line.quantity} {crateWord} of {itemName}.");
                    break;
            }
        }
    }

    private static string ResolveItemName(
        string itemId,
        ResourceCatalog resourceCatalog)
    {
        if (TryResolveFromCatalog(
                itemId,
                resourceCatalog,
                out string catalogName))
        {
            return catalogName;
        }

        if (TryResolveFromLoadedDefinitions(
                itemId,
                out string loadedName))
        {
            return loadedName;
        }

        return HumanizeItemId(
            itemId);
    }

    private static bool TryResolveFromCatalog(
        string itemId,
        ResourceCatalog resourceCatalog,
        out string displayName)
    {
        displayName = null;

        if (resourceCatalog == null ||
            string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (!resourceCatalog.TryGet(
                itemId,
                out ResourceDef definition) ||
            definition == null ||
            string.IsNullOrWhiteSpace(
                definition.displayName))
        {
            return false;
        }

        displayName =
            definition.displayName.Trim();

        return true;
    }

    private static bool TryResolveFromLoadedDefinitions(
        string itemId,
        out string displayName)
    {
        displayName = null;

        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        ResourceDef[] definitions =
            Resources.FindObjectsOfTypeAll<ResourceDef>();

        if (definitions == null)
            return false;

        for (int i = 0;
             i < definitions.Length;
             i++)
        {
            ResourceDef definition =
                definitions[i];

            if (definition == null)
                continue;

            if (!string.Equals(
                    definition.itemId,
                    itemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(
                    definition.displayName))
            {
                return false;
            }

            displayName =
                definition.displayName.Trim();

            return true;
        }

        return false;
    }

    private static string HumanizeItemId(
        string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "Unknown Cargo";

        string cleaned =
            itemId.Trim()
                .Replace(
                    "cargo_crate_",
                    string.Empty)
                .Replace(
                    "crate_",
                    string.Empty)
                .Replace(
                    '_',
                    ' ')
                .Replace(
                    '-',
                    ' ');

        if (cleaned.Length <= 0)
            return "Unknown Cargo";

        return
            char.ToUpperInvariant(
                cleaned[0]) +
            cleaned.Substring(1);
    }
}
