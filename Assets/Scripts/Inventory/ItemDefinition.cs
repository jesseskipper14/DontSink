using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Game/Inventory/Item Definition")]
public sealed class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Stacking")]
    [Min(1)]
    [SerializeField] private int maxStack = 1;

    [Header("Rules")]
    [SerializeField] private bool stowableInInventory = true;
    [SerializeField] private bool droppable = true;
    [SerializeField] private bool tradable = true;

    [Header("Equip")]
    [SerializeField] private BottomBarSlotType equipSlot = BottomBarSlotType.None;

    [Header("Container")]
    [SerializeField] private bool isContainer;
    [Min(0)]
    [SerializeField] private int containerSlotCount = 0;
    [Min(1)]
    [SerializeField] private int containerColumnCount = 4;

    [Header("World")]
    [SerializeField] private WorldItem worldPrefab;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public int MaxStack => Mathf.Max(1, maxStack);

    public bool StowableInInventory => stowableInInventory;
    public bool Droppable => droppable;
    public bool Tradable => tradable;

    public BottomBarSlotType EquipSlot => equipSlot;
    public bool IsEquippable => equipSlot != BottomBarSlotType.None;

    public bool IsContainer => isContainer && containerSlotCount > 0;
    public int ContainerSlotCount => IsContainer ? Mathf.Max(1, containerSlotCount) : 0;
    public int ContainerColumnCount => Mathf.Max(1, containerColumnCount);

    public WorldItem WorldPrefab => worldPrefab;

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxStack = Mathf.Max(1, maxStack);
        containerSlotCount = Mathf.Max(0, containerSlotCount);
        containerColumnCount = Mathf.Max(1, containerColumnCount);

        if (IsContainer)
            maxStack = 1;
    }
#endif
}