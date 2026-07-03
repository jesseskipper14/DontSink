using UnityEngine;

public static class ItemVendorAvailabilityUtility
{
    public static bool CanVendorSellItem(
        ItemDefinition item,
        ItemVendorAvailabilityContext context,
        out string reason)
    {
        reason = null;

        if (item == null)
        {
            reason = "Missing item.";
            return false;
        }

        if (!item.CanBeSoldByItemVendors)
        {
            reason = "Item cannot be sold by item vendors.";
            return false;
        }

        ItemVendorAvailabilityRule rule = item.VendorAvailability;
        if (rule == null || !rule.HasHardRestrictions)
            return true;

        if (!context.HasNodeContext)
        {
            reason = "Item has node restrictions, but no node context was provided.";
            return false;
        }

        if (IsBlockedArchetype(rule, context.NodeArchetypeId))
        {
            reason = $"Blocked at node archetype '{context.NodeArchetypeId}'.";
            return false;
        }

        if (!IsAllowedArchetype(rule, context.NodeArchetypeId))
        {
            reason = $"Not allowed at node archetype '{context.NodeArchetypeId}'.";
            return false;
        }

        if (rule.minProsperity > 0 && context.Prosperity < rule.minProsperity)
        {
            reason = $"Requires prosperity {rule.minProsperity}.";
            return false;
        }

        if (rule.minTradeRating > 0 && context.TradeRating < rule.minTradeRating)
        {
            reason = $"Requires trade rating {rule.minTradeRating}.";
            return false;
        }

        if (rule.minSecurity > 0 && context.Security < rule.minSecurity)
        {
            reason = $"Requires security {rule.minSecurity}.";
            return false;
        }

        if (rule.minStability > 0 && context.Stability < rule.minStability)
        {
            reason = $"Requires stability {rule.minStability}.";
            return false;
        }

        return true;
    }

    public static bool CanVendorBuyItem(
        ItemDefinition item,
        out string reason)
    {
        reason = null;

        if (item == null)
        {
            reason = "Missing item.";
            return false;
        }

        if (!item.CanBeBoughtByItemVendors)
        {
            reason = "Item cannot be bought by item vendors.";
            return false;
        }

        return true;
    }

    private static bool IsAllowedArchetype(ItemVendorAvailabilityRule rule, string nodeArchetypeId)
    {
        NodeArchetypeDef[] allowed = rule.allowedNodeArchetypes;

        if (allowed == null || allowed.Length == 0)
            return true;

        for (int i = 0; i < allowed.Length; i++)
        {
            NodeArchetypeDef def = allowed[i];
            if (def == null)
                continue;

            if (def.archetypeId == nodeArchetypeId)
                return true;
        }

        return false;
    }

    private static bool IsBlockedArchetype(ItemVendorAvailabilityRule rule, string nodeArchetypeId)
    {
        NodeArchetypeDef[] blocked = rule.blockedNodeArchetypes;

        if (blocked == null || blocked.Length == 0)
            return false;

        for (int i = 0; i < blocked.Length; i++)
        {
            NodeArchetypeDef def = blocked[i];
            if (def == null)
                continue;

            if (def.archetypeId == nodeArchetypeId)
                return true;
        }

        return false;
    }
}