using System;
using UnityEngine;

/// <summary>
/// Authoritative two-slot ballast state for a physical diving bell.
///
/// The ballast ItemInstances live inside the diving bell payload ItemInstance's
/// own portable-container state. That gives ballast ordinary inventory identity,
/// nested mass propagation, and persistence for free instead of inventing a
/// parallel save model.
///
/// Slot 0 = left ballast.
/// Slot 1 = right ballast.
///
/// While the bell is docked, the stored payload ItemInstance (including these
/// nested ballast items) contributes mass through the existing boat/module path.
/// While deployed, DivingBellMassAggregator subtracts the nested ballast mass
/// from the payload's centered canonical mass and re-adds each ballast item at
/// its authored physical contribution point.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DivingBellOccupancy))]
public sealed class DivingBellBallastSystem : MonoBehaviour
{
    public const int LeftSlotIndex = 0;
    public const int RightSlotIndex = 1;
    public const int RequiredSlotCount = 2;

    [Header("Authority")]
    [Tooltip(
        "Single-player default is ON. Future network clients should submit ballast " +
        "intents to the authoritative host rather than mutating ItemInstances locally.")]
    [SerializeField] private bool stateAuthority = true;

    [Tooltip(
        "Global gameplay-authority policy layered on top of State Authority. " +
        "Ballast ItemInstances may mutate only when BOTH allow it.")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("References")]
    [SerializeField] private DivingBellOccupancy occupancy;
    [SerializeField] private WorldItem worldItem;

    [Header("Rules")]
    [Tooltip(
        "Ballast slots only accept ItemDefinitions carrying this category. " +
        "The diving bell ItemDefinition must ALSO allow this category in its " +
        "portable-container rules.")]
    [SerializeField]
    private ItemCategoryFlags acceptedBallastCategory =
        ItemCategoryFlags.Ballast;

    [Header("Runtime Debug")]
    [SerializeField] private TetherDeploymentModule resolvedDeployment;
    [SerializeField] private ItemInstance resolvedPayloadItem;
    [SerializeField] private bool payloadContainerReady;
    [SerializeField] private float totalBallastMass;
    [SerializeField] private string lastStatus;
    [SerializeField] private bool verboseLogging;

    private readonly DivingBellBallastSlot[] _slots =
        new DivingBellBallastSlot[RequiredSlotCount];

    private ItemInstance _subscribedPayloadItem;

    public event Action<DivingBellBallastSystem> BallastChanged;

    public bool StateAuthority =>
        stateAuthority &&
        GameplayAuthority.CanRun(gameplayAuthorityMode);
    public ItemInstance PayloadItem
    {
        get
        {
            RefreshPayloadBinding();
            return resolvedPayloadItem;
        }
    }

    public float TotalBallastMass
    {
        get
        {
            RefreshPayloadBinding();
            RefreshDebugMass();
            return totalBallastMass;
        }
    }

    public bool IsReady
    {
        get
        {
            RefreshPayloadBinding();
            return payloadContainerReady;
        }
    }

    private void Awake()
    {
        ResolveRefs();
        ResolveSlotComponents();
        RefreshPayloadBinding();
        RefreshDebugMass();
    }

    private void OnEnable()
    {
        ResolveRefs();
        ResolveSlotComponents();
        RefreshPayloadBinding();
        RefreshDebugMass();
    }

    private void LateUpdate()
    {
        // A live docked shell owns no ItemInstance. Deployment/stowing transfers
        // authority between StorageModule and WorldItem, so keep the binding synced
        // by identity rather than assuming one lifecycle owner forever.
        RefreshPayloadBinding();
    }

    private void OnDisable()
    {
        UnbindPayloadItem();
    }

    private void OnDestroy()
    {
        UnbindPayloadItem();
    }

    public void SetStateAuthority(bool authoritative)
    {
        stateAuthority = authoritative;
    }

    public void RegisterSlot(DivingBellBallastSlot slot)
    {
        if (slot == null)
            return;

        int index = slot.SlotIndex;
        if (!IsValidSlotIndex(index))
            return;

        _slots[index] = slot;
    }

