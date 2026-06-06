using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class UnderwaterResourceInteractable :
    MonoBehaviour,
    IInteractable,
    IPickupInteractable,
    IMouseHarvestInteractable,
    IHoldPickupTickReceiver,
    IInteractPromptProvider,
    IPickupPromptProvider,
    IInteractPromptActionProvider,
    IInteractionLabelProvider,
    IInteractionRangeProvider
{
    [Header("Runtime")]
    [SerializeField] private UnderwaterResourceDefinition definition;
    [SerializeField] private UnderwaterResourceRuntimeInstance runtimeInstance;

    [Header("Prompt")]
    [SerializeField] private Transform promptAnchor;

    [SerializeField, Min(0f)]
    private float hoverNameRange = 5f;

    [SerializeField, Min(0f)]
    private float actionRange = 2f;

    [Header("Priority")]
    [SerializeField] private int interactionPriority = 60;
    [SerializeField] private int pickupPriority = 70;

    [Header("Debug")]
    [SerializeField] private bool debugHarvest = true;

    public int InteractionPriority => interactionPriority;
    public int PickupPriority => pickupPriority;

    public PickupInteractionMode PickupMode
    {
        get
        {
            if (definition != null &&
                definition.category == UnderwaterResourceCategory.Extractable)
            {
                return PickupInteractionMode.Hold;
            }

            return PickupInteractionMode.Instant;
        }
    }

    public float PickupHoldDuration
    {
        get
        {
            if (definition == null)
                return 0f;

            if (definition.category != UnderwaterResourceCategory.Extractable)
                return 0f;

            return Mathf.Max(0.05f, definition.harvestSeconds);
        }
    }

    public UnderwaterResourceDefinition Definition => definition;
    public UnderwaterResourceRuntimeInstance RuntimeInstance => runtimeInstance;

    public void Initialize(
        UnderwaterResourceDefinition newDefinition,
        UnderwaterResourceRuntimeInstance newRuntimeInstance)
    {
        definition = newDefinition;
        runtimeInstance = newRuntimeInstance;

        gameObject.name = definition != null
            ? $"UnderwaterResource_{definition.displayName}"
            : "UnderwaterResource_MissingDefinition";

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        promptAnchor = transform;
    }

    // ---------------------------------------------------------------------
    // IInteractable
    //
    // This is only for blocked/status messages.
    // Actual harvesting uses IPickupInteractable so Interactor2D owns hold timing.
    // ---------------------------------------------------------------------

    public bool CanInteract(in InteractContext context)
    {
        if (!IsValidResource())
            return false;

        if (!IsInRange(context))
            return false;

        if (runtimeInstance.depleted || runtimeInstance.remainingCharges <= 0)
            return true;

        if (definition.category == UnderwaterResourceCategory.Craneable)
            return true;

        if (definition.RequiresTool() && !HasRequiredTool(context))
            return true;

        return false;
    }

    public void Interact(in InteractContext context)
    {
        if (!CanInteract(context))
            return;

        string reason = GetBlockedReason(context);
        if (!string.IsNullOrWhiteSpace(reason))
            Debug.Log($"[UnderwaterResourceInteractable] {reason}", this);
    }

    // ---------------------------------------------------------------------
    // IPickupInteractable
    // ---------------------------------------------------------------------

    public bool CanPickup(in InteractContext context)
    {
        if (!IsValidResource())
            return false;

        if (!IsInRange(context))
            return false;

        if (runtimeInstance.depleted || runtimeInstance.remainingCharges <= 0)
            return false;

        if (definition.category == UnderwaterResourceCategory.Craneable)
            return false;

        if (definition.RequiresTool() && !HasRequiredTool(context))
            return false;

        return true;
    }

    public void Pickup(in InteractContext context)
    {
        if (!CanPickup(context))
            return;

        CompleteHarvest(context);
    }

    // ---------------------------------------------------------------------
    // Prompt providers
    // ---------------------------------------------------------------------

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (definition == null)
            return "Inspect";

        if (definition.category == UnderwaterResourceCategory.Craneable)
            return "Inspect";

        return "Inspect";
    }

    public string GetPickupPromptVerb(in InteractContext context)
    {
        if (definition == null)
            return "Harvest";

        return definition.category switch
        {
            UnderwaterResourceCategory.Collectable => "Collect",
            UnderwaterResourceCategory.Extractable => "Extract",
            UnderwaterResourceCategory.Craneable => "Recover",
            _ => "Harvest"
        };
    }

    public string GetInteractionLabel(in InteractContext context)
    {
        if (definition == null)
            return CleanObjectName(gameObject.name);

        if (!string.IsNullOrWhiteSpace(definition.displayName))
            return definition.displayName;

        return CleanObjectName(gameObject.name);
    }

    public void GetPromptActions(in InteractContext context, List<PromptAction> actions)
    {
        if (actions == null)
            return;

        string blockedReason = GetBlockedReason(context);

        if (!string.IsNullOrWhiteSpace(blockedReason))
            actions.Add(new PromptAction(blockedReason, priority: 120));
    }

    public bool TryGetHoverNameRange(out float range)
    {
        range = hoverNameRange;
        return true;
    }

    public bool TryGetActionRange(out float range)
    {
        range = actionRange;
        return true;
    }

    // ---------------------------------------------------------------------
    // Harvest payout
    // ---------------------------------------------------------------------

    private void CompleteHarvest(in InteractContext context)
    {
        if (!IsValidResource())
            return;

        PlayerInventory inventory = FindInventory(context);

        if (inventory == null)
        {
            Debug.LogWarning(
                $"[UnderwaterResourceInteractable] Could not find PlayerInventory for {GetResourceName()}.",
                this);

            return;
        }

        bool awardedAnything = false;

        int seed = StableHash(runtimeInstance.instanceId);
        System.Random rng = new(seed);

        for (int i = 0; i < definition.yields.Count; i++)
        {
            UnderwaterResourceYield yield = definition.yields[i];

            if (yield == null || yield.itemDefinition == null)
                continue;

            int quantity = yield.RollQuantity(rng);
            if (quantity <= 0)
                continue;

            ItemInstance incoming = ItemInstance.Create(yield.itemDefinition, quantity);

            if (TryGiveToInventoryOrDropRemainder(inventory, incoming))
            {
                awardedAnything = true;

                if (debugHarvest)
                {
                    Debug.Log(
                        $"[UnderwaterResourceInteractable] Harvested {quantity}x {yield.itemDefinition.name} from {GetResourceName()}.",
                        this);
                }
            }
        }

        if (!awardedAnything)
            return;

        runtimeInstance.remainingCharges--;

        if (runtimeInstance.remainingCharges <= 0)
        {
            runtimeInstance.remainingCharges = 0;
            runtimeInstance.depleted = true;
            gameObject.SetActive(false);
        }
    }

    private bool TryGiveToInventoryOrDropRemainder(
        PlayerInventory inventory,
        ItemInstance incoming)
    {
        if (inventory == null ||
            incoming == null ||
            incoming.Definition == null ||
            incoming.Quantity <= 0)
        {
            return false;
        }

        if (inventory.TryAutoInsert(incoming, out ItemInstance remainder))
        {
            if (remainder == null || remainder.IsDepleted())
                return true;

            if (inventory.TryDropInstance(remainder, GetDropPosition()))
                return true;

            Debug.LogWarning(
                $"[UnderwaterResourceInteractable] Inventory accepted part of {incoming.Definition.name}, but remainder could not be dropped.",
                this);

            return true;
        }

        if (inventory.TryAddInstance(incoming))
            return true;

        if (inventory.TryDropInstance(incoming, GetDropPosition()))
            return true;

        Debug.LogWarning(
            $"[UnderwaterResourceInteractable] Could not add or drop harvested item {incoming.Definition.name}.",
            this);

        return false;
    }

    private Vector3 GetDropPosition()
    {
        return transform.position + Vector3.up * 0.35f;
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

    // ---------------------------------------------------------------------
    // Tool checks
    // ---------------------------------------------------------------------

    private bool HasRequiredTool(in InteractContext context)
    {
        if (definition == null)
            return false;

        if (definition.requiredToolCapability != null)
        {
            return ToolUseChargeUtility.CanUseHeldTool(
                context,
                definition.requiredToolCapability,
                out _,
                out _);
        }

        if (definition.requiredTool == UnderwaterResourceToolKind.None)
            return true;

        if (definition.requiredTool == UnderwaterResourceToolKind.SimpleDrill)
            return HasLegacySimpleDrill(context);

        if (definition.requiredTool == UnderwaterResourceToolKind.Crane)
            return false;

        return false;
    }

    public bool TickHoldPickup(in InteractContext context, float deltaTime)
    {
        if (definition == null)
            return false;

        if (definition.category != UnderwaterResourceCategory.Extractable)
            return true;

        if (definition.requiredToolCapability == null)
            return true;

        return ToolUseChargeUtility.TryTickUseHeldTool(
            context,
            definition.requiredToolCapability,
            deltaTime,
            out _);
    }

    private bool HasEquippedToolCapability(
        in InteractContext context,
        ToolCapabilityDefinition requiredCapability)
    {
        if (requiredCapability == null)
            return true;

        PlayerEquipment equipment = FindEquipment(context);
        if (equipment == null)
            return false;

        // Current v1 workaround:
        // backpack/toolbelt insertion is not stable yet, so ONLY hands count.
        ItemInstance handsItem = equipment.Get(BottomBarSlotType.Hands);

        return ItemHasCapability(handsItem, requiredCapability);
    }

    private bool HasLegacySimpleDrill(in InteractContext context)
    {
        PlayerEquipment equipment = FindEquipment(context);
        if (equipment == null)
            return false;

        ItemInstance handsItem = equipment.Get(BottomBarSlotType.Hands);
        if (handsItem == null || handsItem.Definition == null)
            return false;

        string objectName = handsItem.Definition.name;
        if (!string.IsNullOrWhiteSpace(objectName) &&
            objectName.IndexOf("drill", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        string displayName = TryReadStringPropertyOrField(handsItem.Definition, "DisplayName");
        if (!string.IsNullOrWhiteSpace(displayName) &&
            displayName.IndexOf("drill", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        string stableId =
            TryReadStringPropertyOrField(handsItem.Definition, "StableId") ??
            TryReadStringPropertyOrField(handsItem.Definition, "stableId");

        return string.Equals(stableId, "simple_drill", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stableId, "drill", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool ItemHasCapability(
        ItemInstance item,
        ToolCapabilityDefinition requiredCapability)
    {
        if (item == null || item.Definition == null || requiredCapability == null)
            return false;

        // Best case: your ToolCapabilityDefinition already has:
        // bool Matches(ItemDefinition itemDefinition)
        if (TryInvokeCapabilityMatches(requiredCapability, item.Definition, out bool matchResult))
            return matchResult;

        // Also support:
        // item.Definition.HasToolCapability(ToolCapabilityDefinition capability)
        if (TryInvokeItemHasToolCapability(item.Definition, requiredCapability, out matchResult))
            return matchResult;

        // Also support:
        // item.Definition.ToolCapabilities / toolCapabilities containing this capability.
        if (ItemDefinitionHasCapabilityListEntry(item.Definition, requiredCapability))
            return true;

        // Also support:
        // requiredCapability.compatibleItemDefinitions / CompatibleItemDefinitions containing this item definition.
        if (CapabilityHasCompatibleItemDefinition(requiredCapability, item.Definition))
            return true;

        return false;
    }

    private static bool TryInvokeCapabilityMatches(
        ToolCapabilityDefinition capability,
        ItemDefinition itemDefinition,
        out bool result)
    {
        result = false;

        if (capability == null || itemDefinition == null)
            return false;

        System.Reflection.MethodInfo method = capability.GetType().GetMethod(
            "Matches",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(ItemDefinition) },
            modifiers: null);

        if (method == null || method.ReturnType != typeof(bool))
            return false;

        result = (bool)method.Invoke(capability, new object[] { itemDefinition });
        return true;
    }

    private static bool TryInvokeItemHasToolCapability(
        ItemDefinition itemDefinition,
        ToolCapabilityDefinition capability,
        out bool result)
    {
        result = false;

        if (itemDefinition == null || capability == null)
            return false;

        System.Reflection.MethodInfo method = itemDefinition.GetType().GetMethod(
            "HasToolCapability",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(ToolCapabilityDefinition) },
            modifiers: null);

        if (method == null || method.ReturnType != typeof(bool))
            return false;

        result = (bool)method.Invoke(itemDefinition, new object[] { capability });
        return true;
    }

    private static bool ItemDefinitionHasCapabilityListEntry(
        ItemDefinition itemDefinition,
        ToolCapabilityDefinition requiredCapability)
    {
        if (itemDefinition == null || requiredCapability == null)
            return false;

        object list =
            TryReadMember(itemDefinition, "ToolCapabilities") ??
            TryReadMember(itemDefinition, "toolCapabilities");

        return EnumerableContainsCapability(list, requiredCapability);
    }

    private static bool CapabilityHasCompatibleItemDefinition(
        ToolCapabilityDefinition capability,
        ItemDefinition itemDefinition)
    {
        if (capability == null || itemDefinition == null)
            return false;

        object list =
            TryReadMember(capability, "compatibleItemDefinitions") ??
            TryReadMember(capability, "CompatibleItemDefinitions");

        if (list is IEnumerable enumerable)
        {
            foreach (object entry in enumerable)
            {
                if (ReferenceEquals(entry, itemDefinition))
                    return true;
            }
        }

        return false;
    }

    private static bool EnumerableContainsCapability(
        object list,
        ToolCapabilityDefinition requiredCapability)
    {
        if (list == null || requiredCapability == null)
            return false;

        if (list is not IEnumerable enumerable)
            return false;

        foreach (object entry in enumerable)
        {
            if (ReferenceEquals(entry, requiredCapability))
                return true;

            string entryStableId =
                TryReadStringPropertyOrField(entry, "stableId") ??
                TryReadStringPropertyOrField(entry, "StableId");

            string requiredStableId =
                TryReadStringPropertyOrField(requiredCapability, "stableId") ??
                TryReadStringPropertyOrField(requiredCapability, "StableId");

            if (!string.IsNullOrWhiteSpace(entryStableId) &&
                string.Equals(entryStableId, requiredStableId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
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

    private static string TryReadStringPropertyOrField(object obj, string memberName)
    {
        object value = TryReadMember(obj, memberName);
        return value as string;
    }

    // ---------------------------------------------------------------------
    // State helpers
    // ---------------------------------------------------------------------

    private bool IsValidResource()
    {
        return definition != null && runtimeInstance != null;
    }

    private bool IsInRange(in InteractContext context)
    {
        return Vector2.Distance(context.Origin, transform.position) <= actionRange;
    }

    private string GetBlockedReason(in InteractContext context)
    {
        if (definition == null)
            return "Missing Resource Definition";

        if (runtimeInstance == null)
            return "Missing Resource Runtime";

        if (runtimeInstance.depleted || runtimeInstance.remainingCharges <= 0)
            return "Depleted";

        if (definition.category == UnderwaterResourceCategory.Craneable)
            return "Requires Crane";

        if (definition.requiredToolCapability != null)
        {
            bool canUse = ToolUseChargeUtility.CanUseHeldTool(
                context,
                definition.requiredToolCapability,
                out _,
                out string failureReason);

            if (!canUse)
                return string.IsNullOrWhiteSpace(failureReason)
                    ? $"Requires {definition.GetRequiredToolDisplayName()}"
                    : failureReason;
        }
        else if (definition.RequiresTool() && !HasRequiredTool(context))
        {
            return $"Requires {definition.GetRequiredToolDisplayName()}";
        }

        return string.Empty;
    }

    private string GetResourceName()
    {
        if (definition == null)
            return CleanObjectName(gameObject.name);

        if (!string.IsNullOrWhiteSpace(definition.displayName))
            return definition.displayName;

        return CleanObjectName(gameObject.name);
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;

            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
            }

            return hash;
        }
    }

    private static string CleanObjectName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return raw
            .Replace("(Clone)", "")
            .Replace("_", " ")
            .Trim();
    }
}