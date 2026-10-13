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

        if (context.Actor == null)
        {
            reason = "Missing purchase requester.";
            return false;
        }

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

        if (!TryResolveStock(vendor, offer, context, out var stockState, out string vendorKey, out string stockItemId, out reason))
            return false;

        if (stockState.GetRemaining(vendorKey, stockItemId, offer.stock) == 0)
        {
            reason = "Out of stock.";
            return false;
        }

        if (stockState.IsPurchasePending(vendorKey, stockItemId))
        {
            reason = "Purchase already in progress.";
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
        if (!GameplayAuthority.CanRun(GameplayAuthorityMode.SinglePlayerOrAuthoritative))
        {
            message = "Purchase requires gameplay authority.";
            return false;
        }
        if (!CanBuy(vendor, offer, context, out ItemDefinition itemDef, out int price, out message))
            return false;
        if (!TryResolveStock(vendor, offer, context, out var state, out string vendorKey, out string itemId, out message))
            return false;
        if (!state.TryReserve(vendorKey, itemId, offer.stock, out var stock))
        {
            message = "Out of stock or purchase already in progress.";
            return false;
        }
        using (stock)
        {
            var purchased = ItemInstance.Create(itemDef, 1);
            if (purchased == null)
            {
                message = "Failed to create item.";
                return false;
            }
            var treasury = MoneyChestTreasuryService.Instance;
            if (treasury == null)
            {
                message = "No active money chest.";
                return false;
            }
            if (!treasury.TryPrepareTradeBalance(-price, out var money, out message)) return false;
            using (money)
            {
                if (!money.Validate(out message)) return false;
                // Reuse the silent/reversible chest writes: delivery failure restores the
                // exact chest and reserved stock without a separate, fallible refund.
                money.Apply();
                if (!TryDeliver(purchased, context, out string deliveryMessage))
                {
                    message = "Could not deliver item. Payment and stock were restored.";
                    return false;
                }
                stock.Complete();
                money.Complete();
                message = $"Bought {itemDef.DisplayName} for ${price}. {deliveryMessage}";
                try { money.Publish(); } catch (System.Exception ex) { Debug.LogException(ex); }
                Log(message);
                try { GameMessageService.PostInfo($"Purchased {itemDef.DisplayName} for ${price:n0}."); }
                catch (System.Exception ex) { Debug.LogException(ex); }
                return true;
            }
        }
    }

    public int GetRuntimeStock(ItemVendorServiceDefinition vendor, ItemVendorSellOffer offer, AgentServiceContext context)
    {
        return TryResolveStock(vendor, offer, context, out var state, out string vendorKey, out string itemId, out _)
            ? state.GetRemaining(vendorKey, itemId, offer.stock) : 0;
    }

    private bool TryResolveStock(ItemVendorServiceDefinition vendor, ItemVendorSellOffer offer,
        AgentServiceContext context, out ItemVendorStockState state, out string vendorKey,
        out string itemId, out string reason)
    {
        state = GameState.I != null ? GameState.I.itemVendorStock : null;
        vendorKey = null;
        itemId = offer?.ResolveDefinition(itemCatalog)?.ItemId;
        reason = null;
        string agentId = context.Agent != null ? context.Agent.StableId : null;
        if (state == null || vendor == null || string.IsNullOrWhiteSpace(vendor.ServiceId) ||
            string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(itemId))
        {
            reason = "Missing vendor stock state or stable vendor/item identity.";
            return false;
        }
        string nodeId = !string.IsNullOrWhiteSpace(context.Agent.NodeId)
            ? context.Agent.NodeId : GameState.I.player?.currentNodeId;
        vendorKey = ItemVendorStockState.BuildVendorKey(nodeId, agentId, vendor.ServiceId);
        return true;
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

        if (context.Actor == null || !CanDrop(instance))
            return false;

        Vector3 pos = ResolveDropPosition(context);
        GameObject actor = context.Actor;

        return WorldItemDropUtility.TryDrop(instance, pos, actor, out dropped);
    }

    private Vector3 ResolveDropPosition(AgentServiceContext context)
    {
        return context.Actor.transform.position + fallbackDropOffset;
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

        return null;
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[ItemVendorPurchaseService] {message}", this);
    }
}
