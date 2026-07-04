using System.Collections.Generic;
using UnityEngine;

public static class BoatVendorModuleOfferBuilder
{
    public static List<ItemVendorSellOffer> BuildModuleOffers(
        BoatVendorPlaceholderServiceDefinition vendor,
        ItemDefinitionCatalog itemCatalog)
    {
        List<ItemVendorSellOffer> offers = new();

        if (vendor == null || itemCatalog == null)
            return offers;

        IReadOnlyList<ItemDefinition> items = itemCatalog.GetAllItems();
        if (items == null)
            return offers;

        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition def = items[i];
            if (!IsBoatVendorModule(def))
                continue;

            offers.Add(new ItemVendorSellOffer
            {
                itemId = def.ItemId,
                itemDefinition = def,

                useBasePrice = true,
                priceOverride = def.BasePrice,
                priceMultiplier = vendor.ModuleSellPriceMultiplier,

                stock = vendor.DefaultModuleStock
            });
        }

        return offers;
    }

    public static bool IsBoatVendorModule(ItemDefinition def)
    {
        if (def == null)
            return false;

        if (!def.IsInstallableModule)
            return false;

        if (!def.SoldByBoatVendors)
            return false;

        if (!def.Tradable)
            return false;

        if (def.IsSacred)
            return false;

        return true;
    }
}