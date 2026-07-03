using System;
using UnityEngine;

[Serializable]
public sealed class ItemVendorBuyRule
{
    [Tooltip("Optional item ID match. Leave empty to match by tag/category.")]
    public string itemId;

    [Tooltip("Optional category/tag match. Examples: shiny, salvage, diving, food.")]
    public string tagOrCategory;

    [Range(0f, 2f)]
    public float priceMultiplier = 0.25f;

    public bool acceptsItem = true;
}