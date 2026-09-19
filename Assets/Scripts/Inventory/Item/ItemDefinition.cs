using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Survival.Buffs;

[System.Flags]
public enum ItemCategoryFlags
{
    None = 0,
    General = 1 << 0,
    Tool = 1 << 1,
    Weapon = 1 << 2,
    Ammo = 1 << 3,
    Armor = 1 << 4,
    Consumable = 1 << 5,
    Resource = 1 << 6,
    Oxygen = 1 << 7,
    Fuel = 1 << 8,
    Medical = 1 << 9,
    Utility = 1 << 10,
    Module = 1 << 11,
    Cargo = 1 << 12,
    Sacred = 1 << 13,
    Ballast = 1 << 14
}

public enum PickupInteractionMode
{
    Instant = 0,
    Hold = 1
}

public enum PreferredDisplacedDestination
{
    None = 0,
    MatchingEquipSlot,
    AnyHotbar
}

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Game/Inventory/Item Definition")]
public sealed class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Classification")]
    [SerializeField] private ItemCategoryFlags itemCategories = ItemCategoryFlags.General;

    [Header("Tool Capabilities")]
    [SerializeField] private List<ToolCapabilityDefinition> toolCapabilities = new();

    [Header("Tool Use Consumption")]
    [SerializeField] private bool consumesContainedChargesWhileUsed;

    [Tooltip("The contained item category used as the charge source. Drill = Utility battery, gun = Ammo, welder = Fuel, etc.")]
    [SerializeField] private ItemCategoryFlags chargeSourceCategories = ItemCategoryFlags.Utility;

    [Tooltip("How many charge units this tool consumes per second while actively used.")]
    [Min(0f)]
    [SerializeField] private float chargeUsePerSecond = 1f;

    [Tooltip("Minimum contained charge required before the tool is allowed to start/continue use.")]
    [Min(0)]
    [SerializeField] private int minimumChargeToUse = 1;

    public bool ConsumesContainedChargesWhileUsed => consumesContainedChargesWhileUsed;
    public ItemCategoryFlags ChargeSourceCategories => chargeSourceCategories;
    public float ChargeUsePerSecond => Mathf.Max(0f, chargeUsePerSecond);
    public int MinimumChargeToUse => Mathf.Max(0, minimumChargeToUse);

    public IReadOnlyList<ToolCapabilityDefinition> ToolCapabilities => toolCapabilities;

    public bool HasToolCapability(ToolCapabilityDefinition capability)
    {
        if (capability == null || toolCapabilities == null)
            return false;

        for (int i = 0; i < toolCapabilities.Count; i++)
        {
            if (toolCapabilities[i] == capability)
                return true;
        }

        return false;
    }

    [Header("Physical")]
    [Tooltip("Canonical mass of ONE unit of this item. This is the source of truth for portable-item mass in inventory, containers, and spawned world objects.")]
    [SerializeField, Min(0f)] private float unitMass = 1f;

    [Tooltip("Additional displacement volume contributed by ONE unit while the item is physically exposed on the player (held or equipped). Items merely stored in inventory/containers do not contribute this volume.")]
    [SerializeField, Min(0f)] private float unitExposedVolumeContribution = 0f;

    [Header("Stacking")]
    [Min(1)]
    [SerializeField] private int maxStack = 1;

    [Header("Charges / Units")]
    [SerializeField] private bool hasCharges;
    [Min(1)]
    [SerializeField] private int maxCharges = 1;

    [Header("Rules")]
    [SerializeField] private bool stowableInInventory = true;
    [SerializeField] private bool droppable = true;
    [SerializeField] private bool tradable = true;

    [Header("Economy")]
    [SerializeField, Min(0)] private int basePrice = 10;

    [SerializeField] private bool canBeSoldByItemVendors = true;
    [SerializeField] private bool canBeBoughtByItemVendors = true;

    [SerializeField, Range(0f, 2f)]
    private float defaultVendorBuyMultiplier = 0.25f;

    [SerializeField] private ItemVendorAvailabilityRule vendorAvailability = new ItemVendorAvailabilityRule();

    [Header("Parent Slot Rules")]
    [SerializeField] private BottomBarSlotType[] disallowedParentSlots;

    [Header("Fallback Placement")]
    [SerializeField] private PreferredDisplacedDestination preferredDisplacedDestination = PreferredDisplacedDestination.None;

    [Header("Equip")]
    [SerializeField] private BottomBarSlotType equipSlot = BottomBarSlotType.None;

    [Tooltip("Extra equipment slots this item occupies while equipped. If empty, the item only occupies its Equip Slot.")]
    [SerializeField] private BottomBarSlotType[] occupiedEquipSlots;

    [Header("Wearable Visual")]
    [Tooltip("Temporary v1 wearable overlay sprite. Later this can become a segmented visual definition.")]
    [SerializeField] private Sprite wearableVisualSprite;

    [Header("Equipped Buffs")]
    [SerializeField] private PlayerBuffDefinition[] equippedBuffs;

    [Header("External Air Source")]
    [SerializeField] private bool providesExternalAir;

    [Tooltip("Air units supplied per second while underwater and this item has charges.")]
    [Min(0f)]
    [SerializeField] private float externalAirSupplyPerSecond = 10f;

    [Tooltip("Item charge units consumed per second while this air source is active.")]
    [Min(0f)]
    [SerializeField] private float externalAirChargeUsePerSecond = 1f;

    [Tooltip("Optional extra max air capacity while equipped.")]
    [Min(0f)]
    [SerializeField] private float externalAirMaxAirBonus = 0f;

    [Header("Portable Container (this item itself stores other items)")]
    [Tooltip("Enable only when THIS portable item is itself a container, such as a backpack, toolbox, or crate.")]
    [FormerlySerializedAs("isContainer")]
    [SerializeField] private bool isPortableContainer;

    [Tooltip("Number of storage slots inside this portable item.")]
    [FormerlySerializedAs("containerSlotCount")]
    [Min(0)]
    [SerializeField] private int portableContainerSlotCount = 0;

    [Tooltip("Number of columns used when displaying this portable item's contents.")]
    [FormerlySerializedAs("containerColumnCount")]
    [Min(1)]
    [SerializeField] private int portableContainerColumnCount = 4;

    [Tooltip(
        "Maximum quantity allowed in EACH slot of this portable container. " +
        "0 = use the incoming item's normal Max Stack. " +
        "Set to 1 for one-item-per-slot containers such as a handheld sounding line.")]
    [Min(0)]
    [SerializeField] private int portableContainerMaxQuantityPerSlot = 0;

    [Header("Portable Container - Accepted Items")]
    [Tooltip("If enabled, this portable container accepts any item that passes its normal nesting rules.")]
    [SerializeField] private bool portableContainerAcceptsAnyItem = false;

    [Tooltip("Items matching ANY of these categories are accepted. Leave None if categories should not grant acceptance.")]
    [FormerlySerializedAs("allowedContainerCategories")]
    [SerializeField] private ItemCategoryFlags portableContainerAllowedCategories = ItemCategoryFlags.None;

    [Tooltip("Specific ItemDefinitions that are accepted even if their category is not allowed. Useful for special-purpose containers.")]
    [SerializeField] private List<ItemDefinition> explicitlyAllowedPortableContainerItems = new();

    [Tooltip("Prevents equal-or-higher-tier containers from being nested inside this portable container.")]
    [FormerlySerializedAs("containerTier")]
    [SerializeField] private int portableContainerTier = 0;

    [Header("Pickup")]
    [SerializeField] private PickupInteractionMode pickupMode = PickupInteractionMode.Instant;
    [SerializeField] private float pickupHoldDuration = 0.4f;

    public PickupInteractionMode PickupMode => pickupMode;
    public float PickupHoldDuration => Mathf.Max(0.05f, pickupHoldDuration);

    [Header("Module")]
    [SerializeField] private bool isModule;
    [SerializeField] private ModuleDefinition moduleDefinition;

    [Header("Boat Vendor")]
    [Tooltip("If true, this module item can appear in boat vendor module stock.")]
    [SerializeField] private bool soldByBoatVendors;

    public bool IsModule => isModule;
    public ModuleDefinition ModuleDefinition => moduleDefinition;
    public bool SoldByBoatVendors => soldByBoatVendors;

    [Header("World")]
    [SerializeField] private WorldItem worldPrefab;

    [Tooltip(
        "Controls whether a physical WorldItem survives scene/save transitions when it is not in inventory. " +
        "BoatOnly is the safe default for normal items. PersistentWorld is for recoverable important objects " +
        "such as anchors, diving bells, and expensive salvage hardware.")]
    [SerializeField]
    private WorldItemPersistencePolicy worldPersistence =
        WorldItemPersistencePolicy.BoatOnly;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public ItemCategoryFlags ItemCategories => itemCategories;
    public bool IsSacred => (itemCategories & ItemCategoryFlags.Sacred) != 0;
    public float UnitMass => Mathf.Max(0f, unitMass);
    public float UnitExposedVolumeContribution => Mathf.Max(0f, unitExposedVolumeContribution);
    public int MaxStack => Mathf.Max(1, maxStack);

    public bool HasCharges => hasCharges;
    public int MaxCharges => hasCharges ? Mathf.Max(1, maxCharges) : 0;

    public bool StowableInInventory => stowableInInventory;
    public bool Droppable => droppable;
    public bool Tradable => tradable;

    public int BasePrice => Mathf.Max(0, basePrice);

    public bool CanBeSoldByItemVendors =>
        canBeSoldByItemVendors &&
        tradable &&
        !IsSacred;

    public bool CanBeBoughtByItemVendors =>
        canBeBoughtByItemVendors &&
        tradable &&
        !IsSacred;

    public float DefaultVendorBuyMultiplier =>
        Mathf.Clamp(defaultVendorBuyMultiplier, 0f, 2f);

    public ItemVendorAvailabilityRule VendorAvailability => vendorAvailability;
    public bool IsInstallableModule => isModule && moduleDefinition != null;
    public BottomBarSlotType EquipSlot => equipSlot;
    public bool IsEquippable => equipSlot != BottomBarSlotType.None;

    public IReadOnlyList<BottomBarSlotType> OccupiedEquipSlots => occupiedEquipSlots;
    public Sprite WearableVisualSprite => wearableVisualSprite;
    public bool HasWearableVisual => wearableVisualSprite != null;
    public IReadOnlyList<PlayerBuffDefinition> EquippedBuffs => equippedBuffs;

    public bool ProvidesExternalAir => providesExternalAir;
    public float ExternalAirSupplyPerSecond => Mathf.Max(0f, externalAirSupplyPerSecond);
    public float ExternalAirChargeUsePerSecond => Mathf.Max(0f, externalAirChargeUsePerSecond);
    public float ExternalAirMaxAirBonus => Mathf.Max(0f, externalAirMaxAirBonus);

    public bool HasOccupiedEquipSlotOverrides =>
        occupiedEquipSlots != null && occupiedEquipSlots.Length > 0;

    public bool OccupiesEquipSlot(BottomBarSlotType anchorSlot, BottomBarSlotType queriedSlot)
    {
        if (queriedSlot == BottomBarSlotType.None)
            return false;

        // The anchor slot is always occupied, even if the override list forgets it.
        if (queriedSlot == anchorSlot)
            return true;

        if (occupiedEquipSlots == null || occupiedEquipSlots.Length == 0)
            return false;

        for (int i = 0; i < occupiedEquipSlots.Length; i++)
        {
            if (occupiedEquipSlots[i] == queriedSlot)
                return true;
        }

        return false;
    }

    // Clearer portable-container names.
    public bool IsPortableContainer =>
        isPortableContainer &&
        portableContainerSlotCount > 0;

    public int PortableContainerSlotCount =>
        IsPortableContainer
            ? Mathf.Max(1, portableContainerSlotCount)
            : 0;

    public int PortableContainerColumnCount =>
        Mathf.Max(1, portableContainerColumnCount);

    /// <summary>
    /// Per-slot quantity cap for this portable container.
    /// 0 means "no extra container cap" and therefore uses the incoming
    /// item's normal MaxStack.
    /// </summary>
    public int PortableContainerMaxQuantityPerSlot =>
        Mathf.Max(0, portableContainerMaxQuantityPerSlot);

    public bool PortableContainerAcceptsAnyItem =>
        portableContainerAcceptsAnyItem;

    public ItemCategoryFlags PortableContainerAllowedCategories =>
        portableContainerAllowedCategories;

    public IReadOnlyList<ItemDefinition> ExplicitlyAllowedPortableContainerItems =>
        explicitlyAllowedPortableContainerItems;

    public int PortableContainerTier =>
        Mathf.Max(0, portableContainerTier);

    // Compatibility aliases used by the existing inventory/container code.
    public bool IsContainer => IsPortableContainer;
    public int ContainerSlotCount => PortableContainerSlotCount;
    public int ContainerColumnCount => PortableContainerColumnCount;
    public int ContainerMaxQuantityPerSlot => PortableContainerMaxQuantityPerSlot;
    public ItemCategoryFlags AllowedContainerCategories => PortableContainerAllowedCategories;
    public int ContainerTier => PortableContainerTier;
    public PreferredDisplacedDestination PreferredDisplacedDestination => preferredDisplacedDestination;
    public IReadOnlyList<BottomBarSlotType> DisallowedParentSlots => disallowedParentSlots;
    public WorldItem WorldPrefab => worldPrefab;
    public WorldItemPersistencePolicy WorldPersistence => worldPersistence;

    public bool CanContainerAccept(ItemDefinition incoming)
    {
        if (!IsPortableContainer || incoming == null)
            return false;

        bool accepted =
            portableContainerAcceptsAnyItem ||
            IsExplicitlyAllowedPortableContainerItem(incoming) ||
            (portableContainerAllowedCategories != ItemCategoryFlags.None &&
             (portableContainerAllowedCategories & incoming.ItemCategories) != 0);

        if (!accepted)
            return false;

        // Preserve the existing anti-container-recursion/tier rule.
        if (incoming.IsContainer &&
            incoming.ContainerTier >= ContainerTier)
        {
            return false;
        }

        return true;
    }

    private bool IsExplicitlyAllowedPortableContainerItem(ItemDefinition incoming)
    {
        if (incoming == null ||
            explicitlyAllowedPortableContainerItems == null)
        {
            return false;
        }

        for (int i = 0;
             i < explicitlyAllowedPortableContainerItems.Count;
             i++)
        {
            if (explicitlyAllowedPortableContainerItems[i] == incoming)
                return true;
        }

        return false;
    }

    public bool IsAllowedInParentSlot(BottomBarSlotType parentSlot)
    {
        if (disallowedParentSlots == null || disallowedParentSlots.Length == 0)
            return true;

        for (int i = 0; i < disallowedParentSlots.Length; i++)
        {
            if (disallowedParentSlots[i] == parentSlot)
                return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        unitMass = Mathf.Max(0f, unitMass);
        unitExposedVolumeContribution = Mathf.Max(0f, unitExposedVolumeContribution);
        maxStack = Mathf.Max(1, maxStack);
        maxCharges = Mathf.Max(1, maxCharges);
        portableContainerSlotCount = Mathf.Max(0, portableContainerSlotCount);
        portableContainerColumnCount = Mathf.Max(1, portableContainerColumnCount);
        portableContainerMaxQuantityPerSlot = Mathf.Max(0, portableContainerMaxQuantityPerSlot);
        pickupHoldDuration = Mathf.Max(0.05f, pickupHoldDuration);

        chargeUsePerSecond = Mathf.Max(0f, chargeUsePerSecond);
        minimumChargeToUse = Mathf.Max(0, minimumChargeToUse);

        if (!consumesContainedChargesWhileUsed)
        {
            chargeUsePerSecond = 0f;
        }

        if (IsContainer)
            maxStack = 1;

        portableContainerTier = Mathf.Max(0, portableContainerTier);

        if (!IsPortableContainer)
        {
            portableContainerAcceptsAnyItem = false;
            portableContainerAllowedCategories = ItemCategoryFlags.None;
            portableContainerMaxQuantityPerSlot = 0;
            portableContainerTier = 0;

            if (explicitlyAllowedPortableContainerItems != null)
                explicitlyAllowedPortableContainerItems.Clear();
        }

        if (!hasCharges)
            maxCharges = 1;

        externalAirSupplyPerSecond = Mathf.Max(0f, externalAirSupplyPerSecond);
        externalAirChargeUsePerSecond = Mathf.Max(0f, externalAirChargeUsePerSecond);
        externalAirMaxAirBonus = Mathf.Max(0f, externalAirMaxAirBonus);

        if (!providesExternalAir)
        {
            externalAirSupplyPerSecond = 0f;
            externalAirChargeUsePerSecond = 0f;
            externalAirMaxAirBonus = 0f;
        }
    }
#endif
}