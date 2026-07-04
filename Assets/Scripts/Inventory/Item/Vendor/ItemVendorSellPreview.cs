using System.Collections.Generic;

public sealed class ItemVendorSellPreview
{
    public bool canSell;
    public string blockReason;

    public int itemValue;
    public int containedValue;
    public int totalValue;

    public bool hasContainedItems;
    public bool hasBlockedContainedItems;

    public readonly List<string> containedLines = new();

    public bool RequiresContainerConfirmation =>
        hasContainedItems && canSell;

    public string BuildContainedSummary()
    {
        if (!hasContainedItems || containedLines.Count == 0)
            return null;

        string text =
            $"Selling this container will also sell its contents. " +
            $"Container value: ${itemValue:n0}. Contents value: ${containedValue:n0}. Total: ${totalValue:n0}.";

        text += "\nContents:";

        for (int i = 0; i < containedLines.Count; i++)
            text += "\n- " + containedLines[i];

        return text;
    }
}