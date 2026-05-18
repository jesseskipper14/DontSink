using System;
using System.Collections.Generic;
using UnityEngine;

public enum SecureZoneKind
{
    CargoBay,
    TieDownAnchor,
    DeckTieDown,
    Other
}

public enum BoatSecureSupportKind
{
    None,
    DirectSurface,
    SecuredCargo,
    UnsecuredCargo,
    OtherWorldItem
}

public enum SecureZoneCapacityMode
{
    Fixed,
    AreaBased
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BoatSecureZone : MonoBehaviour
{
    [Serializable]
    public sealed class SecuredSlot
    {
        public string occupiedItemInstanceId;
        public Vector2 localPosition;
        public float localRotationZ;

        public bool IsOccupied => !string.IsNullOrWhiteSpace(occupiedItemInstanceId);

        public void Clear()
        {
            occupiedItemInstanceId = null;
            localPosition = Vector2.zero;
            localRotationZ = 0f;
        }
    }

    [Header("Identity")]
    [SerializeField] private string stableId;

    [Header("Zone Type")]
    [SerializeField] private SecureZoneKind zoneKind = SecureZoneKind.CargoBay;

    [Header("Capacity")]
    [SerializeField] private SecureZoneCapacityMode capacityMode = SecureZoneCapacityMode.Fixed;

    [Tooltip("Used when Capacity Mode is Fixed. Good for cleats / tie-down anchors.")]
    [Min(1)]
    [SerializeField] private int fixedCapacity = 2;

    [Tooltip("Used when Capacity Mode is AreaBased. Capacity = floor(collider area / areaPerSlot), clamped by maxAreaBasedCapacity.")]
    [Min(0.1f)]
    [SerializeField] private float areaPerSlot = 2f;

    [Tooltip("Used when Capacity Mode is AreaBased. Prevents giant zones from becoming infinite cargo safety rectangles.")]
    [Min(1)]
    [SerializeField] private int maxAreaBasedCapacity = 24;

    [Tooltip("If true, cargo items may be secured in this zone.")]
    [SerializeField] private bool acceptsCargo = true;

    [Tooltip("Future seam: allow non-cargo items like chests/tools to be secured here.")]
    [SerializeField] private bool acceptsNonCargo = false;

    [Tooltip("If false, this zone is intended for one floor layer of cargo. If true, future secure logic may allow vertically stacked cargo within this zone height.")]
    [SerializeField] private bool allowStackedCargo = false;

    [Header("Quality")]
    [Range(0f, 2f)]
    [SerializeField] private float zoneQualityMultiplier = 1f;

    [Range(0f, 5f)]
    [SerializeField] private float passiveDecayMultiplier = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float impactProtection01 = 0.25f;

    [Header("Runtime Slots")]
    [SerializeField] private List<SecuredSlot> slots = new();

    [Header("Grounding / Support")]

    [Tooltip("Optional explicit support colliders. Trigger colliders here may count as direct floor support, useful for CargoZoneFloor floor overlays.")]
    [SerializeField] private Collider2D[] directSupportColliders;

    [Tooltip("Layers the grounding probe is allowed to hit. Leave as Everything unless debugging says otherwise.")]
    [SerializeField] private LayerMask supportProbeLayerMask = ~0;

    [Tooltip("How far below the cargo bottom the grounding probe searches.")]
    [Min(0.001f)]
    [SerializeField] private float supportVerticalTolerance = 0.12f;

    [Tooltip("Height of the grounding probe box.")]
    [Min(0.001f)]
    [SerializeField] private float supportProbeHeight = 0.06f;

    [Tooltip("Horizontal inset used when checking support below the cargo.")]
    [Min(0f)]
    [SerializeField] private float supportHorizontalInset = 0.03f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Collider2D _zoneCollider;
    private Boat _boat;

    public string StableId => stableId;
    public SecureZoneKind ZoneKind => zoneKind;
    public SecureZoneCapacityMode CapacityMode => capacityMode;

    public bool AllowStackedCargo => allowStackedCargo;

    public int FixedCapacity => Mathf.Max(1, fixedCapacity);
    public float AreaPerSlot => Mathf.Max(0.1f, areaPerSlot);
    public int MaxAreaBasedCapacity => Mathf.Max(1, maxAreaBasedCapacity);

    public int Capacity => CalculateCapacity();

    public bool AcceptsCargo => acceptsCargo;
    public bool AcceptsNonCargo => acceptsNonCargo;

    public float ZoneQualityMultiplier => Mathf.Max(0f, zoneQualityMultiplier);
    public float PassiveDecayMultiplier => Mathf.Max(0f, passiveDecayMultiplier);
    public float ImpactProtection01 => Mathf.Clamp01(impactProtection01);


    public Collider2D ZoneCollider => _zoneCollider;

    public float EstimatedArea => CalculateColliderArea();

    public int OccupiedCount
    {
        get
        {
            EnsureSlots();

            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].IsOccupied)
                    count++;
            }

