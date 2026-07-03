using System;
using UnityEngine;

[Serializable]
public sealed class ItemVendorSellOffer
{
    [Header("Item")]
    public string itemId;

    [Tooltip("Optional direct reference. If assigned, this wins over itemId.")]
    public ItemDefinition itemDefinition;

    [Header("Pricing")]
    public bool useBasePrice = true;

    [Min(0)]
    public int priceOverride = 100;

    [Min(0f)]
    public float priceMultiplier = 1f;

    [Header("Stock")]
    [Tooltip("-1 means infinite stock for v0.")]
    public int stock = -1;

    public ItemDefinition ResolveDefinition(IItemDefinitionResolver resolver)
    {
        if (itemDefinition != null)
            return itemDefinition;

        if (resolver == null || string.IsNullOrWhiteSpace(itemId))
            return null;

        return resolver.Resolve(itemId);
    }

    public int ResolvePrice(ItemDefinition resolvedDefinition)
    {
        if (!useBasePrice)
            return Mathf.Max(0, priceOverride);

        int sourcePrice = resolvedDefinition != null
            ? resolvedDefinition.BasePrice
            : priceOverride;

        return Mathf.Max(0, Mathf.RoundToInt(sourcePrice * Mathf.Max(0f, priceMultiplier)));
    }

    public string ResolveLabel(ItemDefinition resolvedDefinition)
    {
        if (resolvedDefinition != null)
            return resolvedDefinition.DisplayName;

        if (!string.IsNullOrWhiteSpace(itemId))
            return itemId;

        return "<missing item>";
    }
}