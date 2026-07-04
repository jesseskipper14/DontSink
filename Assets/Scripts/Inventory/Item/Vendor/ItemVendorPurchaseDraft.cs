using System;

[Serializable]
public sealed class ItemVendorPurchaseDraft
{
    public int version = 1;
    public string vendorId;
    public int offerIndex;
    public string itemId;
    public int quantity = 1;
    public int unitPrice;
}