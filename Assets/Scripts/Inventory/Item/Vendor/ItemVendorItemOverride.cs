using System;
using UnityEngine;

[Serializable]
public sealed class ItemVendorItemOverride
{
    [Header("Item Match")]
    public string itemId;
    public ItemDefinition itemDefinition;

    [Header("Availability")]
    [Tooltip("Exclude this item from this vendor even if it matches the catalog/category rules.")]
    public bool exclude;

    [Tooltip("Include this item even if it fails this vendor's category filters. Does not bypass ItemDefinition tradable/sacred/vendor flags.")]
    public bool forceInclude;

    [Header("Price")]
    public bool usePriceOverride;

    [Min(0)]
    public int priceOverride;

    [Tooltip("Multiplies this vendor's normal sell price multiplier. 1 = no change.")]
    [Min(0f)]
    public float priceMultiplier = 1f;

    [Header("Stock")]
    public bool overrideStock;

    [Tooltip("-1 means infinite stock for v0.")]
    public int stock = -1;

    public float PriceMultiplier => priceMultiplier <= 0f ? 1f : priceMultiplier;

    public bool Matches(ItemDefinition def)
    {
        if (def == null)
            return false;

        if (itemDefinition != null)
            return itemDefinition == def;

        if (!string.IsNullOrWhiteSpace(itemId))
            return def.ItemId == itemId;

        return false;
    }
}