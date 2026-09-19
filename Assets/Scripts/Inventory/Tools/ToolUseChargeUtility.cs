using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ToolUseChargeUtility
{
    private static readonly Dictionary<string, float> fractionalChargeByToolInstanceId = new();

    public static bool CanUseHeldTool(
        in InteractContext context,
        ToolCapabilityDefinition requiredCapability,
        out ItemInstance heldTool,
        out string failureReason)
    {
        heldTool = null;
        failureReason = string.Empty;

        if (requiredCapability == null)
            return true;

        PlayerEquipment equipment = FindEquipment(context);
        if (equipment == null)
        {
            failureReason = "No Equipment";
            return false;
        }

        // Current workaround:
        // toolbelt/backpack drag behavior is still not stable, so hands only.
        heldTool = equipment.Get(BottomBarSlotType.Hands);

        if (heldTool == null || heldTool.Definition == null)
        {
            failureReason = $"Requires {GetCapabilityName(requiredCapability)}";
            return false;
        }

        if (!heldTool.Definition.HasToolCapability(requiredCapability))
        {
            failureReason = $"Requires {GetCapabilityName(requiredCapability)}";
            return false;
        }

        if (!heldTool.Definition.ConsumesContainedChargesWhileUsed)
            return true;

        if (!TryFindContainedChargeSource(
                heldTool,
                heldTool.Definition.ChargeSourceCategories,
                out ItemInstance chargeSource))
        {
            failureReason = $"Requires {GetChargeSourceName(heldTool.Definition.ChargeSourceCategories)}";
            return false;
        }

        if (chargeSource.CurrentCharges < heldTool.Definition.MinimumChargeToUse)
        {
            failureReason = $"{GetChargeSourceName(heldTool.Definition.ChargeSourceCategories)} Empty";
            return false;
        }

        return true;
    }

    public static bool TryTickUseHeldTool(
        in InteractContext context,
        ToolCapabilityDefinition requiredCapability,
        float deltaTime,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (!CanUseHeldTool(context, requiredCapability, out ItemInstance heldTool, out failureReason))
            return false;

        if (heldTool == null || heldTool.Definition == null)
            return true;

        if (!heldTool.Definition.ConsumesContainedChargesWhileUsed)
            return true;

        float usePerSecond = heldTool.Definition.ChargeUsePerSecond;
        if (usePerSecond <= 0f)
            return true;

        if (!TryFindContainedChargeSource(
                heldTool,
                heldTool.Definition.ChargeSourceCategories,
                out ItemInstance chargeSource))
        {
            failureReason = $"Requires {GetChargeSourceName(heldTool.Definition.ChargeSourceCategories)}";
            return false;
        }

        if (chargeSource.CurrentCharges < heldTool.Definition.MinimumChargeToUse)
        {
            failureReason = $"{GetChargeSourceName(heldTool.Definition.ChargeSourceCategories)} Empty";
            return false;
        }

        string key = GetToolAccumulatorKey(heldTool);

        fractionalChargeByToolInstanceId.TryGetValue(key, out float accumulated);
        accumulated += Mathf.Max(0f, deltaTime) * usePerSecond;

        int wholeChargesToConsume = Mathf.FloorToInt(accumulated);

        if (wholeChargesToConsume <= 0)
        {
            fractionalChargeByToolInstanceId[key] = accumulated;
            return true;
        }

        int consumed = chargeSource.ConsumeChargesUpTo(wholeChargesToConsume);
        accumulated -= consumed;
        accumulated = Mathf.Max(0f, accumulated);

        fractionalChargeByToolInstanceId[key] = accumulated;

        NotifyToolContainerChanged(heldTool);
        NotifyInventoryChanged(context);

        if (consumed < wholeChargesToConsume)
        {
            failureReason = $"{GetChargeSourceName(heldTool.Definition.ChargeSourceCategories)} Empty";
            return false;
        }

        if (chargeSource.CurrentCharges < heldTool.Definition.MinimumChargeToUse)
        {
            failureReason = $"{GetChargeSourceName(heldTool.Definition.ChargeSourceCategories)} Empty";
            return false;
        }

        return true;
    }

    private static bool TryFindContainedChargeSource(
        ItemInstance tool,
        ItemCategoryFlags sourceCategories,
        out ItemInstance chargeSource)
    {
        chargeSource = null;

        if (tool == null || !tool.IsContainer || tool.ContainerState == null)
            return false;

        for (int i = 0; i < tool.ContainerState.SlotCount; i++)
        {
            InventorySlot slot = tool.ContainerState.GetSlot(i);
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

    private static PlayerEquipment FindEquipment(in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerEquipment equipment =
                context.InteractorGO.GetComponent<PlayerEquipment>() ??
                context.InteractorGO.GetComponentInParent<PlayerEquipment>() ??
                context.InteractorGO.GetComponentInChildren<PlayerEquipment>(true);

            if (equipment != null)
                return equipment;

            PlayerInventory inventory =
                context.InteractorGO.GetComponent<PlayerInventory>() ??
                context.InteractorGO.GetComponentInParent<PlayerInventory>() ??
                context.InteractorGO.GetComponentInChildren<PlayerInventory>(true);

            if (inventory != null)
                return inventory.Equipment;
        }

        if (context.InteractorTransform != null)
        {
            PlayerEquipment equipment =
                context.InteractorTransform.GetComponent<PlayerEquipment>() ??
                context.InteractorTransform.GetComponentInParent<PlayerEquipment>() ??
                context.InteractorTransform.GetComponentInChildren<PlayerEquipment>(true);

            if (equipment != null)
                return equipment;
        }

        return null;
    }

    private static PlayerInventory FindInventory(in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerInventory inventory =
                context.InteractorGO.GetComponent<PlayerInventory>() ??
                context.InteractorGO.GetComponentInParent<PlayerInventory>() ??
                context.InteractorGO.GetComponentInChildren<PlayerInventory>(true);

            if (inventory != null)
                return inventory;
        }

        if (context.InteractorTransform != null)
        {
            PlayerInventory inventory =
                context.InteractorTransform.GetComponent<PlayerInventory>() ??
                context.InteractorTransform.GetComponentInParent<PlayerInventory>() ??
                context.InteractorTransform.GetComponentInChildren<PlayerInventory>(true);

            if (inventory != null)
                return inventory;
        }

        return null;
    }

    private static void NotifyToolContainerChanged(ItemInstance heldTool)
    {
        if (heldTool?.ContainerState != null)
            heldTool.ContainerState.NotifyChanged();
    }

    private static void NotifyInventoryChanged(in InteractContext context)
    {
        PlayerInventory inventory = FindInventory(context);
        if (inventory != null)
            inventory.NotifyChanged();
    }

    private static string GetToolAccumulatorKey(ItemInstance heldTool)
    {
        if (heldTool == null)
            return "null_tool";

        if (!string.IsNullOrWhiteSpace(heldTool.InstanceId))
            return heldTool.InstanceId;

        return heldTool.GetHashCode().ToString();
    }

    private static string GetCapabilityName(ToolCapabilityDefinition capability)
    {
        if (capability == null)
            return "Tool";

        string display =
            TryReadStringPropertyOrField(capability, "displayName") ??
            TryReadStringPropertyOrField(capability, "DisplayName");

        if (!string.IsNullOrWhiteSpace(display))
            return display;

        return capability.name;
    }

    private static string GetChargeSourceName(ItemCategoryFlags categories)
    {
        if ((categories & ItemCategoryFlags.Ammo) != 0)
            return "Ammo";

        if ((categories & ItemCategoryFlags.Fuel) != 0)
            return "Fuel";

        if ((categories & ItemCategoryFlags.Utility) != 0)
            return "Battery";

        if ((categories & ItemCategoryFlags.Oxygen) != 0)
            return "Oxygen";

        return "Charge Source";
    }

    private static string TryReadStringPropertyOrField(object obj, string memberName)
    {
        if (obj == null || string.IsNullOrWhiteSpace(memberName))
            return null;

        System.Type type = obj.GetType();

        System.Reflection.PropertyInfo prop = type.GetProperty(
            memberName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (prop != null && prop.PropertyType == typeof(string))
            return prop.GetValue(obj) as string;

        System.Reflection.FieldInfo field = type.GetField(
            memberName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (field != null && field.FieldType == typeof(string))
            return field.GetValue(obj) as string;

        return null;
    }
}