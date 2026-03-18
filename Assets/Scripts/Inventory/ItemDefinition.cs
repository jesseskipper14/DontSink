using NUnit.Framework.Internal.Execution;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemDefinition",
    menuName = "Game/Inventory/Item Definition")]
public sealed class ItemDefinition : ScriptableObject
{
    public enum EquipSlotType
    {
        None = 0,
        Backpack = 1,
        Toolbelt = 2
    }

    [Header("Identity")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Stacking")]
    [Min(1)]
    [SerializeField] private int maxStack = 1;

    [Header("Physical")]
    [Min(0f)]
    [SerializeField] private float mass = 0f;

    [Header("Inventory Rules")]
    [SerializeField] private bool stowableInInventory = true;
    [SerializeField] private bool droppable = true;
    [SerializeField] private bool tradable = true;

    [Header("Equipment")]
    [SerializeField] private EquipSlotType equipSlot = EquipSlotType.None;
    [Min(0)]
    [SerializeField] private int bonusInventorySlots = 0;

    [Header("World")]
    [SerializeField] private WorldItem worldPrefab;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;

    public int MaxStack => Mathf.Max(1, maxStack);
    public float Mass => Mathf.Max(0f, mass);

    public bool StowableInInventory => stowableInInventory;
    public bool Droppable => droppable;
    public bool Tradable => tradable;

    public EquipSlotType EquipSlot => equipSlot;
    public int BonusInventorySlots => Mathf.Max(0, bonusInventorySlots);

    public bool IsEquippable => equipSlot != EquipSlotType.None;

    public WorldItem WorldPrefab => worldPrefab;
}