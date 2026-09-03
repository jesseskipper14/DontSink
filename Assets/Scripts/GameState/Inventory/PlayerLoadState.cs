using Survival.Attributes;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerLoadState : MonoBehaviour, IVolumeContribution
{
    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerAttributeState attributes;
    [SerializeField] private ForceBody2D forceBody;

    [Header("Fallbacks")]
    [Tooltip("Used only if the attribute profile has no EncumbranceCapacity entry.")]
    [SerializeField, Min(0f)] private float fallbackEncumbranceCapacity = 100f;

    [Header("Debug")]
    [SerializeField] private bool logChanges = false;

    private readonly HashSet<ItemInstance> _topLevelItems = new();
    private readonly HashSet<ItemInstance> _subscribedItems = new();
    private readonly HashSet<ItemInstance> _massTraversalVisited = new();
    private readonly HashSet<ItemInstance> _exposedVolumeVisited = new();

    private Rigidbody2D _rb;
    private int _lastBuffVersion = int.MinValue;

    public event Action<PlayerLoadState> Changed;

    /// <summary>
    /// Actual Rigidbody2D body mass used by player locomotion/physics.
    /// Inventory does NOT get written into Rigidbody2D.mass.
    /// </summary>
    public float BodyMass { get; private set; }

    /// <summary>
    /// Real physical mass of hotbar + equipped items, including recursive
    /// container contents. This is used for load/boat mass, not by directly
    /// changing the player's Rigidbody mass.
    /// </summary>
    public float CarriedMass { get; private set; }

    /// <summary>
    /// BodyMass + CarriedMass. This is the player's truthful contribution to
    /// vehicles/boats while preserving a stable locomotion Rigidbody mass.
    /// </summary>
    public float TotalPhysicalMass { get; private set; }

    /// <summary>
    /// Additional displacement volume from items that are physically exposed on
    /// the player. Only actual equipment anchors count, including Hands. Hotbar
    /// storage and items nested inside containers do not contribute.
    /// </summary>
    public float ExposedVolumeContribution { get; private set; }

    /// <summary>
    /// IVolumeContribution hook consumed by ForceBody2D. Carried mass intentionally
    /// remains separate from personal buoyancy; this only contributes exposed volume.
    /// </summary>
    public float VolumeContribution => ExposedVolumeContribution;

    /// <summary>
    /// Gameplay carrying capacity resolved through PlayerAttributeState, so
    /// ordinary equipment buffs can add/multiply/override it.
    /// </summary>
    public float EncumbranceCapacity { get; private set; }

    public float LoadRatio =>
        EncumbranceCapacity > 0.0001f
            ? CarriedMass / EncumbranceCapacity
            : (CarriedMass > 0f ? float.PositiveInfinity : 0f);

    public bool IsOverCapacity =>
        CarriedMass > EncumbranceCapacity + 0.0001f;

    private static readonly BottomBarSlotType[] EquipmentAnchorSlots =
    {
        BottomBarSlotType.Hands,
        BottomBarSlotType.Head,
        BottomBarSlotType.Feet,
        BottomBarSlotType.Toolbelt,
        BottomBarSlotType.Backpack,
        BottomBarSlotType.Body
    };

    private void Reset()
    {
        ResolveRefs();
    }

    private void Awake()
    {
        ResolveRefs();
        RebuildTopLevelItemSubscriptions();
        RefreshNow(forceEvent: false);
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (forceBody != null)
            forceBody.RegisterVolumeContribution(this);

        if (inventory != null)
            inventory.InventoryChanged += HandleInventoryChanged;

        if (equipment != null)
            equipment.EquipmentChanged += HandleEquipmentChanged;

        RebuildTopLevelItemSubscriptions();
        RefreshNow(forceEvent: true);
    }

    private void OnDisable()
    {
        if (forceBody != null)
            forceBody.UnregisterVolumeContribution(this);

        if (inventory != null)
            inventory.InventoryChanged -= HandleInventoryChanged;

        if (equipment != null)
            equipment.EquipmentChanged -= HandleEquipmentChanged;

        ClearTopLevelItemSubscriptions();
    }

    private void FixedUpdate()
    {
        // Buffs may alter EncumbranceCapacity without inventory/equipment changing.
        // PlayerBuffSystem exposes Version rather than a change event, so this is
        // a tiny version check rather than a mass-tree polling pass.
        int buffVersion =
            attributes != null &&
            attributes.BuffSystem != null
                ? attributes.BuffSystem.Version
                : -1;

        float currentBodyMass =
            _rb != null
                ? Mathf.Max(0f, _rb.mass)
                : 0f;

        if (buffVersion != _lastBuffVersion ||
            !Mathf.Approximately(currentBodyMass, BodyMass))
        {
            RefreshNow(forceEvent: false);
        }
    }

    public void RefreshNow(bool forceEvent = false)
    {
        ResolveRefs();

        float nextBodyMass =
            _rb != null
                ? Mathf.Max(0f, _rb.mass)
                : 0f;

        float nextCarriedMass =
            CalculateCarriedMass();

        float nextExposedVolume =
            CalculateExposedVolumeContribution();

        float nextCapacity =
            attributes != null
                ? Mathf.Max(
                    0f,
                    attributes.GetFloat(
                        PlayerAttributeId.EncumbranceCapacity,
                        fallbackEncumbranceCapacity))
                : Mathf.Max(
                    0f,
                    fallbackEncumbranceCapacity);

        float nextTotalMass =
            Mathf.Max(
                0f,
                nextBodyMass +
                nextCarriedMass);

        bool changed =
            !Mathf.Approximately(BodyMass, nextBodyMass) ||
            !Mathf.Approximately(CarriedMass, nextCarriedMass) ||
            !Mathf.Approximately(TotalPhysicalMass, nextTotalMass) ||
            !Mathf.Approximately(ExposedVolumeContribution, nextExposedVolume) ||
            !Mathf.Approximately(EncumbranceCapacity, nextCapacity);

        BodyMass = nextBodyMass;
        CarriedMass = nextCarriedMass;
        TotalPhysicalMass = nextTotalMass;
        ExposedVolumeContribution = nextExposedVolume;
        EncumbranceCapacity = nextCapacity;

        _lastBuffVersion =
            attributes != null &&
            attributes.BuffSystem != null
                ? attributes.BuffSystem.Version
                : -1;

        if (!changed && !forceEvent)
            return;

        if (logChanges)
        {
            Debug.Log(
                $"[PlayerLoadState:{name}] " +
                $"body={BodyMass:F3}, " +
                $"carried={CarriedMass:F3}, " +
                $"total={TotalPhysicalMass:F3}, " +
                $"exposedVolume={ExposedVolumeContribution:F3}, " +
                $"capacity={EncumbranceCapacity:F3}, " +
                $"ratio={(float.IsInfinity(LoadRatio) ? "INF" : LoadRatio.ToString("F3"))}, " +
                $"over={IsOverCapacity}",
                this);
        }

        Changed?.Invoke(this);
    }

    private float CalculateCarriedMass()
    {
        _massTraversalVisited.Clear();

        float total = 0f;

        CollectTopLevelItems(_topLevelItems);

        foreach (ItemInstance item in _topLevelItems)
            total += CalculateUniqueRecursiveMass(item);

        return Mathf.Max(0f, total);
    }

    private float CalculateUniqueRecursiveMass(ItemInstance item)
    {
        if (item == null ||
            item.Definition == null ||
            item.Quantity <= 0 ||
            !_massTraversalVisited.Add(item))
        {
            return 0f;
        }

        float total =
            Mathf.Max(0f, item.OwnMass);

        ItemContainerState container =
            item.ContainerState;

        if (container == null ||
            container.Slots == null)
        {
            return total;
        }

        IReadOnlyList<InventorySlot> slots =
            container.Slots;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot =
                slots[i];

            if (slot == null ||
                slot.IsEmpty ||
                slot.Instance == null)
            {
                continue;
            }

            total +=
                CalculateUniqueRecursiveMass(
                    slot.Instance);
        }

        return total;
    }

    private float CalculateExposedVolumeContribution()
    {
        _exposedVolumeVisited.Clear();

        if (equipment == null)
            return 0f;

        float total = 0f;

        for (int i = 0;
             i < EquipmentAnchorSlots.Length;
             i++)
        {
            ItemInstance item =
                equipment.Get(
                    EquipmentAnchorSlots[i]);

            if (item == null ||
                item.Definition == null ||
                item.Quantity <= 0 ||
                !_exposedVolumeVisited.Add(item))
            {
                continue;
            }

            total +=
                Mathf.Max(
                    0f,
                    item.ExposedVolumeContribution);
        }

        return Mathf.Max(0f, total);
    }

    private void HandleInventoryChanged()
    {
        RebuildTopLevelItemSubscriptions();
        RefreshNow();
    }

    private void HandleEquipmentChanged()
    {
        RebuildTopLevelItemSubscriptions();
        RefreshNow();
    }

    private void HandleTopLevelItemChanged()
    {
        // ItemInstance.Changed already bubbles nested container changes upward.
        // Rebuilding also safely captures any unusual ownership/state transition.
        RebuildTopLevelItemSubscriptions();
        RefreshNow();
    }

    private void RebuildTopLevelItemSubscriptions()
    {
        HashSet<ItemInstance> next =
            new HashSet<ItemInstance>();

        CollectTopLevelItems(next);

        foreach (ItemInstance previous in _subscribedItems)
        {
            if (previous != null &&
                !next.Contains(previous))
            {
                previous.Changed -=
                    HandleTopLevelItemChanged;
            }
        }

        foreach (ItemInstance current in next)
        {
            if (current != null &&
                !_subscribedItems.Contains(current))
            {
                current.Changed +=
                    HandleTopLevelItemChanged;
            }
        }

        _subscribedItems.Clear();

        foreach (ItemInstance current in next)
            _subscribedItems.Add(current);
    }

    private void ClearTopLevelItemSubscriptions()
    {
        foreach (ItemInstance item in _subscribedItems)
        {
            if (item != null)
            {
                item.Changed -=
                    HandleTopLevelItemChanged;
            }
        }

        _subscribedItems.Clear();
    }

    private void CollectTopLevelItems(
        HashSet<ItemInstance> destination)
    {
        destination.Clear();

        if (inventory != null)
        {
            for (int i = 0;
                 i < inventory.HotbarSlotCount;
                 i++)
            {
                InventorySlot slot =
                    inventory.GetSlot(i);

                ItemInstance item =
                    slot != null &&
                    !slot.IsEmpty
                        ? slot.Instance
                        : null;

                if (item != null)
                    destination.Add(item);
            }
        }

        if (equipment != null)
        {
            for (int i = 0;
                 i < EquipmentAnchorSlots.Length;
                 i++)
            {
                ItemInstance item =
                    equipment.Get(
                        EquipmentAnchorSlots[i]);

                if (item != null)
                    destination.Add(item);
            }
        }
    }

    private void ResolveRefs()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        if (forceBody == null)
        {
            forceBody =
                GetComponent<ForceBody2D>() ??
                GetComponentInChildren<ForceBody2D>(true) ??
                GetComponentInParent<ForceBody2D>();
        }

        if (inventory == null)
        {
            inventory =
                GetComponent<PlayerInventory>() ??
                GetComponentInChildren<PlayerInventory>(true) ??
                GetComponentInParent<PlayerInventory>();
        }

        if (equipment == null)
        {
            equipment =
                GetComponent<PlayerEquipment>() ??
                GetComponentInChildren<PlayerEquipment>(true) ??
                GetComponentInParent<PlayerEquipment>();
        }

        if (attributes == null)
        {
            attributes =
                GetComponent<PlayerAttributeState>() ??
                GetComponentInChildren<PlayerAttributeState>(true) ??
                GetComponentInParent<PlayerAttributeState>();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Log Current Load")]
    private void DebugLogCurrentLoad()
    {
        RefreshNow(forceEvent: false);

        Debug.Log(
            $"[PlayerLoadState:{name}] " +
            $"BODY={BodyMass:F3} " +
            $"CARRIED={CarriedMass:F3} " +
            $"TOTAL={TotalPhysicalMass:F3} " +
            $"EXPOSED_VOLUME={ExposedVolumeContribution:F3} " +
            $"CAPACITY={EncumbranceCapacity:F3} " +
            $"LOAD={(float.IsInfinity(LoadRatio) ? "INF" : (LoadRatio * 100f).ToString("F1") + "%")} " +
            $"OVER={IsOverCapacity}",
            this);
    }
#endif
}
