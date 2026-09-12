using System;
using System.Collections.Generic;
using UnityEngine;

public enum InstalledStorageAcceptanceMode
{
    [InspectorName("Storage Type Default")]
    StorageTypeDefault = 0,

    [InspectorName("Accept Any Item")]
    AnyItem = 1,

    [InspectorName("Filtered")]
    Filtered = 2
}

[Serializable]
public sealed class StorageModuleDefinition
{
    [Header("Installed Storage Type")]
    [Tooltip("None = no installed storage. Fixed Storage = locker/bin/special slot. Container Rack = storage intended for portable containers/cargo.")]
    [SerializeField] private StorageModuleMode mode = StorageModuleMode.None;

    [Header("Installed Storage Layout")]
    [Min(1)]
    [SerializeField] private int slotCount = 12;

    [Min(1)]
    [SerializeField] private int columnCount = 4;

    [Header("Installed Storage - Accepted Items")]
    [Tooltip(
        "Storage Type Default preserves the existing behavior: " +
        "Fixed Storage accepts any item; Container Rack uses the portable-container/cargo toggles below. " +
        "Choose Filtered when you want to author exactly what this installed storage accepts.")]
    [SerializeField]
    private InstalledStorageAcceptanceMode acceptanceMode =
        InstalledStorageAcceptanceMode.StorageTypeDefault;

    [Tooltip("Filtered mode: items matching ANY selected category are accepted.")]
    [SerializeField]
    private ItemCategoryFlags allowedItemCategories =
        ItemCategoryFlags.None;

    [Tooltip(
        "Filtered mode: specific ItemDefinitions accepted by this installed storage. " +
        "For an Anchor Hole, add the Anchor ItemDefinition here.")]
    [SerializeField] private List<ItemDefinition> explicitlyAllowedItems = new();

    [Tooltip("Filtered mode, or Container Rack default mode: allow any portable-container item.")]
    [SerializeField] private bool acceptsPortableContainers = false;

    [Tooltip("Filtered mode, or Container Rack default mode: allow items in the Cargo category.")]
    [SerializeField] private bool acceptsCargoCrates = false;

    [Header("Future Securing Rules")]
    [Tooltip("If true, contents are treated as intentionally secured to the boat.")]
    [SerializeField] private bool countsAsSecuredStorage = true;

    [Tooltip("Future hook. Keep at 0 for now. Later storms/combat may use this.")]
    [Range(0f, 1f)]
    [SerializeField] private float looseFailureChance = 0f;

    public StorageModuleMode Mode => mode;
    public int SlotCount => Mathf.Max(1, slotCount);
    public int ColumnCount => Mathf.Max(1, columnCount);

    public InstalledStorageAcceptanceMode AcceptanceMode => acceptanceMode;
    public ItemCategoryFlags AllowedItemCategories => allowedItemCategories;
    public IReadOnlyList<ItemDefinition> ExplicitlyAllowedItems => explicitlyAllowedItems;

    public bool AcceptsPortableContainers => acceptsPortableContainers;
    public bool AcceptsCargoCrates => acceptsCargoCrates;

    public bool CountsAsSecuredStorage => countsAsSecuredStorage;
    public float LooseFailureChance => Mathf.Clamp01(looseFailureChance);

    public bool HasStorage => mode != StorageModuleMode.None;
    public bool IsFixedStorage => mode == StorageModuleMode.FixedStorage;
    public bool IsContainerRack => mode == StorageModuleMode.ContainerRack;

    public bool CanAcceptItem(ItemDefinition incoming)
    {
        if (!HasStorage || incoming == null)
            return false;

        switch (acceptanceMode)
        {
            case InstalledStorageAcceptanceMode.AnyItem:
                return true;

            case InstalledStorageAcceptanceMode.Filtered:
                return MatchesFilteredRules(incoming);

            case InstalledStorageAcceptanceMode.StorageTypeDefault:
            default:
                return MatchesStorageTypeDefault(incoming);
        }
    }

    private bool MatchesStorageTypeDefault(ItemDefinition incoming)
    {
        if (incoming == null)
            return false;

        if (IsFixedStorage)
            return true;

        if (IsContainerRack)
        {
            if (incoming.IsContainer && acceptsPortableContainers)
                return true;

            if ((incoming.ItemCategories & ItemCategoryFlags.Cargo) != 0 &&
                acceptsCargoCrates)
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesFilteredRules(ItemDefinition incoming)
    {
        if (incoming == null)
            return false;

        if (IsExplicitlyAllowed(incoming))
            return true;

        if (allowedItemCategories != ItemCategoryFlags.None &&
            (allowedItemCategories & incoming.ItemCategories) != 0)
        {
            return true;
        }

        if (acceptsPortableContainers && incoming.IsContainer)
            return true;

        if (acceptsCargoCrates &&
            (incoming.ItemCategories & ItemCategoryFlags.Cargo) != 0)
        {
            return true;
        }

        return false;
    }

    private bool IsExplicitlyAllowed(ItemDefinition incoming)
    {
        if (incoming == null || explicitlyAllowedItems == null)
            return false;

        for (int i = 0; i < explicitlyAllowedItems.Count; i++)
        {
            if (explicitlyAllowedItems[i] == incoming)
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    public void Editor_SetDefaultsForMode()
    {
        acceptanceMode = InstalledStorageAcceptanceMode.StorageTypeDefault;
        allowedItemCategories = ItemCategoryFlags.None;

        switch (mode)
        {
            case StorageModuleMode.None:
                slotCount = 1;
                columnCount = 1;
                acceptsPortableContainers = false;
                acceptsCargoCrates = false;
                countsAsSecuredStorage = true;
                looseFailureChance = 0f;
                break;

            case StorageModuleMode.FixedStorage:
                slotCount = 12;
                columnCount = 4;
                acceptsPortableContainers = false;
                acceptsCargoCrates = false;
                countsAsSecuredStorage = true;
                looseFailureChance = 0f;
                break;

            case StorageModuleMode.ContainerRack:
                slotCount = 4;
                columnCount = 2;
                acceptsPortableContainers = true;
                acceptsCargoCrates = true;
                countsAsSecuredStorage = true;
                looseFailureChance = 0f;
                break;
        }
    }
#endif
}
