using System.Collections.Generic;
using UnityEngine;

public static class ItemVendorOfferBuilder
{
    public static List<ItemVendorSellOffer> BuildSellOffers(
        ItemVendorServiceDefinition vendor,
        ItemDefinitionCatalog itemCatalog,
        ItemVendorAvailabilityContext availabilityContext)
    {
        List<ItemVendorSellOffer> offers = new();

        if (vendor == null || itemCatalog == null)
            return offers;

        if (!vendor.GenerateSellOffersFromCatalog)
            return offers;

        IReadOnlyList<ItemDefinition> items = itemCatalog.GetAllItems();
        if (items == null)
            return offers;

        int max = vendor.MaxGeneratedSellOffers;

        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition def = items[i];
            if (def == null)
                continue;

            ItemVendorItemOverride itemOverride = vendor.FindOverride(def);

            if (itemOverride != null && itemOverride.exclude)
                continue;

            bool forceInclude = itemOverride != null && itemOverride.forceInclude;

            if (!CanIncludeItem(vendor, def, forceInclude, availabilityContext))
                continue;

            offers.Add(BuildOffer(vendor, def, itemOverride));

            if (max > 0 && offers.Count >= max)
                break;
        }

        return offers;
    }

    private static bool CanIncludeItem(
        ItemVendorServiceDefinition vendor,
        ItemDefinition def,
        bool forceInclude,
        ItemVendorAvailabilityContext availabilityContext)
    {
        if (vendor == null || def == null)
            return false;

        // Hard item-level gate. Do not bypass sacred/tradable/vendor flags.
        if (!def.CanBeSoldByItemVendors)
            return false;

        if (!forceInclude)
        {
            if (!PassesCategoryFilters(vendor, def))
                return false;

            if (!ItemVendorAvailabilityUtility.CanVendorSellItem(
                    def,
                    availabilityContext,
                    out _))
                return false;
        }

        return true;
    }

    private static bool PassesCategoryFilters(
        ItemVendorServiceDefinition vendor,
        ItemDefinition def)
    {
        ItemCategoryFlags itemCategories = def.ItemCategories;

        ItemCategoryFlags allowed = vendor.AllowedSellCategories;
        if (allowed != ItemCategoryFlags.None && (itemCategories & allowed) == 0)
            return false;

        ItemCategoryFlags blocked = vendor.BlockedSellCategories;
        if (blocked != ItemCategoryFlags.None && (itemCategories & blocked) != 0)
            return false;

        return true;
    }

    private static ItemVendorSellOffer BuildOffer(
        ItemVendorServiceDefinition vendor,
        ItemDefinition def,
        ItemVendorItemOverride itemOverride)
    {
        bool useOverridePrice = itemOverride != null && itemOverride.usePriceOverride;

        float overrideMultiplier = itemOverride != null
            ? itemOverride.PriceMultiplier
            : 1f;

        int stock = vendor.DefaultStock;

        if (itemOverride != null && itemOverride.overrideStock)
            stock = itemOverride.stock;

        return new ItemVendorSellOffer
        {
            itemId = def.ItemId,
            itemDefinition = def,

            useBasePrice = !useOverridePrice,
            priceOverride = useOverridePrice ? Mathf.Max(0, itemOverride.priceOverride) : def.BasePrice,
            priceMultiplier = vendor.SellPriceMultiplier * overrideMultiplier,

            stock = stock
        };
    }
}