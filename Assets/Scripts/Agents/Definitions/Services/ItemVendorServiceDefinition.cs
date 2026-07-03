using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Boat Game/Agents/Services/Item Vendor Service")]
public sealed class ItemVendorServiceDefinition : AgentServiceDefinition
{
    [Header("Catalog Stock")]
    [Tooltip("If true, this vendor generates sell offers from ItemDefinitionCatalog using item/vendor rules.")]
    [SerializeField] private bool generateSellOffersFromCatalog = true;

    [Tooltip("None means all categories are allowed unless blocked below.")]
    [SerializeField] private ItemCategoryFlags allowedSellCategories = ItemCategoryFlags.None;

    [Tooltip("Items matching these categories are excluded from this vendor.")]
    [SerializeField] private ItemCategoryFlags blockedSellCategories = ItemCategoryFlags.None;

    [Tooltip("0 means no limit.")]
    [SerializeField, Min(0)] private int maxGeneratedSellOffers = 0;

    [Header("Pricing")]
    [Tooltip("Multiplies ItemDefinition.BasePrice for this vendor.")]
    [SerializeField, Min(0f)] private float sellPriceMultiplier = 1f;

    [Header("Stock")]
    [Tooltip("-1 means infinite stock for v0.")]
    [SerializeField] private int defaultStock = -1;

    [Header("Per-Item Overrides")]
    [Tooltip("Optional. Use this for exclusions, forced inclusions, price overrides, or stock overrides. Do not populate every item here.")]
    [SerializeField] private ItemVendorItemOverride[] itemOverrides;

    [Header("Vendor Buys From Player - Future")]
    [SerializeField] private bool canBuyFromPlayer = true;

    [Tooltip("Global multiplier applied when this vendor buys from the player. ItemDefinition.DefaultVendorBuyMultiplier still applies later.")]
    [SerializeField, Min(0f)] private float buyFromPlayerMultiplier = 1f;

    [SerializeField] private ItemCategoryFlags allowedBuyCategories = ItemCategoryFlags.None;
    [SerializeField] private ItemCategoryFlags blockedBuyCategories = ItemCategoryFlags.Sacred;

    public override AgentServiceKind Kind => AgentServiceKind.ItemVendor;

    public bool GenerateSellOffersFromCatalog => generateSellOffersFromCatalog;
    public ItemCategoryFlags AllowedSellCategories => allowedSellCategories;
    public ItemCategoryFlags BlockedSellCategories => blockedSellCategories;
    public int MaxGeneratedSellOffers => Mathf.Max(0, maxGeneratedSellOffers);
    public float SellPriceMultiplier => Mathf.Max(0f, sellPriceMultiplier);
    public int DefaultStock => defaultStock;

    public bool CanBuyFromPlayer => canBuyFromPlayer;
    public float BuyFromPlayerMultiplier => Mathf.Max(0f, buyFromPlayerMultiplier);
    public ItemCategoryFlags AllowedBuyCategories => allowedBuyCategories;
    public ItemCategoryFlags BlockedBuyCategories => blockedBuyCategories;

    public IReadOnlyList<ItemVendorItemOverride> ItemOverrides => itemOverrides;

    public ItemVendorItemOverride FindOverride(ItemDefinition def)
    {
        if (def == null || itemOverrides == null)
            return null;

        for (int i = 0; i < itemOverrides.Length; i++)
        {
            ItemVendorItemOverride rule = itemOverrides[i];
            if (rule == null)
                continue;

            if (rule.Matches(def))
                return rule;
        }

        return null;
    }
}