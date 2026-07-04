using System;

public enum ItemVendorSellArea
{
    Player = 0,
    Boat = 1
}

[Serializable]
public sealed class ItemVendorSellDraft
{
    public int version = 1;

    public string vendorId;
    public ItemVendorSellArea area;
    public int sourceIndex = -1;

    public string sourceKey;
    public string expectedInstanceId;

    public int quantity = 1;
}