    public void UnregisterSlot(DivingBellBallastSlot slot)
    {
        if (slot == null)
            return;

        int index = slot.SlotIndex;
        if (!IsValidSlotIndex(index))
            return;

        if (ReferenceEquals(_slots[index], slot))
            _slots[index] = null;
    }

    public ItemInstance GetBallast(int slotIndex)
    {
        if (!TryGetPayloadContainer(out ItemContainerState container))
            return null;

        InventorySlot slot =
            container.GetSlot(slotIndex);

        if (slot == null || slot.IsEmpty)
            return null;

        return slot.Instance;
    }

    public bool TryGetMassSample(
        int slotIndex,
        out float mass,
        out Vector2 worldCenter)
    {
        mass = 0f;
        worldCenter = transform.position;

        ItemInstance ballast =
            GetBallast(slotIndex);

        if (ballast == null ||
            ballast.Definition == null)
        {
            return false;
        }

        mass =
            Mathf.Max(
                0f,
                ballast.TotalMass);

        if (mass <= 0f ||
            float.IsNaN(mass) ||
            float.IsInfinity(mass))
        {
            mass = 0f;
            return false;
        }

        DivingBellBallastSlot slotView =
            ResolveSlot(slotIndex);

        Transform contributionPoint =
            slotView != null
                ? slotView.ContributionPoint
                : null;

        worldCenter =
            contributionPoint != null
                ? (Vector2)contributionPoint.position
                : (Vector2)transform.position;

        return
            !float.IsNaN(worldCenter.x) &&
            !float.IsNaN(worldCenter.y) &&
            !float.IsInfinity(worldCenter.x) &&
            !float.IsInfinity(worldCenter.y);
    }

    public bool CanAcceptBallast(
        int slotIndex,
        ItemInstance incoming,
        GameObject requester,
        out string reason)
    {
        reason = null;

        if (!StateAuthority)
        {
            reason = "Ballast state is not authoritative on this peer.";
            return false;
        }

        if (!IsValidSlotIndex(slotIndex))
        {
            reason = "Invalid ballast slot.";
            return false;
        }

        if (!CanRequesterUseBallast(requester, out reason))
            return false;

        if (!TryGetPayloadContainer(out ItemContainerState container))
        {
            reason = lastStatus;
            return false;
        }

        if (incoming == null ||
            incoming.Definition == null ||
            incoming.Quantity <= 0)
        {
            reason = "Missing ballast item.";
            return false;
        }

        if ((incoming.Definition.ItemCategories & acceptedBallastCategory) == 0)
        {
            reason = "That item is not ballast.";
            return false;
        }

        if (resolvedPayloadItem == null ||
            !resolvedPayloadItem.CanAcceptIntoContainer(incoming))
        {
            reason = "The diving bell does not accept that ballast item.";
            return false;
        }

        InventorySlot slot =
            container.GetSlot(slotIndex);

        if (slot == null)
        {
            reason = "Ballast slot could not be resolved.";
            return false;
        }

        if (!slot.IsEmpty)
        {
            reason = "Ballast slot is already occupied.";
            return false;
        }

        if (incoming.Quantity > 1 &&
            !incoming.CanSplit)
        {
            reason = "Ballast slot accepts one item at a time.";
            return false;
        }

        return true;
    }

    public bool TryInsertBallast(
        int slotIndex,
        ItemInstance incoming,
        GameObject requester,
        out ItemInstance remainder,
        out string message)
    {
        remainder = incoming;
        message = null;

        if (!CanAcceptBallast(
                slotIndex,
                incoming,
                requester,
                out message))
        {
            return false;
        }

        if (!TryGetPayloadContainer(out ItemContainerState container))
        {
            message = lastStatus;
            return false;
        }

        InventorySlot slot =
            container.GetSlot(slotIndex);

        if (slot == null || !slot.IsEmpty)
        {
            message = "Ballast slot is unavailable.";
            return false;
        }

        ItemInstance inserted;

        if (incoming.Quantity > 1)
        {
            inserted =
                incoming.SplitOff(1);

            if (inserted == null)
            {
                message = "Could not split one ballast item from the stack.";
                return false;
            }

            remainder = incoming;
        }
        else
        {
            inserted = incoming;
            remainder = null;
        }

        slot.Set(inserted);
        container.NotifyChanged();

        message =
            $"Loaded {inserted.Definition.DisplayName} into " +
            $"{GetSlotDisplayName(slotIndex)} ballast slot.";

        Log(message);
        return true;
    }

