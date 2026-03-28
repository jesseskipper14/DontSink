using System.Collections.Generic;
using UnityEngine;

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
    Module = 1 << 11
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

    [Header("Stacking")]
    [Min(1)]
    [SerializeField] private int maxStack = 1;

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
    public int MaxStack => Mathf.Max(1, maxStack);
    public bool StowableInInventory => stowableInInventory;
    public bool Droppable => droppable;
    public bool Tradable => tradable;
    public bool IsInstallableModule => isModule && moduleDefinition != null;
    public BottomBarSlotType EquipSlot => equipSlot;
    public bool IsEquippable => equipSlot != BottomBarSlotType.None;
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
        Debug.Log("Can accept?");
        if (!IsContainer || incoming == null)
            return false;

        if (allowedContainerCategories == ItemCategoryFlags.None)
            return false;

        if ((allowedContainerCategories & incoming.ItemCategories) == 0)
            return false;

        if (incoming.IsContainer && incoming.ContainerTier >= ContainerTier)
            return false;

        Debug.Log("Accepted");
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
        containerSlotCount = Mathf.Max(0, containerSlotCount);
        containerColumnCount = Mathf.Max(1, containerColumnCount);
        pickupHoldDuration = Mathf.Max(0.05f, pickupHoldDuration);

        if (IsContainer)
            maxStack = 1;

        containerTier = Mathf.Max(0, containerTier);

        if (!IsContainer)
        {
            allowedContainerCategories = ItemCategoryFlags.None;
            containerTier = 0;
        }
    }
#endif
}