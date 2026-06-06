using UnityEngine;

public static class ItemChargeDisplayUtility
{
    public static bool TryGetChargeDisplay(
        ItemInstance item,
        out ItemChargeDisplayInfo info)
    {
        info = ItemChargeDisplayInfo.Hidden;

        if (item == null || item.Definition == null)
            return false;

        // 1. Direct charge item: battery, oxygen tank, fuel canister, magic sadness crystal, etc.
        if (item.HasCharges)
        {
            info = ItemChargeDisplayInfo.FromCharges(
                item.CurrentCharges,
                item.MaxCharges,
                GetDirectChargeLabel(item.Definition));

            return true;
        }

        // 2. Consuming tool/item: drill with battery, gun with ammo, welder with fuel.
        if (ItemConsumesContainedCharges(item.Definition))
        {
            ItemCategoryFlags sourceCategories = GetChargeSourceCategories(item.Definition);
            string label = GetChargeSourceLabel(sourceCategories);

            if (!TryFindContainedChargeSource(
                    item,
                    sourceCategories,
                    out ItemInstance chargeSource))
            {
                // Important behavior:
                // Drill has no battery -> show empty bar.
                info = ItemChargeDisplayInfo.Empty(label);
                return true;
            }

            info = ItemChargeDisplayInfo.FromCharges(
                chargeSource.CurrentCharges,
                chargeSource.MaxCharges,
                label);

            return true;
        }

        return false;
    }

    public static bool TryFindContainedChargeSource(
        ItemInstance containerItem,
        ItemCategoryFlags sourceCategories,
        out ItemInstance chargeSource)
    {
        chargeSource = null;

        if (containerItem == null ||
            !containerItem.IsContainer ||
            containerItem.ContainerState == null)
        {
            return false;
        }

        for (int i = 0; i < containerItem.ContainerState.SlotCount; i++)
        {
            InventorySlot slot = containerItem.ContainerState.GetSlot(i);
            ItemInstance candidate = slot?.Instance;

            if (candidate == null || candidate.Definition == null)
                continue;

            if (!candidate.HasCharges)
                continue;

            if (sourceCategories != ItemCategoryFlags.None &&
                (candidate.Definition.ItemCategories & sourceCategories) == 0)
            {
                continue;
            }

            chargeSource = candidate;
            return true;
        }

        return false;
    }

    private static bool ItemConsumesContainedCharges(ItemDefinition definition)
    {
        if (definition == null)
            return false;

        object value =
            TryReadMember(definition, "ConsumesContainedChargesWhileUsed") ??
            TryReadMember(definition, "consumesContainedChargesWhileUsed");

        return value is bool b && b;
    }

    private static ItemCategoryFlags GetChargeSourceCategories(ItemDefinition definition)
    {
        if (definition == null)
            return ItemCategoryFlags.None;

        object value =
            TryReadMember(definition, "ChargeSourceCategories") ??
            TryReadMember(definition, "chargeSourceCategories");

        if (value is ItemCategoryFlags flags)
            return flags;

        return ItemCategoryFlags.None;
    }

    private static string GetDirectChargeLabel(ItemDefinition definition)
    {
        if (definition == null)
            return "Charge";

        ItemCategoryFlags categories = definition.ItemCategories;

        if ((categories & ItemCategoryFlags.Utility) != 0)
            return "Battery";

        if ((categories & ItemCategoryFlags.Ammo) != 0)
            return "Ammo";

        if ((categories & ItemCategoryFlags.Fuel) != 0)
            return "Fuel";

        if ((categories & ItemCategoryFlags.Oxygen) != 0)
            return "Oxygen";

        return "Charge";
    }

    private static string GetChargeSourceLabel(ItemCategoryFlags categories)
    {
        if ((categories & ItemCategoryFlags.Utility) != 0)
            return "Battery";

        if ((categories & ItemCategoryFlags.Ammo) != 0)
            return "Ammo";

        if ((categories & ItemCategoryFlags.Fuel) != 0)
            return "Fuel";

        if ((categories & ItemCategoryFlags.Oxygen) != 0)
            return "Oxygen";

        return "Charge";
    }

    private static object TryReadMember(object obj, string memberName)
    {
        if (obj == null || string.IsNullOrWhiteSpace(memberName))
            return null;

        System.Type type = obj.GetType();

        System.Reflection.PropertyInfo prop = type.GetProperty(
            memberName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (prop != null)
            return prop.GetValue(obj);

        System.Reflection.FieldInfo field = type.GetField(
            memberName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (field != null)
            return field.GetValue(obj);

        return null;
    }
}