    public bool TryTakeBallast(
        int slotIndex,
        GameObject requester,
        out string message)
    {
        message = null;

        if (!StateAuthority)
        {
            message = "Ballast state is not authoritative on this peer.";
            return false;
        }

        if (!IsValidSlotIndex(slotIndex))
        {
            message = "Invalid ballast slot.";
            return false;
        }

        if (!CanRequesterUseBallast(requester, out message))
            return false;

        if (!TryGetPayloadContainer(out ItemContainerState container))
        {
            message = lastStatus;
            return false;
        }

        InventorySlot slot =
            container.GetSlot(slotIndex);

        ItemInstance ballast =
            slot != null && !slot.IsEmpty
                ? slot.Instance
                : null;

        if (ballast == null ||
            ballast.Definition == null)
        {
            message = "Ballast slot is empty.";
            return false;
        }

        ItemAcquisitionResolver resolver =
            FindAcquisitionResolver(requester);

        if (resolver == null ||
            !resolver.CanAcquire(ballast))
        {
            message = "No room to take that ballast.";
            return false;
        }

        slot.Clear();
        container.NotifyChanged();

        if (!resolver.TryAcquire(ballast))
        {
            // Defensive rollback. The preview above should normally make this
            // impossible, but never destroy a physical load because an inventory
            // mutation changed between validation and commit.
            slot.Set(ballast);
            container.NotifyChanged();

            message = "Could not take ballast.";
            return false;
        }

        message =
            $"Removed {ballast.Definition.DisplayName} from " +
            $"{GetSlotDisplayName(slotIndex)} ballast slot.";

        Log(message);
        return true;
    }

    /// <summary>
    /// Emergency jettison seam. The item leaves the bell payload ItemInstance and
    /// becomes an ordinary unowned WorldItem at the slot's authored external dump
    /// point. Passing actor=null to WorldItemDropUtility intentionally prevents the
    /// dropped ballast from immediately inheriting diving-bell containment again.
    /// </summary>
    public bool TryDumpBallast(
        int slotIndex,
        GameObject requester,
        out WorldItem dropped,
        out string message)
    {
        dropped = null;
        message = null;

        if (!StateAuthority)
        {
            message = "Ballast state is not authoritative on this peer.";
            return false;
        }

        if (!IsValidSlotIndex(slotIndex))
        {
            message = "Invalid ballast slot.";
            return false;
        }

        if (requester != null &&
            !CanRequesterUseBallast(requester, out message))
        {
            return false;
        }

        if (!TryGetPayloadContainer(out ItemContainerState container))
        {
            message = lastStatus;
            return false;
        }

        InventorySlot slot =
            container.GetSlot(slotIndex);

        ItemInstance ballast =
            slot != null && !slot.IsEmpty
                ? slot.Instance
                : null;

        if (ballast == null ||
            ballast.Definition == null)
        {
            message = "Ballast slot is empty.";
            return false;
        }

        if (!ballast.Definition.Droppable ||
            ballast.Definition.WorldPrefab == null)
        {
            message = "That ballast cannot be dumped into the world.";
            return false;
        }

        DivingBellBallastSlot slotView =
            ResolveSlot(slotIndex);

        Transform dumpPoint =
            slotView != null
                ? slotView.DumpPoint
                : null;

        Vector3 spawnPosition =
            dumpPoint != null
                ? dumpPoint.position
                : transform.position;

        Quaternion spawnRotation =
            dumpPoint != null
                ? dumpPoint.rotation
                : Quaternion.identity;

        // Transfer authority away from the ballast container before spawning.
        // Roll back if the world representation cannot be created.
        slot.Clear();
        container.NotifyChanged();

        if (!WorldItemDropUtility.TryDrop(
                ballast,
                spawnPosition,
                actor: null,
                out dropped) ||
            dropped == null)
        {
            slot.Set(ballast);
            container.NotifyChanged();

            message = "Could not dump ballast.";
            return false;
        }

        dropped.transform.rotation =
            spawnRotation;

        Rigidbody2D droppedBody =
            dropped.GetComponent<Rigidbody2D>();

        Rigidbody2D bellBody =
            occupancy != null &&
            occupancy.Payload != null
                ? occupancy.Payload.Rigidbody
                : GetComponent<Rigidbody2D>();

        if (droppedBody != null &&
            bellBody != null)
        {
            droppedBody.linearVelocity =
                bellBody.linearVelocity;

            droppedBody.angularVelocity =
                bellBody.angularVelocity;
        }

        message =
            $"Dumped {ballast.Definition.DisplayName} from " +
            $"{GetSlotDisplayName(slotIndex)} ballast slot.";

        Log(message);
        return true;
    }

