using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemVendorSellService : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    public ItemVendorSellPreview BuildPreview(
        ItemVendorServiceDefinition vendor,
        ItemVendorSellSource source,
        int quantity)
    {
        return ItemVendorSellValueUtility.BuildPreviewForSource(vendor, source, quantity);
    }

    public bool CanSell(
        ItemVendorServiceDefinition vendor,
        ItemVendorSellSource source,
        int quantity,
        out ItemVendorSellPreview preview,
        out string reason)
    {
        preview = BuildPreview(vendor, source, quantity);
        reason = preview != null ? preview.blockReason : "Missing sell preview.";

        return preview != null && preview.canSell;
    }

    public bool TrySell(
        ItemVendorServiceDefinition vendor,
        ItemVendorSellSource source,
        int quantity,
        out string message)
    {
        message = null;

        if (!CanSell(vendor, source, quantity, out ItemVendorSellPreview beforePreview, out string reason))
        {
            message = reason;
            return false;
        }

        quantity = Mathf.Clamp(quantity, 1, source.MaxQuantity);

        if (!source.TryRemove(quantity, out ItemInstance removedItem, out string removeMessage))
        {
            message = removeMessage;
            return false;
        }

        ItemVendorSellPreview finalPreview =
            ItemVendorSellValueUtility.BuildPreviewForItem(
                vendor,
                removedItem,
                Mathf.Max(1, removedItem.Quantity));

        if (finalPreview == null || !finalPreview.canSell)
        {
            // At this point the item was already removed. This should only happen if
            // definitions changed between preview and sale, which is designer sorcery.
            // We do not have a universal rollback for every source kind yet.
            message = finalPreview != null && !string.IsNullOrWhiteSpace(finalPreview.blockReason)
                ? $"Sold item was removed but could not be valued: {finalPreview.blockReason}"
                : "Sold item was removed but could not be valued.";

            Debug.LogWarning($"[ItemVendorSellService] {message}", this);
            return false;
        }

        int payout = Mathf.Max(0, finalPreview.totalValue);
        MoneyService.AddMoney(payout);

        string label = ItemVendorSellValueUtility.GetItemLabel(removedItem);
        message = $"Sold {label} for ${payout:n0}.";

        if (!string.IsNullOrWhiteSpace(removeMessage))
            message += $" {removeMessage}";

        Log(message);
        return true;
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[ItemVendorSellService] {message}", this);
    }
}