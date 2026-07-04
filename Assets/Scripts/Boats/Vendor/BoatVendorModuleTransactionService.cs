using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatVendorModuleTransactionService : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    [Header("Fallback Drop")]
    [SerializeField] private Vector3 fallbackDropOffset = new Vector3(0.75f, 0.25f, 0f);

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private void Awake()
    {
        if (itemCatalog == null)
            Debug.LogWarning("[BoatVendorModuleTransactionService] Missing ItemDefinitionCatalog reference.", this);
    }

    public bool CanBuyModule(
        BoatVendorPlaceholderServiceDefinition vendor,
        ItemVendorSellOffer offer,
        AgentServiceContext context,
        out ItemDefinition itemDef,
        out int price,
        out string reason)
    {
        itemDef = null;
        price = 0;
        reason = null;

        if (vendor == null)
        {
            reason = "Missing boat vendor.";
            return false;
        }

        if (offer == null)
        {
            reason = "Missing module offer.";
            return false;
        }

        itemDef = offer.ResolveDefinition(itemCatalog);
        if (itemDef == null)
        {
            reason = $"Could not resolve module '{offer.itemId}'.";
            return false;
        }

        if (!BoatVendorModuleOfferBuilder.IsBoatVendorModule(itemDef))
        {
            reason = "This module is not sold by boat vendors.";
            return false;
        }

        price = offer.ResolvePrice(itemDef);

        if (offer.stock == 0)
        {
            reason = "Out of stock.";
            return false;
        }

        if (!MoneyService.HasActiveChest)
        {
            reason = "No active money chest.";
            return false;
        }

        if (!MoneyService.CanSpend(price))
        {
            reason = $"Not enough money. Need ${price:n0}, have ${MoneyService.Balance:n0}.";
            return false;
        }

        ItemInstance previewInstance = ItemInstance.Create(itemDef, 1);
        if (previewInstance == null)
        {
            reason = "Failed to create module preview.";
            return false;
        }

        if (CanDeliver(previewInstance, context))
            return true;

        reason = "No place to put module, and it cannot be dropped.";
        return false;
    }

    public bool TryBuyModule(
        BoatVendorPlaceholderServiceDefinition vendor,
        ItemVendorSellOffer offer,
        AgentServiceContext context,
        out string message)
    {
        message = null;

        if (!CanBuyModule(vendor, offer, context, out ItemDefinition itemDef, out int price, out string reason))
        {
            message = reason;
            return false;
        }

        ItemInstance purchased = ItemInstance.Create(itemDef, 1);
        if (purchased == null)
        {
            message = "Failed to create module item.";
            return false;
        }

        if (!MoneyService.TrySpend(price))
        {
            message = $"Could not spend ${price:n0}.";
            return false;
        }

        if (TryDeliver(purchased, context, out string deliveryMessage))
        {
            message = $"Bought {itemDef.DisplayName} for ${price:n0}. {deliveryMessage}";
            Log(message);
            return true;
        }

        MoneyService.AddMoney(price);

        message = $"Module purchase failed after payment, so ${price:n0} was refunded.";
        Debug.LogWarning($"[BoatVendorModuleTransactionService] {message}", this);
        return false;
    }

    public BoatVendorModuleSellPreview BuildSellPreview(
        BoatVendorPlaceholderServiceDefinition vendor,
        ItemVendorSellSource source,
        int quantity)
    {
        BoatVendorModuleSellPreview preview = new BoatVendorModuleSellPreview();

        if (vendor == null)
        {
            preview.canSell = false;
            preview.blockReason = "Missing boat vendor.";
            return preview;
        }

        if (source == null)
        {
            preview.canSell = false;
            preview.blockReason = "Missing sell source.";
            return preview;
        }

        string sourceBlock = source.BlockReason;
        if (!string.IsNullOrWhiteSpace(sourceBlock))
        {
            preview.canSell = false;
            preview.blockReason = sourceBlock;
            return preview;
        }

        ItemInstance item = source.Item;
        if (item == null || item.Definition == null)
        {
            preview.canSell = false;
            preview.blockReason = "Missing module item.";
            return preview;
        }

        if (!BoatVendorModuleOfferBuilder.IsBoatVendorModule(item.Definition))
        {
            preview.canSell = false;
            preview.blockReason = "Boat vendor does not buy this module.";
            return preview;
        }

        if (!MoneyService.HasActiveChest)
        {
            preview.canSell = false;
            preview.blockReason = "No active money chest.";
            return preview;
        }

        quantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, source.MaxQuantity));

        int unitValue = GetModuleBuyFromPlayerPrice(vendor, item.Definition);
        preview.totalValue = Mathf.Max(0, unitValue * quantity);
        preview.canSell = true;

        return preview;
    }

    public bool TrySellModule(
        BoatVendorPlaceholderServiceDefinition vendor,
        ItemVendorSellSource source,
        int quantity,
        out string message)
    {
        message = null;

        BoatVendorModuleSellPreview preview = BuildSellPreview(vendor, source, quantity);
        if (preview == null || !preview.canSell)
        {
            message = preview != null ? preview.blockReason : "Missing sell preview.";
            return false;
        }

        quantity = Mathf.Clamp(quantity, 1, source.MaxQuantity);

        if (!source.TryRemove(quantity, out ItemInstance removedItem, out string removeMessage))
        {
            message = removeMessage;
            return false;
        }

        if (removedItem == null || removedItem.Definition == null)
        {
            message = "Removed module was invalid.";
            return false;
        }

        int payout = GetModuleBuyFromPlayerPrice(vendor, removedItem.Definition) *
                     Mathf.Max(1, removedItem.Quantity);

        payout = Mathf.Max(0, payout);
        MoneyService.AddMoney(payout);

        string label = !string.IsNullOrWhiteSpace(removedItem.Definition.DisplayName)
            ? removedItem.Definition.DisplayName
            : removedItem.Definition.ItemId;

        message = $"Sold {label} for ${payout:n0}.";

        if (!string.IsNullOrWhiteSpace(removeMessage))
            message += $" {removeMessage}";

        Log(message);
        return true;
    }

    private int GetModuleBuyFromPlayerPrice(
        BoatVendorPlaceholderServiceDefinition vendor,
        ItemDefinition def)
    {
        if (vendor == null || def == null)
            return 0;

        float multiplier =
            Mathf.Max(0f, def.DefaultVendorBuyMultiplier) *
            Mathf.Max(0f, vendor.ModuleBuyFromPlayerMultiplier);

        return Mathf.Max(0, Mathf.RoundToInt(def.BasePrice * multiplier));
    }

    private bool CanDeliver(ItemInstance instance, AgentServiceContext context)
    {
        if (instance == null || instance.Definition == null)
            return false;

        ItemAcquisitionResolver resolver = FindAcquisitionResolver(context);
        if (resolver != null && resolver.CanAcquire(instance))
            return true;

        return CanDrop(instance);
    }

    private bool TryDeliver(ItemInstance instance, AgentServiceContext context, out string message)
    {
        message = null;

        if (instance == null || instance.Definition == null)
        {
            message = "Invalid module item.";
            return false;
        }

        ItemAcquisitionResolver resolver = FindAcquisitionResolver(context);
        if (resolver != null && resolver.TryAcquire(instance))
        {
            message = "Added to loadout.";
            return true;
        }

        if (TryDropNearPlayer(instance, context, out WorldItem dropped))
        {
            message = dropped != null ? "Dropped near player." : "Dropped.";
            return true;
        }

        message = "Could not deliver module item.";
        return false;
    }

    private bool CanDrop(ItemInstance instance)
    {
        return instance != null &&
               instance.Definition != null &&
               instance.Definition.Droppable &&
               instance.Definition.WorldPrefab != null;
    }

    private bool TryDropNearPlayer(
        ItemInstance instance,
        AgentServiceContext context,
        out WorldItem dropped)
    {
        dropped = null;

        if (!CanDrop(instance))
            return false;

        Vector3 pos = ResolveDropPosition(context);
        GameObject actor = context.Actor != null ? context.Actor : gameObject;

        return WorldItemDropUtility.TryDrop(instance, pos, actor, out dropped);
    }

    private Vector3 ResolveDropPosition(AgentServiceContext context)
    {
        if (context.InteractContext.InteractorTransform != null)
            return context.InteractContext.InteractorTransform.position + fallbackDropOffset;

        return transform.position + fallbackDropOffset;
    }

    private ItemAcquisitionResolver FindAcquisitionResolver(AgentServiceContext context)
    {
        GameObject actor = context.Actor;

        if (actor != null)
        {
            ItemAcquisitionResolver fromParent =
                actor.GetComponentInParent<ItemAcquisitionResolver>();

            if (fromParent != null)
                return fromParent;

            ItemAcquisitionResolver fromChildren =
                actor.GetComponentInChildren<ItemAcquisitionResolver>(true);

            if (fromChildren != null)
                return fromChildren;
        }

        return FindFirstObjectByType<ItemAcquisitionResolver>();
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatVendorModuleTransactionService] {message}", this);
    }
}