    public int TryDumpAllBallast(
        GameObject requester)
    {
        int dumpedCount = 0;

        if (TryDumpBallast(
                LeftSlotIndex,
                requester,
                out _,
                out _))
        {
            dumpedCount++;
        }

        if (TryDumpBallast(
                RightSlotIndex,
                requester,
                out _,
                out _))
        {
            dumpedCount++;
        }

        return dumpedCount;
    }

    public bool CanRequesterUseBallast(
        GameObject requester,
        out string reason)
    {
        reason = null;

        ResolveRefs();

        if (requester == null)
        {
            reason = "Missing ballast requester.";
            return false;
        }

        if (occupancy != null &&
            occupancy.Contains(requester))
        {
            return true;
        }

        if (occupancy == null ||
            !occupancy.IsDocked)
        {
            reason = "Board the diving bell to use its ballast while deployed.";
            return false;
        }

        Boat dockedBoat =
            ResolveDockedBoat();

        if (dockedBoat == null)
        {
            reason = "Could not resolve the boat supporting this diving bell.";
            return false;
        }

        PlayerBoardingState boarding =
            requester.GetComponent<PlayerBoardingState>() ??
            requester.GetComponentInParent<PlayerBoardingState>() ??
            requester.GetComponentInChildren<PlayerBoardingState>(true);

        if (boarding == null ||
            !boarding.IsBoarded ||
            boarding.CurrentBoatRoot == null)
        {
            reason = "Board the boat to use docked ballast slots.";
            return false;
        }

        Boat requesterBoat =
            boarding.CurrentBoatRoot.GetComponent<Boat>() ??
            boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

        if (requesterBoat == null)
        {
            reason = "Could not resolve the requester's boat.";
            return false;
        }

        bool sameBoat =
            (!string.IsNullOrWhiteSpace(dockedBoat.BoatInstanceId) &&
             !string.IsNullOrWhiteSpace(requesterBoat.BoatInstanceId))
                ? dockedBoat.BoatInstanceId == requesterBoat.BoatInstanceId
                : ReferenceEquals(dockedBoat, requesterBoat);

        if (!sameBoat)
        {
            reason = "That diving bell belongs to another boat.";
            return false;
        }

        return true;
    }

