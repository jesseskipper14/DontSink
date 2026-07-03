using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemVendorPurchaseService : MonoBehaviour
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
            Debug.LogWarning("[ItemVendorPurchaseService] Missing ItemDefinitionCatalog reference.", this);
    }

    public int GetMoneyBalance()
    {
        return MoneyService.Balance;
    }

    public bool CanBuy(
        ItemVendorServiceDefinition vendor,
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
            reason = "Missing vendor.";
            return false;
        }

        if (offer == null)
        {
            reason = "Missing offer.";
            return false;
        }

        itemDef = offer.ResolveDefinition(itemCatalog);
        if (itemDef == null)
        {
            reason = $"Could not resolve item '{offer.itemId}'.";
            return false;
        }

        price = offer.ResolvePrice(itemDef);

        if (offer.stock == 0)
        {
            reason = "Out of stock.";
            return false;
        }

        if (!itemDef.CanBeSoldByItemVendors)
        {
            reason = "Item cannot be sold by item vendors.";
            return false;
        }

        if (!MoneyService.HasActiveChest)
        {
            reason = "No active money chest.";
            return false;
        }

        if (!MoneyService.CanSpend(price))
        {
            reason = $"Not enough money. Need ${price}, have ${MoneyService.Balance}.";
            return false;
        }

        ItemInstance previewInstance = ItemInstance.Create(itemDef, 1);
        if (previewInstance == null)
        {
            reason = "Failed to create item preview.";
            return false;
        }

        if (CanDeliver(previewInstance, context))
            return true;

        reason = "No place to put item, and item cannot be dropped.";
        return false;
    }

    public bool TryBuy(
        ItemVendorServiceDefinition vendor,
        ItemVendorSellOffer offer,
        AgentServiceContext context,
        out string message)
    {
        message = null;

        if (!CanBuy(vendor, offer, context, out ItemDefinition itemDef, out int price, out string reason))
        {
            message = reason;
            return false;
        }

        ItemInstance purchased = ItemInstance.Create(itemDef, 1);
        if (purchased == null)
        {
            message = "Failed to create item.";
            return false;
        }

        if (!MoneyService.TrySpend(price))
        {
            message = $"Could not spend ${price}.";
            return false;
        }

        if (TryDeliver(purchased, context, out string deliveryMessage))
        {
            message = $"Bought {itemDef.DisplayName} for ${price}. {deliveryMessage}";
            Log(message);
            return true;
        }

        MoneyService.AddMoney(price);

        message = $"Purchase failed after payment, so ${price} was refunded.";
        Debug.LogWarning($"[ItemVendorPurchaseService] {message}", this);
        return false;
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
            message = "Invalid item.";
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

        message = "Could not deliver item.";
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

        Debug.Log($"[ItemVendorPurchaseService] {message}", this);
    }
}