            return count;
        }
    }

    public int FreeCount => Mathf.Max(0, Capacity - OccupiedCount);

    private void Reset()
    {
        EnsureStableId();
        CacheRefs();
        ApplyKindDefaults();

        if (_zoneCollider != null)
            _zoneCollider.isTrigger = true;
    }

    private void Awake()
    {
        EnsureStableId();
        CacheRefs();
        EnsureSlots();
    }

    private void OnValidate()
    {
        fixedCapacity = Mathf.Max(1, fixedCapacity);
        areaPerSlot = Mathf.Max(0.1f, areaPerSlot);
        maxAreaBasedCapacity = Mathf.Max(1, maxAreaBasedCapacity);

        zoneQualityMultiplier = Mathf.Max(0f, zoneQualityMultiplier);
        passiveDecayMultiplier = Mathf.Max(0f, passiveDecayMultiplier);
        impactProtection01 = Mathf.Clamp01(impactProtection01);

        supportVerticalTolerance = Mathf.Max(0.001f, supportVerticalTolerance);
        supportProbeHeight = Mathf.Max(0.001f, supportProbeHeight);
        supportHorizontalInset = Mathf.Max(0f, supportHorizontalInset);

        EnsureStableId();
        CacheRefs();
        EnsureSlots();

        if (_zoneCollider != null)
            _zoneCollider.isTrigger = true;
    }

    [ContextMenu("Apply Kind Defaults")]
    public void ApplyKindDefaults()
    {
        switch (zoneKind)
        {
            case SecureZoneKind.CargoBay:
                capacityMode = SecureZoneCapacityMode.AreaBased;
                areaPerSlot = 2f;
                maxAreaBasedCapacity = 24;
                zoneQualityMultiplier = 1f;
                passiveDecayMultiplier = 0.75f;
                impactProtection01 = 0.35f;
                acceptsCargo = true;
                acceptsNonCargo = false;
                break;

            case SecureZoneKind.TieDownAnchor:
                capacityMode = SecureZoneCapacityMode.Fixed;
                fixedCapacity = 1;
                zoneQualityMultiplier = 0.85f;
                passiveDecayMultiplier = 1.15f;
                impactProtection01 = 0.2f;
                acceptsCargo = true;
                acceptsNonCargo = false;
                break;

            case SecureZoneKind.DeckTieDown:
                capacityMode = SecureZoneCapacityMode.Fixed;
                fixedCapacity = 2;
                zoneQualityMultiplier = 0.8f;
                passiveDecayMultiplier = 1.25f;
                impactProtection01 = 0.15f;
                acceptsCargo = true;
                acceptsNonCargo = false;
                break;

            case SecureZoneKind.Other:
                capacityMode = SecureZoneCapacityMode.Fixed;
                fixedCapacity = Mathf.Max(1, fixedCapacity);
                acceptsCargo = true;
                break;
        }

        EnsureSlots();
    }

    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        CacheRefs();

        if (_zoneCollider == null)
            return false;

        return _zoneCollider.OverlapPoint(worldPoint);
    }

    public bool CanAccept(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        bool isCargo = CargoLabelFormatter.IsCargo(item);

        if (isCargo && !acceptsCargo)
            return false;

        if (!isCargo && !acceptsNonCargo)
            return false;

        return HasFreeSlot();
    }

    public bool HasFreeSlot()
    {
        EnsureSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null || !slots[i].IsOccupied)
                return true;
        }

        return false;
    }

    public void EditorSetStackedCargoAllowed(bool allowed)
    {
        allowStackedCargo = allowed;
    }

    public bool HasPhysicalSupport(WorldItem worldItem)
    {
        if (!TryFindPhysicalSupport(worldItem, out BoatSecureSupportKind supportKind, out Collider2D supportCollider))
            return false;

        switch (supportKind)
        {
            case BoatSecureSupportKind.DirectSurface:
                Log($"Support OK: direct surface '{supportCollider.name}'.");
                return true;

            case BoatSecureSupportKind.SecuredCargo:
                if (allowStackedCargo)
                {
                    Log($"Support OK: secured stacked cargo '{supportCollider.name}'.");
                    return true;
                }

                Log($"Support rejected: cargo is stacked on secured cargo '{supportCollider.name}', but stacked cargo is disabled.");
                return false;

            case BoatSecureSupportKind.UnsecuredCargo:
                Log($"Support rejected: cargo is resting on unsecured cargo '{supportCollider.name}'.");
                return false;

            case BoatSecureSupportKind.OtherWorldItem:
                Log($"Support rejected: cargo is resting on another world item '{supportCollider.name}'.");
                return false;

            default:
                Log("Support rejected: no valid grounding source.");
                return false;
        }
    }

    public bool TryFindPhysicalSupport(
        WorldItem worldItem,
        out BoatSecureSupportKind supportKind,
        out Collider2D supportCollider)
    {
        supportKind = BoatSecureSupportKind.None;
        supportCollider = null;

        if (worldItem == null || worldItem.Instance == null || worldItem.Instance.Definition == null)
            return false;

        Collider2D itemCollider = worldItem.GetComponentInChildren<Collider2D>();
        if (itemCollider == null)
            return false;

        Bounds cargoBounds = itemCollider.bounds;

        float probeWidth = Mathf.Max(
            0.03f,
            cargoBounds.size.x - supportHorizontalInset * 2f);

        float probeHeight = Mathf.Max(0.001f, supportProbeHeight);

        Vector2 probeSize = new Vector2(probeWidth, probeHeight);

        // Start the probe just inside the bottom of the cargo, then cast downward.
        // This catches surfaces that are touching or just barely separated.
        Vector2 probeOrigin = new Vector2(
            cargoBounds.center.x,
            cargoBounds.min.y + probeHeight * 0.5f);

        int mask = supportProbeLayerMask.value == 0
            ? Physics2D.AllLayers
            : supportProbeLayerMask.value;

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            probeOrigin,
            probeSize,
            0f,
            Vector2.down,
            Mathf.Max(0.001f, supportVerticalTolerance),
            mask);

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, CompareSupportHits);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            if (!TryClassifySupportCollider(
                    worldItem,
                    hitCollider,
                    out BoatSecureSupportKind candidateKind))
            {
                continue;
            }

            supportKind = candidateKind;
            supportCollider = hitCollider;
            return candidateKind == BoatSecureSupportKind.DirectSurface ||
                   candidateKind == BoatSecureSupportKind.SecuredCargo;
        }

        return false;
    }

    private bool TryClassifySupportCollider(
    WorldItem candidate,
    Collider2D hitCollider,
    out BoatSecureSupportKind supportKind)
    {
        supportKind = BoatSecureSupportKind.None;

        if (candidate == null || hitCollider == null)
            return false;

        // Ignore the candidate's own colliders.
        if (hitCollider.transform == candidate.transform || hitCollider.transform.IsChildOf(candidate.transform))
            return false;

        CacheRefs();

        // Ignore this zone's own trigger volume.
        if (_zoneCollider != null && hitCollider == _zoneCollider)
            return false;

        // Do not ever treat the player/interactor as cargo support.
        // Yes, this bug was hilarious. No, it may not live.
        if (IsKnownNonSupportActor(hitCollider))
            return false;

        bool explicitDirectSupport = IsExplicitDirectSupportCollider(hitCollider);

        // Trigger colliders only count if explicitly registered as support.
        // CargoZoneFloor's floor trigger is the main intended case.
        if (hitCollider.isTrigger && !explicitDirectSupport)
            return false;

        if (explicitDirectSupport)
        {
            supportKind = BoatSecureSupportKind.DirectSurface;
            return true;
        }

        WorldItem supportWorldItem = hitCollider.GetComponentInParent<WorldItem>();
        if (supportWorldItem != null)
        {
            if (supportWorldItem == candidate)
                return false;

            BoatSecuredItem secured = supportWorldItem.GetComponent<BoatSecuredItem>();

            if (secured != null &&
                secured.IsSecured &&
                string.Equals(secured.SecureZoneStableId, stableId, StringComparison.Ordinal))
            {
                supportKind = BoatSecureSupportKind.SecuredCargo;
                return true;
            }

            bool isCargo = supportWorldItem.Instance != null &&
                           CargoLabelFormatter.IsCargo(supportWorldItem.Instance);

            supportKind = isCargo
                ? BoatSecureSupportKind.UnsecuredCargo
                : BoatSecureSupportKind.OtherWorldItem;

            return true;
        }

        // For non-item solid colliders, only count them as direct support if they belong
        // to this boat's hierarchy. This lets cleats use deck/hull/floor colliders without
        // accepting the player, dock junk, random scene props, or some cursed passing seagull.
        if (IsColliderOnThisBoat(hitCollider))
        {
            supportKind = BoatSecureSupportKind.DirectSurface;
            return true;
        }

        return false;
    }

    private bool IsKnownNonSupportActor(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return false;

        // PlayerBoardingState already exists in this project and is the cleanest known marker.
        if (hitCollider.GetComponentInParent<PlayerBoardingState>() != null)
            return true;

        // Interactor2D is also a strong "this is the player/controller" marker.
        if (hitCollider.GetComponentInParent<Interactor2D>() != null)
            return true;

        // Cheap fallback for normal player tagging setups.
        if (hitCollider.CompareTag("Player") || hitCollider.GetComponentInParent<Transform>()?.CompareTag("Player") == true)
            return true;

        return false;
    }

    private bool IsColliderOnThisBoat(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return false;

        Transform boatRoot = GetBoatRoot();
        if (boatRoot == null)
            return false;

        Transform hitTransform = hitCollider.transform;

        return hitTransform == boatRoot || hitTransform.IsChildOf(boatRoot);
    }

    private bool IsExplicitDirectSupportCollider(Collider2D hitCollider)
    {
        if (hitCollider == null || directSupportColliders == null)
            return false;

        for (int i = 0; i < directSupportColliders.Length; i++)
        {
            if (directSupportColliders[i] == hitCollider)
                return true;
        }

        return false;
    }

    private static int CompareSupportHits(RaycastHit2D a, RaycastHit2D b)
    {
        return a.distance.CompareTo(b.distance);
    }

    public bool TryReserveSlot(
        ItemInstance item,
        Transform itemTransform,
        out int slotIndex,
        out Vector2 localPosition,
        out float localRotationZ)
    {
        slotIndex = -1;
        localPosition = Vector2.zero;
        localRotationZ = 0f;

        if (!CanAccept(item))
            return false;

        if (itemTransform == null)
            return false;

        string instanceId = item.InstanceId;
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        EnsureSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            SecuredSlot existing = slots[i];
            if (existing == null)
                continue;

            if (string.Equals(existing.occupiedItemInstanceId, instanceId, StringComparison.Ordinal))
            {
                CaptureLocalTransform(itemTransform, out localPosition, out localRotationZ);

                existing.localPosition = localPosition;
                existing.localRotationZ = localRotationZ;

                slotIndex = i;
                Log($"Updated existing reservation slot={i} item={instanceId}");
                return true;
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                slots[i] = new SecuredSlot();

            if (slots[i].IsOccupied)
                continue;

            CaptureLocalTransform(itemTransform, out localPosition, out localRotationZ);

            slots[i].occupiedItemInstanceId = instanceId;
            slots[i].localPosition = localPosition;
            slots[i].localRotationZ = localRotationZ;

            slotIndex = i;

            Log($"Reserved slot={i} item={instanceId}");
            return true;
        }

        return false;
    }

    public bool ReleaseSlot(string itemInstanceId)
    {
        if (string.IsNullOrWhiteSpace(itemInstanceId))
            return false;

        EnsureSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            SecuredSlot slot = slots[i];
            if (slot == null || !slot.IsOccupied)
                continue;

            if (!string.Equals(slot.occupiedItemInstanceId, itemInstanceId, StringComparison.Ordinal))
                continue;

            slot.Clear();
            Log($"Released slot={i} item={itemInstanceId}");
            return true;
        }

        return false;
    }

    public bool TryGetSlot(int slotIndex, out SecuredSlot slot)
    {
        EnsureSlots();

        slot = null;

        if (slotIndex < 0 || slotIndex >= slots.Count)
            return false;

        slot = slots[slotIndex];
        return slot != null;
    }

    private int CalculateCapacity()
    {
        if (capacityMode == SecureZoneCapacityMode.Fixed)
            return Mathf.Max(1, fixedCapacity);

        float area = CalculateColliderArea();
        int byArea = Mathf.FloorToInt(area / Mathf.Max(0.1f, areaPerSlot));

        return Mathf.Clamp(
            Mathf.Max(1, byArea),
            1,
            Mathf.Max(1, maxAreaBasedCapacity));
    }

    private float CalculateColliderArea()
    {
        CacheRefs();

        if (_zoneCollider == null)
            return 0f;

        if (_zoneCollider is BoxCollider2D box)
        {
            Vector2 lossy = box.transform.lossyScale;
            float width = Mathf.Abs(box.size.x * lossy.x);
            float height = Mathf.Abs(box.size.y * lossy.y);
            return Mathf.Max(0f, width * height);
        }

        if (_zoneCollider is CircleCollider2D circle)
        {
            Vector2 lossy = circle.transform.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
            float radius = Mathf.Abs(circle.radius * scale);
            return Mathf.PI * radius * radius;
        }

        // Good enough for Polygon/Composite/etc. If the ocean demands exact geometry math,
        // it can file a ticket with the Department of Absolutely Not Tonight.
        Bounds b = _zoneCollider.bounds;
        return Mathf.Max(0f, b.size.x * b.size.y);
    }

    private void CaptureLocalTransform(
        Transform itemTransform,
        out Vector2 localPosition,
        out float localRotationZ)
    {
        Transform root = GetBoatRoot();

        if (root == null)
            root = transform;

        localPosition = root.InverseTransformPoint(itemTransform.position);

        float itemZ = itemTransform.rotation.eulerAngles.z;
        float rootZ = root.rotation.eulerAngles.z;
        localRotationZ = Mathf.DeltaAngle(rootZ, itemZ);
    }

    public Transform GetBoatRoot()
    {
        CacheRefs();

        if (_boat != null)
            return _boat.transform;

        return transform.root;
    }

    public void EditorSetPrimarySupportSurface(Collider2D support)
    {
        if (support == null)
            return;

        if (directSupportColliders == null || directSupportColliders.Length == 0)
        {
            directSupportColliders = new[] { support };
            return;
        }

        directSupportColliders[0] = support;
    }

    private void EnsureSlots()
    {
        if (slots == null)
            slots = new List<SecuredSlot>();

        int desired = Capacity;

        while (slots.Count < desired)
            slots.Add(new SecuredSlot());

        if (slots.Count > desired)
            slots.RemoveRange(desired, slots.Count - desired);

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                slots[i] = new SecuredSlot();
        }
    }

    private void EnsureStableId()
    {
        if (!string.IsNullOrWhiteSpace(stableId))
            return;

        stableId = Guid.NewGuid().ToString("N");
    }

    private void CacheRefs()
    {
        if (_zoneCollider == null)
            _zoneCollider = GetComponent<Collider2D>();

        if (_boat == null)
            _boat = GetComponentInParent<Boat>();
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatSecureZone:{name}] {msg}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
            return;

        Color fill = zoneKind switch
        {
            SecureZoneKind.CargoBay => new Color(0.1f, 0.8f, 1f, 0.25f),
            SecureZoneKind.TieDownAnchor => new Color(1f, 0.85f, 0.1f, 0.25f),
            SecureZoneKind.DeckTieDown => new Color(1f, 0.55f, 0.1f, 0.25f),
            _ => new Color(0.8f, 0.8f, 0.8f, 0.25f)
        };

        Gizmos.color = fill;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        UnityEditor.Handles.Label(
            col.bounds.center,
            $"{zoneKind}\n" +
            $"{OccupiedCount}/{Capacity} slots\n" +
            $"Mode: {capacityMode}\n" +
            $"Area: {EstimatedArea:0.0}\n" +
            $"Stacked: {(allowStackedCargo ? "Yes" : "No")}\n" +
            $"Qx {zoneQualityMultiplier:0.00}");
    }
#endif
}