    private bool TryGetPayloadContainer(
        out ItemContainerState container)
    {
        container = null;

        RefreshPayloadBinding();

        if (resolvedPayloadItem == null ||
            resolvedPayloadItem.Definition == null)
        {
            payloadContainerReady = false;
            lastStatus = "Diving bell payload ItemInstance is not currently resolved.";
            return false;
        }

        resolvedPayloadItem.EnsureContainerStateMatchesDefinition();

        if (!resolvedPayloadItem.IsContainer ||
            resolvedPayloadItem.ContainerState == null)
        {
            payloadContainerReady = false;
            lastStatus =
                "Diving bell ItemDefinition must be configured as a portable container.";
            return false;
        }

        if (resolvedPayloadItem.ContainerState.SlotCount < RequiredSlotCount)
        {
            payloadContainerReady = false;
            lastStatus =
                $"Diving bell ItemDefinition requires at least {RequiredSlotCount} portable-container slots.";
            return false;
        }

        container =
            resolvedPayloadItem.ContainerState;

        payloadContainerReady = true;
        lastStatus = "Ready";
        return true;
    }

    private void RefreshPayloadBinding()
    {
        ResolveRefs();

        ItemInstance next =
            ResolvePayloadItem();

        if (ReferenceEquals(
                next,
                resolvedPayloadItem) &&
            ReferenceEquals(
                next,
                _subscribedPayloadItem))
        {
            return;
        }

        UnbindPayloadItem();

        resolvedPayloadItem =
            next;

        _subscribedPayloadItem =
            next;

        if (_subscribedPayloadItem != null)
        {
            _subscribedPayloadItem.EnsureContainerStateMatchesDefinition();
            _subscribedPayloadItem.Changed += HandlePayloadItemChanged;
        }

        RefreshContainerReadyState();
        RefreshDebugMass();
        BallastChanged?.Invoke(this);
    }

    private void UnbindPayloadItem()
    {
        if (_subscribedPayloadItem != null)
        {
            _subscribedPayloadItem.Changed -=
                HandlePayloadItemChanged;
        }

        _subscribedPayloadItem = null;
    }

    private void HandlePayloadItemChanged()
    {
        RefreshContainerReadyState();
        RefreshDebugMass();
        BallastChanged?.Invoke(this);
    }

    private void RefreshContainerReadyState()
    {
        if (resolvedPayloadItem == null ||
            resolvedPayloadItem.Definition == null)
        {
            payloadContainerReady = false;
            lastStatus = "No payload ItemInstance.";
            return;
        }

        resolvedPayloadItem.EnsureContainerStateMatchesDefinition();

        payloadContainerReady =
            resolvedPayloadItem.IsContainer &&
            resolvedPayloadItem.ContainerState != null &&
            resolvedPayloadItem.ContainerState.SlotCount >= RequiredSlotCount;

        lastStatus =
            payloadContainerReady
                ? "Ready"
                : "Bell ItemDefinition is not configured with two ballast container slots.";
    }

    private void RefreshDebugMass()
    {
        float total = 0f;

        for (int i = 0;
             i < RequiredSlotCount;
             i++)
        {
            ItemInstance item =
                GetBallastWithoutRefreshing(i);

            if (item == null)
                continue;

            float mass =
                item.TotalMass;

            if (mass > 0f &&
                !float.IsNaN(mass) &&
                !float.IsInfinity(mass))
            {
                total += mass;
            }
        }

        totalBallastMass =
            Mathf.Max(0f, total);
    }

