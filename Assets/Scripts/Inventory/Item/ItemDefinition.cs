using System.Collections.Generic;
using UnityEngine;
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
    Sacred = 1 << 13
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

    [Header("Container")]
    [SerializeField] private bool isContainer;
    [Min(0)]
    [SerializeField] private int containerSlotCount = 0;
    [Min(1)]
    [SerializeField] private int containerColumnCount = 4;
    [SerializeField] private ItemCategoryFlags allowedContainerCategories = ItemCategoryFlags.None;
    [SerializeField] private int containerTier = 0;

    [Header("Pickup")]
    [SerializeField] private PickupInteractionMode pickupMode = PickupInteractionMode.Instant;
    [SerializeField] private float pickupHoldDuration = 0.4f;

    public PickupInteractionMode PickupMode => pickupMode;
    public float PickupHoldDuration => Mathf.Max(0.05f, pickupHoldDuration);

    [Header("Module")]
    [SerializeField] private bool isModule;
    [SerializeField] private ModuleDefinition moduleDefinition;

    public bool IsModule => isModule;
    public ModuleDefinition ModuleDefinition => moduleDefinition;

    [Header("World")]
    [SerializeField] private WorldItem worldPrefab;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public ItemCategoryFlags ItemCategories => itemCategories;
    public bool IsSacred => (itemCategories & ItemCategoryFlags.Sacred) != 0;
    public int MaxStack => Mathf.Max(1, maxStack);

    public bool HasCharges => hasCharges;
    public int MaxCharges => hasCharges ? Mathf.Max(1, maxCharges) : 0;

    public bool StowableInInventory => stowableInInventory;
    public bool Droppable => droppable;
    public bool Tradable => tradable;
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

    public bool IsContainer => isContainer && containerSlotCount > 0;
    public int ContainerSlotCount => IsContainer ? Mathf.Max(1, containerSlotCount) : 0;
    public int ContainerColumnCount => Mathf.Max(1, containerColumnCount);
    public ItemCategoryFlags AllowedContainerCategories => allowedContainerCategories;
    public int ContainerTier => Mathf.Max(0, containerTier);
    public PreferredDisplacedDestination PreferredDisplacedDestination => preferredDisplacedDestination;
    public IReadOnlyList<BottomBarSlotType> DisallowedParentSlots => disallowedParentSlots;
    public WorldItem WorldPrefab => worldPrefab;

    public bool CanContainerAccept(ItemDefinition incoming)
    {
        if (!IsContainer || incoming == null)
            return false;

        if (allowedContainerCategories == ItemCategoryFlags.None)
            return false;

        if ((allowedContainerCategories & incoming.ItemCategories) == 0)
            return false;

        if (incoming.IsContainer && incoming.ContainerTier >= ContainerTier)
            return false;

        return true;
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
        maxStack = Mathf.Max(1, maxStack);
        maxCharges = Mathf.Max(1, maxCharges);
        containerSlotCount = Mathf.Max(0, containerSlotCount);
        containerColumnCount = Mathf.Max(1, containerColumnCount);
        pickupHoldDuration = Mathf.Max(0.05f, pickupHoldDuration);

        chargeUsePerSecond = Mathf.Max(0f, chargeUsePerSecond);
        minimumChargeToUse = Mathf.Max(0, minimumChargeToUse);

        if (!consumesContainedChargesWhileUsed)
        {
            chargeUsePerSecond = 0f;
        }

        if (IsContainer)
            maxStack = 1;

        containerTier = Mathf.Max(0, containerTier);

        if (!IsContainer)
        {
            allowedContainerCategories = ItemCategoryFlags.None;
            containerTier = 0;
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