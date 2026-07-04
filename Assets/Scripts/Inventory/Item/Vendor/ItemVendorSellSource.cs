using System;
using UnityEngine;

public enum ItemVendorSellSourceKind
{
    PlayerHotbar,
    PlayerEquipment,
    PlayerContainer,
    BoatWorldItem,
    BoatWorldContainer,
    BoatStorageModule
}

public readonly struct ItemVendorSellRemovalResult
{
    public readonly bool success;
    public readonly ItemInstance removedItem;
    public readonly string message;

    private ItemVendorSellRemovalResult(bool success, ItemInstance removedItem, string message)
    {
        this.success = success;
        this.removedItem = removedItem;
        this.message = message;
    }

    public static ItemVendorSellRemovalResult Success(ItemInstance removedItem, string message = null)
    {
        return new ItemVendorSellRemovalResult(true, removedItem, message);
    }

    public static ItemVendorSellRemovalResult Fail(string message)
    {
        return new ItemVendorSellRemovalResult(false, null, message);
    }
}

public sealed class ItemVendorSellSource
{
    private readonly Func<ItemInstance> getItem;
    private readonly Func<int> getMaxQuantity;
    private readonly Func<string> getBlockReason;
    private readonly Func<int, ItemVendorSellRemovalResult> remove;

    public string SourceKey { get; }
    public ItemVendorSellSourceKind Kind { get; }
    public string SourceLabel { get; }
    public int Depth { get; }

    public ItemInstance Item => getItem != null ? getItem() : null;

    public int MaxQuantity
    {
        get
        {
            int fromFunc = getMaxQuantity != null ? getMaxQuantity() : 0;
            if (fromFunc > 0)
                return fromFunc;

            ItemInstance item = Item;
            return item != null ? Mathf.Max(0, item.Quantity) : 0;
        }
    }

    public string BlockReason => getBlockReason != null ? getBlockReason() : null;

    public bool IsValid
    {
        get
        {
            ItemInstance item = Item;
            return item != null &&
                   item.Definition != null &&
                   item.Quantity > 0 &&
                   MaxQuantity > 0;
        }
    }

    public ItemVendorSellSource(
        string sourceKey,
        ItemVendorSellSourceKind kind,
        string sourceLabel,
        int depth,
        Func<ItemInstance> getItem,
        Func<int> getMaxQuantity,
        Func<string> getBlockReason,
        Func<int, ItemVendorSellRemovalResult> remove)
    {
        SourceKey = string.IsNullOrWhiteSpace(sourceKey)
            ? Guid.NewGuid().ToString("N")
            : sourceKey;

        Kind = kind;
        SourceLabel = string.IsNullOrWhiteSpace(sourceLabel)
            ? kind.ToString()
            : sourceLabel;

        Depth = Mathf.Max(0, depth);

        this.getItem = getItem;
        this.getMaxQuantity = getMaxQuantity;
        this.getBlockReason = getBlockReason;
        this.remove = remove;
    }

    public bool TryRemove(int quantity, out ItemInstance removedItem, out string message)
    {
        removedItem = null;
        message = null;

        if (!IsValid)
        {
            message = "Item source is no longer valid.";
            return false;
        }

        string blockReason = BlockReason;
        if (!string.IsNullOrWhiteSpace(blockReason))
        {
            message = blockReason;
            return false;
        }

        quantity = Mathf.Clamp(quantity, 1, MaxQuantity);

        if (remove == null)
        {
            message = "Item source cannot be removed.";
            return false;
        }

        ItemVendorSellRemovalResult result = remove(quantity);

        removedItem = result.removedItem;
        message = result.message;

        if (!result.success)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = "Failed to remove item.";

            return false;
        }

        if (removedItem == null || removedItem.Definition == null)
        {
            message = "Removed item was invalid.";
            return false;
        }

        return true;
    }
}