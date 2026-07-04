using System.Collections.Generic;
using UnityEngine;

public static class ItemVendorSellValueUtility
{
    public static bool CanVendorBuyItem(
        ItemVendorServiceDefinition vendor,
        ItemDefinition def,
        out string reason)
    {
        reason = null;

        if (vendor == null)
        {
            reason = "Missing vendor.";
            return false;
        }

        if (!vendor.CanBuyFromPlayer)
        {
            reason = "This vendor is not buying items.";
            return false;
        }

        if (def == null)
        {
            reason = "Missing item definition.";
            return false;
        }

        if (!def.CanBeBoughtByItemVendors)
        {
            reason = "Vendor will not buy this item.";
            return false;
        }

        ItemVendorItemOverride itemOverride = vendor.FindOverride(def);
        if (itemOverride != null && itemOverride.exclude)
        {
            reason = "Vendor refuses this item.";
            return false;
        }

        bool forceInclude = itemOverride != null && itemOverride.forceInclude;
        if (forceInclude)
            return true;

        ItemCategoryFlags itemCategories = def.ItemCategories;

        ItemCategoryFlags allowed = vendor.AllowedBuyCategories;
        if (allowed != ItemCategoryFlags.None && (itemCategories & allowed) == 0)
        {
            reason = "Vendor does not buy this item category.";
            return false;
        }

        ItemCategoryFlags blocked = vendor.BlockedBuyCategories;
        if (blocked != ItemCategoryFlags.None && (itemCategories & blocked) != 0)
        {
            reason = "Vendor refuses this item category.";
            return false;
        }

        return true;
    }

    public static int GetUnitSellPrice(
        ItemVendorServiceDefinition vendor,
        ItemDefinition def)
    {
        if (vendor == null || def == null)
            return 0;

        float multiplier =
            Mathf.Max(0f, def.DefaultVendorBuyMultiplier) *
            Mathf.Max(0f, vendor.BuyFromPlayerMultiplier);

        ItemVendorItemOverride itemOverride = vendor.FindOverride(def);
        if (itemOverride != null)
            multiplier *= itemOverride.PriceMultiplier;

        return Mathf.Max(0, Mathf.RoundToInt(def.BasePrice * multiplier));
    }

    public static ItemVendorSellPreview BuildPreviewForSource(
        ItemVendorServiceDefinition vendor,
        ItemVendorSellSource source,
        int quantity)
    {
        if (source == null)
        {
            return new ItemVendorSellPreview
            {
                canSell = false,
                blockReason = "Missing sell source."
            };
        }

        string sourceBlock = source.BlockReason;
        if (!string.IsNullOrWhiteSpace(sourceBlock))
        {
            return new ItemVendorSellPreview
            {
                canSell = false,
                blockReason = sourceBlock
            };
        }

        ItemInstance item = source.Item;
        if (item == null || item.Definition == null)
        {
            return new ItemVendorSellPreview
            {
                canSell = false,
                blockReason = "Missing item."
            };
        }

        quantity = Mathf.Clamp(quantity, 1, source.MaxQuantity);

        return BuildPreviewForItem(vendor, item, quantity);
    }

    public static ItemVendorSellPreview BuildPreviewForItem(
        ItemVendorServiceDefinition vendor,
        ItemInstance item,
        int quantity)
    {
        ItemVendorSellPreview preview = new ItemVendorSellPreview();

        if (item == null || item.Definition == null)
        {
            preview.canSell = false;
            preview.blockReason = "Missing item.";
            return preview;
        }

        quantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, item.Quantity));

        if (!CanVendorBuyItem(vendor, item.Definition, out string reason))
        {
            preview.canSell = false;
            preview.blockReason = reason;
            return preview;
        }

        int unit = GetUnitSellPrice(vendor, item.Definition);
        preview.itemValue = unit * quantity;

        HashSet<string> visited = new HashSet<string>();
        AddContainerContentsValue(vendor, item, preview, visited);

        preview.totalValue = Mathf.Max(0, preview.itemValue + preview.containedValue);

        if (preview.hasBlockedContainedItems)
        {
            preview.canSell = false;
            preview.blockReason = "Container has contents this vendor cannot buy. Remove those items first.";
            return preview;
        }

        preview.canSell = true;
        return preview;
    }

    private static void AddContainerContentsValue(
        ItemVendorServiceDefinition vendor,
        ItemInstance containerItem,
        ItemVendorSellPreview preview,
        HashSet<string> visited)
    {
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
            return;

        string id = containerItem.InstanceId;
        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!visited.Add(id))
                return;
        }

        if (containerItem.ContainerState.Slots == null)
            return;

        for (int i = 0; i < containerItem.ContainerState.Slots.Count; i++)
        {
            InventorySlot slot = containerItem.ContainerState.Slots[i];
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            ItemInstance child = slot.Instance;
            if (child.Definition == null || child.Quantity <= 0)
                continue;

            preview.hasContainedItems = true;

            string label = GetItemLabel(child);
            int qty = Mathf.Max(1, child.Quantity);

            if (!CanVendorBuyItem(vendor, child.Definition, out string reason))
            {
                preview.hasBlockedContainedItems = true;
                preview.containedLines.Add($"{label} x{qty} - not accepted ({reason})");
            }
            else
            {
                int unit = GetUnitSellPrice(vendor, child.Definition);
                int lineValue = unit * qty;

                preview.containedValue += lineValue;
                preview.containedLines.Add($"{label} x{qty} - ${lineValue:n0}");
            }

            AddContainerContentsValue(vendor, child, preview, visited);
        }
    }

    public static string GetItemLabel(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return "<missing item>";

        if (!string.IsNullOrWhiteSpace(item.Definition.DisplayName))
            return item.Definition.DisplayName;

        return item.Definition.ItemId;
    }
}