    private ItemInstance GetBallastWithoutRefreshing(
        int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex) ||
            resolvedPayloadItem == null ||
            resolvedPayloadItem.ContainerState == null ||
            resolvedPayloadItem.ContainerState.SlotCount <= slotIndex)
        {
            return null;
        }

        InventorySlot slot =
            resolvedPayloadItem.ContainerState.GetSlot(slotIndex);

        return
            slot != null && !slot.IsEmpty
                ? slot.Instance
                : null;
    }

    private ItemInstance ResolvePayloadItem()
    {
        if (worldItem != null &&
            worldItem.Instance != null)
        {
            return worldItem.Instance;
        }

        if (!DeploymentRepresentsThisPhysicalBell(
                resolvedDeployment))
        {
            resolvedDeployment =
                ResolveDeploymentForThisPhysicalBell();
        }

        if (resolvedDeployment == null)
            return null;

        if (worldItem != null &&
            ReferenceEquals(
                resolvedDeployment.DockedPhysicalWorldItem,
                worldItem))
        {
            return resolvedDeployment.StoredPayload;
        }

        if (worldItem != null &&
            ReferenceEquals(
                resolvedDeployment.DeployedWorldItem,
                worldItem))
        {
            return
                resolvedDeployment.ReservedPayloadItem ??
                worldItem.Instance;
        }

        return null;
    }

    private TetherDeploymentModule ResolveDeploymentForThisPhysicalBell()
    {
        if (worldItem == null)
            return null;

        TetherDeploymentModule[] deployments =
            FindObjectsByType<TetherDeploymentModule>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        TetherDeploymentModule match = null;

        for (int i = 0;
             i < deployments.Length;
             i++)
        {
            TetherDeploymentModule candidate =
                deployments[i];

            if (!DeploymentRepresentsThisPhysicalBell(candidate))
                continue;

            if (match != null &&
                !ReferenceEquals(match, candidate))
            {
                Debug.LogWarning(
                    $"[DivingBellBallastSystem:{name}] Multiple TetherDeploymentModules " +
                    "claim this physical bell. Ballast authority is ambiguous.",
                    this);

                return null;
            }

            match = candidate;
        }

        return match;
    }

    private bool DeploymentRepresentsThisPhysicalBell(
        TetherDeploymentModule deployment)
    {
        if (deployment == null ||
            worldItem == null)
        {
            return false;
        }

        return
            ReferenceEquals(
                deployment.DockedPhysicalWorldItem,
                worldItem) ||
            ReferenceEquals(
                deployment.DeployedWorldItem,
                worldItem);
    }

    private Boat ResolveDockedBoat()
    {
        if (occupancy == null ||
            !occupancy.IsDocked)
        {
            return null;
        }

        TetherPayloadDock dock =
            occupancy.CurrentDock;

        if (dock == null)
            return null;

        return
            dock.GetComponentInParent<Boat>();
    }

    private DivingBellBallastSlot ResolveSlot(
        int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return null;

        DivingBellBallastSlot slot =
            _slots[slotIndex];

        if (slot != null)
            return slot;

        ResolveSlotComponents();
        return _slots[slotIndex];
    }

    private void ResolveSlotComponents()
    {
        DivingBellBallastSlot[] slots =
            GetComponentsInChildren<DivingBellBallastSlot>(true);

        for (int i = 0;
             i < slots.Length;
             i++)
        {
            DivingBellBallastSlot slot =
                slots[i];

            if (slot == null ||
                !IsValidSlotIndex(slot.SlotIndex))
            {
                continue;
            }

            _slots[slot.SlotIndex] = slot;
        }
    }

    private void ResolveRefs()
    {
        if (occupancy == null)
        {
            occupancy =
                GetComponent<DivingBellOccupancy>() ??
                GetComponentInParent<DivingBellOccupancy>() ??
                GetComponentInChildren<DivingBellOccupancy>(true);
        }

        if (worldItem == null)
        {
            worldItem =
                GetComponent<WorldItem>() ??
                GetComponentInParent<WorldItem>() ??
                GetComponentInChildren<WorldItem>(true);
        }
    }

    private static ItemAcquisitionResolver FindAcquisitionResolver(
        GameObject actor)
    {
        if (actor == null)
            return null;

        ItemAcquisitionResolver resolver =
            actor.GetComponent<ItemAcquisitionResolver>();

        if (resolver != null)
            return resolver;

        resolver =
            actor.GetComponentInChildren<ItemAcquisitionResolver>(true);

        if (resolver != null)
            return resolver;

        return
            actor.GetComponentInParent<ItemAcquisitionResolver>();
    }

    private static bool IsValidSlotIndex(
        int slotIndex)
    {
        return
            slotIndex == LeftSlotIndex ||
            slotIndex == RightSlotIndex;
    }

    private static string GetSlotDisplayName(
        int slotIndex)
    {
        return
            slotIndex == LeftSlotIndex
                ? "left"
                : "right";
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[DivingBellBallastSystem:{name}] {message}",
            this);
    }
}
