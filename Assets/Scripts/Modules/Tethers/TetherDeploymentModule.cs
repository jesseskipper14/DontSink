using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(InstalledModule))]
[RequireComponent(typeof(StorageModule))]
public sealed class TetherDeploymentModule : MonoBehaviour, IInstalledModuleLifecycle
{
    [Header("Payload Storage")]
    [SerializeField] private StorageModule storageModule;
    [SerializeField, Min(0)] private int payloadSlotIndex = 0;

    [Header("Rigging")]
    [Tooltip("Where the stored payload becomes a physical WorldItem when deployed.")]
    [SerializeField] private Transform payloadHangPoint;

    [Tooltip("Where the visible/simulated tether will leave this installed module.")]
    [SerializeField] private Transform tetherExitPoint;

    [Header("Live Stored Payload")]
    [Tooltip(
        "OPT-IN. When enabled, an item sitting in the payload storage slot also has its " +
        "actual WorldPrefab instantiated and physically docked while stowed. " +
        "The ItemInstance remains authoritative in StorageModule until deployment, so " +
        "existing inventory/save semantics remain intact. Use this for boardable payloads " +
        "such as a diving bell or cage.")]
    [SerializeField] private bool keepStoredPayloadPhysical = false;

    [Tooltip(
        "Dock/lock authority for the live stored payload. Required only when " +
        "Keep Stored Payload Physical is enabled.")]
    [SerializeField] private TetherPayloadDock payloadDock;

    [Header("Tether")]
    [SerializeField] private TetherConstraint2D tetherConstraint;

    [Tooltip("Temporary starting tether length until WinchModule owns payout/retrieval.")]
    [SerializeField, Min(0.05f)] private float initialDeployedLength = 1f;

    [Header("Persistence Restore")]
    [Tooltip(
        "Extra clearance left before the restored payload's collider reaches terrain. " +
        "Restore uses a Rigidbody2D shape cast from PayloadHangPoint toward the saved pose, " +
        "so a saved anchor cannot respawn inside the seabed.")]
    [SerializeField, Min(0f)] private float restoreCollisionSkinMeters = 0.02f;

    [Tooltip(
        "How long a restored deployed tether ignores breaking-load accumulation while physics settles. " +
        "The tether joint remains physically active during this window.")]
    [SerializeField, Min(0f)] private float restoreTetherBreakProtectionSeconds = 0.5f;

    [Header("Stored Payload Visual")]
    [Tooltip("Optional dumb visual used while the payload ItemInstance is stored. " +
             "If left empty, one is created automatically under PayloadHangPoint at runtime.")]
    [SerializeField] private SpriteRenderer storedPayloadVisualRenderer;

    [Tooltip("Extra local scale applied to the stored visual.")]
    [SerializeField, Min(0.01f)] private float storedPayloadVisualScale = 1f;

    [Tooltip("Additional sorting-order offset beyond the default Anchor Hole/module renderer order + 1.")]
    [SerializeField] private int storedPayloadSortingOrderOffset = 0;

    [Header("Runtime")]
    [SerializeField] private TetherDeploymentState deploymentState = TetherDeploymentState.Stowed;
    [SerializeField] private WorldItem deployedWorldItem;

    [Tooltip(
        "Runtime physical shell shown while an opt-in live payload is stowed. " +
        "Its WorldItem intentionally owns NO ItemInstance until deployment.")]
    [SerializeField] private WorldItem dockedPhysicalWorldItem;

    private ItemInstance _deployedItemInstance;
    private ItemInstance _dockedPhysicalSourceItem;
    private bool _syncingStoredPhysicalPayload;

    // Smooth-dock recall is asynchronous. While this is true the deployed
    // WorldItem + ItemInstance remain authoritative/reserved until the specific
    // payload actually reaches Docked state.
    [SerializeField] private bool physicalRecallCapturePending;
    [SerializeField] private float pendingPhysicalRecallPreviousLength;

    private ItemContainerState _subscribedContainer;

    public event Action<TetherDeploymentModule> PayloadChanged;
    public event Action<TetherDeploymentModule, TetherDeploymentState> DeploymentStateChanged;

    public TetherDeploymentState DeploymentState => deploymentState;
    public bool IsStowed => deploymentState == TetherDeploymentState.Stowed;
    public bool IsBottomed =>
        deploymentState == TetherDeploymentState.Bottomed ||
        deploymentState == TetherDeploymentState.Holding;
    public bool IsHolding => deploymentState == TetherDeploymentState.Holding;
    public StorageModule StorageModule => storageModule;
    public int PayloadSlotIndex => Mathf.Max(0, payloadSlotIndex);

    public Transform PayloadHangPoint =>
        payloadHangPoint != null
            ? payloadHangPoint
            : transform;

    public Transform TetherExitPoint =>
        tetherExitPoint != null
            ? tetherExitPoint
            : PayloadHangPoint;

    public WorldItem DeployedWorldItem => deployedWorldItem;
    public WorldItem DockedPhysicalWorldItem => dockedPhysicalWorldItem;
    public TetherConstraint2D TetherConstraint => tetherConstraint;
    public TetherPayloadDock PayloadDock => payloadDock;
    public bool KeepStoredPayloadPhysical => keepStoredPayloadPhysical;
    public bool HasDockedPhysicalPayload => dockedPhysicalWorldItem != null;
    public bool IsPhysicalRecallCapturePending => physicalRecallCapturePending;

    public TetherPayload DeployedPayload
    {
        get
        {
            if (deployedWorldItem == null)
                return null;

            return deployedWorldItem.GetComponent<TetherPayload>();
        }
    }

    public bool HasDeployedPayload =>
        deployedWorldItem != null;

    public bool HasStoredPayload =>
        TryGetStoredPayload(out _);

    public ItemInstance StoredPayload
    {
        get
        {
            TryGetStoredPayload(out ItemInstance item);
            return item;
        }
    }

    /// <summary>
    /// The payload ItemInstance currently represented by the deployed WorldItem.
    /// It is NOT in StorageModule while deployed, but its configured payload slot
    /// remains logically reserved for it.
    /// </summary>
    public ItemInstance ReservedPayloadItem =>
        _deployedItemInstance;

    public bool IsPayloadSlotReserved(int slotIndex)
    {
        return
            slotIndex == PayloadSlotIndex &&
            _deployedItemInstance != null;
    }

    public bool TryGetReservedPayloadForSlot(
        int slotIndex,
        out ItemInstance item)
    {
        item = null;

        if (!IsPayloadSlotReserved(slotIndex))
            return false;

        item = _deployedItemInstance;
        return item != null;
    }

    private void Awake()
    {
        CacheRefs();
        BindContainer();
        SyncStoredPhysicalPayload();
        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
    }

    private void OnEnable()
    {
        CacheRefs();
        BindContainer();
        SyncStoredPhysicalPayload();
        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
    }

    private void Update()
    {
        CacheRefs();

        if (storageModule == null)
            return;

        storageModule.EnsureContainer();

        ItemContainerState currentContainer =
            storageModule.ContainerState;

        // Save/load may replace the ItemContainerState object after this
        // component has already subscribed to the previous instance.
        if (!ReferenceEquals(
                _subscribedContainer,
                currentContainer))
        {
            BindContainer();
            SyncStoredPhysicalPayload();
            RefreshStoredPayloadVisual();
        }
    }

    private void FixedUpdate()
    {
        ReconcilePendingPhysicalRecall();
        RefreshDeploymentState();
    }

    private void OnDisable()
    {
        UnbindContainer();
        SuppressStoredPayloadVisual();
    }

    public void OnInstalled(Hardpoint hardpoint)
    {
        CacheRefs();
        BindContainer();
        SyncStoredPhysicalPayload();
        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
        PayloadChanged?.Invoke(this);
    }

    public void OnRemoved()
    {
        UnbindContainer();
        CancelPendingPhysicalRecall(restoreTetherIfPossible: false);
        SetDeploymentState(TetherDeploymentState.Stowed);
        SuppressStoredPayloadVisual();
    }

    /// <summary>
    /// Immediately reconciles the runtime stored-payload shell with the current
    /// StorageModule container state.
    ///
    /// Persistence can replace StorageModule.ContainerState during BoatSpawner.Start.
    /// This component may still be subscribed to the previous container until its
    /// first Update, which is too late for loose BellItems restored later in that
    /// same Start call. Calling this after storage restore guarantees a stowed
    /// physical payload shell exists before dependent loose-item restoration runs.
    /// </summary>
    public void RefreshStoredPayloadAfterStorageRestore()
    {
        CacheRefs();
        BindContainer();
        SyncStoredPhysicalPayload();
        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
    }

    public bool TryGetStoredPayload(out ItemInstance item)
    {
        item = null;

        CacheRefs();

        if (!TryGetPayloadSlot(out InventorySlot slot))
            return false;

        if (slot.IsEmpty || slot.Instance == null)
            return false;

        item = slot.Instance;
        return true;
    }

    /// <summary>
    /// Removes the ItemInstance from the configured StorageModule slot and
    /// instantiates that item's WorldPrefab at PayloadHangPoint.
    ///
    /// The spawned prefab must contain TetherPayload on its WorldItem root.
    /// The SAME ItemInstance becomes authoritative on the spawned WorldItem.
    /// </summary>
    public bool TryDeployStoredPayload(out TetherPayload payload)
    {
        payload = null;

        if (keepStoredPayloadPhysical)
        {
            return TryDeployStoredPhysicalPayload(
                out payload);
        }

        if (deployedWorldItem != null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot deploy: a payload is already deployed.",
                this);

            return false;
        }

        if (!TryGetStoredPayload(out ItemInstance item) ||
            item == null ||
            item.Definition == null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot deploy: payload slot {PayloadSlotIndex} is empty.",
                this);

            return false;
        }

        WorldItem worldPrefab = item.Definition.WorldPrefab;

        if (worldPrefab == null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot deploy '{item.Definition.DisplayName}': " +
                "its ItemDefinition has no WorldPrefab.",
                this);

            return false;
        }

        TetherPayload prefabPayload =
            worldPrefab.GetComponent<TetherPayload>();

        if (prefabPayload == null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot deploy '{item.Definition.DisplayName}': " +
                "its WorldPrefab has no TetherPayload component on the root.",
                this);

            return false;
        }

        if (!TryGetPayloadSlot(out InventorySlot slot))
            return false;

        // Transfer authority away from StorageModule first.
        slot.Clear();
        storageModule.ContainerState.NotifyChanged();

        Transform spawnPoint = PayloadHangPoint;

        WorldItem spawned = Instantiate(
            worldPrefab,
            spawnPoint.position,
            spawnPoint.rotation);

        if (spawned == null)
        {
            // Extremely defensive rollback. Unity would have to be having a day.
            slot.Set(item);
            storageModule.ContainerState.NotifyChanged();
            return false;
        }

        spawned.Initialize(item);

        payload = spawned.GetComponent<TetherPayload>();

        if (payload == null)
        {
            // Prefab validation above should make this impossible, but do not
            // eat the player's item if a runtime prefab somehow differs.
            Destroy(spawned.gameObject);

            slot.Set(item);
            storageModule.ContainerState.NotifyChanged();

            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Spawned payload lost its TetherPayload component. " +
                "Deployment rolled back.",
                this);

            return false;
        }

        CacheRefs();

        if (tetherConstraint == null)
        {
            Destroy(spawned.gameObject);

            slot.Set(item);
            storageModule.ContainerState.NotifyChanged();

            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Cannot deploy: no TetherConstraint2D is assigned/present. " +
                "Deployment rolled back.",
                this);

            return false;
        }

        float initialLength =
            Mathf.Max(
                0.05f,
                initialDeployedLength);

        Boat owningBoat =
            GetComponentInParent<Boat>();

        Rigidbody2D boatBody =
            owningBoat != null
                ? owningBoat.GetComponent<Rigidbody2D>()
                : null;

        if (!payload.SetTetherCollisionLayerActive(true))
        {
            Destroy(spawned.gameObject);

            slot.Set(item);
            storageModule.ContainerState.NotifyChanged();

            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Could not apply the TetherPayload physics layer. " +
                "Deployment rolled back.",
                this);

            return false;
        }

        tetherConstraint.Bind(
            TetherExitPoint,
            boatBody,
            payload,
            initialLength,
            0f,
            0f);

        if (!tetherConstraint.IsAttached)
        {
            payload.SetTetherCollisionLayerActive(false);

            Destroy(spawned.gameObject);

            slot.Set(item);
            storageModule.ContainerState.NotifyChanged();

            Debug.LogError(
                $"[TetherDeploymentModule:{name}] TetherConstraint2D failed to attach. " +
                "Deployment rolled back.",
                this);

            return false;
        }

        deployedWorldItem = spawned;
        _deployedItemInstance = item;

        RefreshStoredPayloadVisual();

        // The raw storage slot is empty while deployed, but its binding now
        // displays the reserved payload ItemInstance. Notify again after the
        // reservation exists so an already-open storage UI refreshes immediately.
        storageModule.ContainerState.NotifyChanged();

        RefreshDeploymentState();
        PayloadChanged?.Invoke(this);

        return true;
    }

    /// <summary>
    /// Temporary iteration helper. Detaches the currently deployed payload,
    /// destroys its WorldItem representation, and returns the SAME ItemInstance
    /// to the configured storage slot.
    ///
    /// This intentionally refuses to run if the slot is occupied or if the
    /// deployed WorldItem no longer exists, to avoid duplicating items.
    /// </summary>
    public bool TryRecallDeployedPayload()
    {
        if (keepStoredPayloadPhysical)
        {
            return TryRecallDeployedPhysicalPayload();
        }

        if (deployedWorldItem == null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot recall: no deployed WorldItem is currently tracked.",
                this);

            return false;
        }

        if (_deployedItemInstance == null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot recall: deployed ItemInstance is not tracked.",
                this);

            return false;
        }

        if (!TryGetPayloadSlot(out InventorySlot slot))
            return false;

        if (!slot.IsEmpty)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot recall: payload storage slot {PayloadSlotIndex} is occupied.",
                this);

            return false;
        }

        if (tetherConstraint != null)
            tetherConstraint.Detach();

        TetherPayload deployedPayload =
            deployedWorldItem.GetComponent<TetherPayload>();

        if (deployedPayload != null)
        {
            deployedPayload.SetTetherCollisionLayerActive(
                false);
        }

        WorldItem worldItemToDestroy =
            deployedWorldItem;

        deployedWorldItem = null;

        if (worldItemToDestroy != null)
        {
            EmergencyEjectDivingBellOccupantsBeforeTeardown(
                worldItemToDestroy,
                "Tether payload was recalled and is being destroyed.");

            worldItemToDestroy.gameObject.SetActive(false);
            Destroy(worldItemToDestroy.gameObject);
        }

        slot.Set(_deployedItemInstance);
        _deployedItemInstance = null;

        RefreshStoredPayloadVisual();

        storageModule.ContainerState.NotifyChanged();
        SetDeploymentState(TetherDeploymentState.Stowed);
        PayloadChanged?.Invoke(this);

        return true;
    }

    /// <summary>
    /// Keeps the actual payload prefab alive while stowed without moving
    /// ItemInstance authority out of StorageModule.
    ///
    /// Stowed:
    /// - ItemInstance stays in the storage slot.
    /// - WorldPrefab exists as a real physics/gameplay shell.
    /// - WorldItem.Instance is intentionally null, so normal pickup cannot steal it.
    /// - TetherPayloadDock locks the shell to the boat.
    ///
    /// Deployed:
    /// - SAME shell is released.
    /// - SAME ItemInstance transfers from storage into that WorldItem.
    /// </summary>
    private void SyncStoredPhysicalPayload()
    {
        if (!keepStoredPayloadPhysical ||
            _syncingStoredPhysicalPayload ||
            deployedWorldItem != null)
        {
            return;
        }

        CacheRefs();

        if (payloadDock == null)
        {
            if (TryGetStoredPayload(out ItemInstance missingDockItem) &&
                missingDockItem != null)
            {
                Debug.LogError(
                    $"[TetherDeploymentModule:{name}] Keep Stored Payload Physical is enabled, " +
                    "but no TetherPayloadDock component is assigned/present.",
                    this);
            }

            return;
        }

        if (!TryGetStoredPayload(out ItemInstance storedItem) ||
            storedItem == null ||
            storedItem.Definition == null)
        {
            DestroyDockedPhysicalShell();
            return;
        }

        if (dockedPhysicalWorldItem != null &&
            ReferenceEquals(
                _dockedPhysicalSourceItem,
                storedItem))
        {
            TetherPayload existingPayload =
                dockedPhysicalWorldItem.GetComponent<TetherPayload>();

            if (existingPayload != null &&
                !payloadDock.IsDocked(existingPayload))
            {
                payloadDock.TryDock(
                    existingPayload,
                    PayloadHangPoint);
            }

            return;
        }

        DestroyDockedPhysicalShell();

        WorldItem worldPrefab =
            storedItem.Definition.WorldPrefab;

        if (worldPrefab == null)
        {
            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Cannot create live stored payload " +
                $"'{storedItem.Definition.DisplayName}': ItemDefinition has no WorldPrefab.",
                this);

            return;
        }

        TetherPayload prefabPayload =
            worldPrefab.GetComponent<TetherPayload>();

        if (prefabPayload == null)
        {
            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Cannot create live stored payload " +
                $"'{storedItem.Definition.DisplayName}': WorldPrefab needs TetherPayload on the root.",
                this);

            return;
        }

        Transform spawnPoint =
            PayloadHangPoint;

        WorldItem spawned =
            Instantiate(
                worldPrefab,
                spawnPoint.position,
                spawnPoint.rotation);

        if (spawned == null)
            return;

        // Critical: while STOWED, StorageModule still owns the ItemInstance.
        // The physical shell exists for boarding/collision/gameplay only.
        spawned.Initialize(null);

        TetherPayload payload =
            spawned.GetComponent<TetherPayload>();

        if (payload == null ||
            !payloadDock.TryDock(
                payload,
                PayloadHangPoint))
        {
            Destroy(
                spawned.gameObject);

            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Could not dock live stored payload " +
                $"'{storedItem.Definition.DisplayName}'.",
                this);

            return;
        }

        dockedPhysicalWorldItem =
            spawned;

        _dockedPhysicalSourceItem =
            storedItem;

        SuppressStoredPayloadVisual();

        PayloadChanged?.Invoke(
            this);
    }

    private void DestroyDockedPhysicalShell()
    {
        if (dockedPhysicalWorldItem == null)
        {
            _dockedPhysicalSourceItem =
                null;

            return;
        }

        TetherPayload payload =
            dockedPhysicalWorldItem.GetComponent<TetherPayload>();

        if (payloadDock != null &&
            payload != null &&
            payloadDock.GetDockState(payload) !=
                TetherDockState.Free)
        {
            // A stored shell may still be in the final Capturing phase. Release
            // either claimed state before destroying it so the dock cannot retain
            // a stale payload reference.
            payloadDock.TryRelease(
                payload);
        }

        WorldItem doomed =
            dockedPhysicalWorldItem;

        dockedPhysicalWorldItem =
            null;

        _dockedPhysicalSourceItem =
            null;

        if (doomed != null)
        {
            EmergencyEjectDivingBellOccupantsBeforeTeardown(
                doomed,
                "Stored physical tether payload is being removed.");

            doomed.gameObject.SetActive(
                false);

            Destroy(
                doomed.gameObject);
        }
    }

    private static void EmergencyEjectDivingBellOccupantsBeforeTeardown(
        WorldItem worldItem,
        string reason)
    {
        if (worldItem == null)
            return;

        DivingBellOccupancy occupancy =
            worldItem.GetComponent<DivingBellOccupancy>() ??
            worldItem.GetComponentInChildren<DivingBellOccupancy>(
                true);

        if (occupancy == null ||
            !occupancy.HasOccupants)
        {
            return;
        }

        occupancy.EmergencyEjectAllOccupants(
            reason);
    }

    private bool TryDeployStoredPhysicalPayload(
        out TetherPayload payload,
        bool allowCapturingDockClaim = false)
    {
        payload =
            null;

        if (deployedWorldItem != null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot deploy: a payload is already deployed.",
                this);

            return false;
        }

        SyncStoredPhysicalPayload();

        if (!TryGetStoredPayload(out ItemInstance item) ||
            item == null ||
            item.Definition == null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot deploy: payload slot {PayloadSlotIndex} is empty.",
                this);

            return false;
        }

        if (dockedPhysicalWorldItem == null ||
            !ReferenceEquals(
                _dockedPhysicalSourceItem,
                item))
        {
            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Cannot deploy '{item.Definition.DisplayName}': " +
                "its live stored physical shell is missing or out of sync.",
                this);

            return false;
        }

        payload =
            dockedPhysicalWorldItem.GetComponent<TetherPayload>();

        bool shellIsDocked =
            payload != null &&
            payloadDock != null &&
            payloadDock.IsDocked(payload);

        bool shellIsCapturingForRestore =
            allowCapturingDockClaim &&
            payload != null &&
            payloadDock != null &&
            payloadDock.IsCapturing(payload);

        if (payload == null ||
            payloadDock == null ||
            (!shellIsDocked &&
             !shellIsCapturingForRestore))
        {
            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Cannot deploy '{item.Definition.DisplayName}': " +
                "its physical shell is neither docked nor an allowed restore-time capture.",
                this);

            payload =
                null;

            return false;
        }

        CacheRefs();

        if (tetherConstraint == null)
        {
            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Cannot deploy: no TetherConstraint2D is assigned/present.",
                this);

            payload =
                null;

            return false;
        }

        Boat owningBoat =
            GetComponentInParent<Boat>();

        Rigidbody2D boatBody =
            owningBoat != null
                ? owningBoat.GetComponent<Rigidbody2D>()
                : null;

        float initialLength =
            Mathf.Max(
                0.05f,
                initialDeployedLength);

        if (payload.TetherAnchor != null &&
            TetherExitPoint != null)
        {
            float currentEndpointDistance =
                Vector2.Distance(
                    TetherExitPoint.position,
                    payload.TetherAnchor.position);

            // A composite payload may have its root well below the cable eye.
            // Never release it into an instantly-overstretched 5 cm tether.
            initialLength =
                Mathf.Max(
                    initialLength,
                    currentEndpointDistance +
                    0.05f);
        }

        if (!payloadDock.TryRelease(
                payload))
        {
            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Could not release docked payload.",
                this);

            payload =
                null;

            return false;
        }

        if (!payload.SetTetherCollisionLayerActive(
                true))
        {
            payloadDock.TryDock(
                payload,
                PayloadHangPoint);

            payload =
                null;

            return false;
        }

        tetherConstraint.Bind(
            TetherExitPoint,
            boatBody,
            payload,
            initialLength,
            0f,
            0f);

        if (!tetherConstraint.IsAttached)
        {
            payload.SetTetherCollisionLayerActive(
                false);

            payloadDock.TryDock(
                payload,
                PayloadHangPoint);

            Debug.LogError(
                $"[TetherDeploymentModule:{name}] TetherConstraint2D failed to attach. " +
                "Physical payload was returned to its dock.",
                this);

            payload =
                null;

            return false;
        }

        if (!TryGetPayloadSlot(
                out InventorySlot slot))
        {
            tetherConstraint.Detach();

            payload.SetTetherCollisionLayerActive(
                false);

            payloadDock.TryDock(
                payload,
                PayloadHangPoint);

            payload =
                null;

            return false;
        }

        WorldItem liveWorldItem =
            dockedPhysicalWorldItem;

        deployedWorldItem =
            liveWorldItem;

        _deployedItemInstance =
            item;

        dockedPhysicalWorldItem =
            null;

        _dockedPhysicalSourceItem =
            null;

        // Authority transfers only AFTER the same physical shell has safely
        // left the dock and acquired its tether.
        deployedWorldItem.Initialize(
            item);

        _syncingStoredPhysicalPayload =
            true;

        try
        {
            slot.Clear();
            storageModule.ContainerState.NotifyChanged();
        }
        finally
        {
            _syncingStoredPhysicalPayload =
                false;
        }

        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
        PayloadChanged?.Invoke(
            this);

        return true;
    }

    private bool TryRecallDeployedPhysicalPayload()
    {
        // Idempotent repeat request while the same authoritative recall is
        // already being completed by TetherPayloadDock.FixedUpdate.
        if (physicalRecallCapturePending)
        {
            TetherPayload pendingPayload =
                DeployedPayload;

            if (pendingPayload != null &&
                payloadDock != null &&
                (payloadDock.IsCapturing(pendingPayload) ||
                 payloadDock.IsDocked(pendingPayload)))
            {
                return true;
            }

            // Something broke/interrupted the claimed capture. Restore the
            // deployed/tethered state rather than silently reporting success.
            CancelPendingPhysicalRecall(
                restoreTetherIfPossible: true);

            return false;
        }

        if (deployedWorldItem == null ||
            _deployedItemInstance == null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot recall: no deployed physical payload is tracked.",
                this);

            return false;
        }

        CacheRefs();

        if (payloadDock == null)
        {
            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Cannot dock retrieved payload: " +
                "no TetherPayloadDock is assigned/present.",
                this);

            return false;
        }

        if (!TryGetPayloadSlot(
                out InventorySlot slot))
        {
            return false;
        }

        if (!slot.IsEmpty)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot recall: payload storage slot " +
                $"{PayloadSlotIndex} is occupied.",
                this);

            return false;
        }

        TetherPayload deployedPayload =
            deployedWorldItem.GetComponent<TetherPayload>();

        if (deployedPayload == null)
            return false;

        float previousLength =
            tetherConstraint != null
                ? tetherConstraint.DeployedLength
                : Mathf.Max(
                    0.05f,
                    initialDeployedLength);

        if (tetherConstraint != null)
            tetherConstraint.Detach();

        deployedPayload.SetTetherCollisionLayerActive(
            false);

        if (!payloadDock.TryDock(
                deployedPayload,
                PayloadHangPoint))
        {
            // Defensive rollback. Docking should only be attempted by the winch
            // once the payload is within capture distance, but never strand a
            // previously-tethered payload because a dock setup is invalid.
            RestoreTetherAfterFailedPhysicalRecall(
                deployedPayload,
                previousLength);

            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Retrieved payload reached docking range " +
                "but could not be captured.",
                this);

            return false;
        }

        // IMPORTANT:
        // TryDock now means "capture accepted", not "final pose reached".
        //
        // Keep the deployed WorldItem and ItemInstance authoritative/reserved
        // until payloadDock.IsDocked(payload) becomes true. This avoids a
        // multiplayer/save/UI state where storage says the bell is stowed while
        // the physical shell is still moving into the dock.
        physicalRecallCapturePending =
            true;

        pendingPhysicalRecallPreviousLength =
            Mathf.Max(
                0.05f,
                previousLength);

        // Winch/tether retrieval is over once the dock owns the payload. The
        // dock's own authoritative FixedUpdate now owns the final capture motion.
        EndPayloadRetrieval();

        return true;
    }

    private void ReconcilePendingPhysicalRecall()
    {
        if (!physicalRecallCapturePending)
            return;

        CacheRefs();

        TetherPayload payload =
            DeployedPayload;

        if (payload == null ||
            payloadDock == null)
        {
            CancelPendingPhysicalRecall(
                restoreTetherIfPossible: true);

            return;
        }

        if (payloadDock.IsDocked(
                payload))
        {
            CompletePendingPhysicalRecall(
                payload);

            return;
        }

        if (payloadDock.IsCapturing(
                payload))
        {
            return;
        }

        // The dock no longer owns this payload, so the asynchronous recall was
        // interrupted. Return to ordinary deployed/tethered authority.
        CancelPendingPhysicalRecall(
            restoreTetherIfPossible: true);
    }

    private void CompletePendingPhysicalRecall(
        TetherPayload payload)
    {
        if (!physicalRecallCapturePending ||
            payload == null ||
            payloadDock == null ||
            !payloadDock.IsDocked(payload) ||
            deployedWorldItem == null ||
            _deployedItemInstance == null)
        {
            return;
        }

        if (!TryGetPayloadSlot(
                out InventorySlot slot))
        {
            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Physical payload finished docking, " +
                "but its reserved storage slot could not be resolved. " +
                "Rolling the recall back to deployed/tethered state.",
                this);

            CancelPendingPhysicalRecall(
                restoreTetherIfPossible: true);

            return;
        }

        if (!slot.IsEmpty)
        {
            // IsPayloadSlotReserved should prevent this in normal play, including
            // multiplayer inventory UI. Never overwrite unexpected data.
            Debug.LogError(
                $"[TetherDeploymentModule:{name}] Physical payload finished docking, " +
                $"but payload slot {PayloadSlotIndex} is unexpectedly occupied. " +
                "Rolling the recall back to deployed/tethered state.",
                this);

            CancelPendingPhysicalRecall(
                restoreTetherIfPossible: true);

            return;
        }

        WorldItem liveWorldItem =
            deployedWorldItem;

        ItemInstance item =
            _deployedItemInstance;

        // Only NOW, after the authoritative dock reports the final pose, does
        // storage regain ItemInstance authority.
        liveWorldItem.Initialize(
            null);

        dockedPhysicalWorldItem =
            liveWorldItem;

        _dockedPhysicalSourceItem =
            item;

        deployedWorldItem =
            null;

        _deployedItemInstance =
            null;

        physicalRecallCapturePending =
            false;

        pendingPhysicalRecallPreviousLength =
            0f;

        _syncingStoredPhysicalPayload =
            true;

        try
        {
            slot.Set(
                item);

            storageModule.ContainerState.NotifyChanged();
        }
        finally
        {
            _syncingStoredPhysicalPayload =
                false;
        }

        SuppressStoredPayloadVisual();

        SetDeploymentState(
            TetherDeploymentState.Stowed);

        PayloadChanged?.Invoke(
            this);
    }

    private void CancelPendingPhysicalRecall(
        bool restoreTetherIfPossible)
    {
        if (!physicalRecallCapturePending)
            return;

        TetherPayload payload =
            DeployedPayload;

        float previousLength =
            Mathf.Max(
                0.05f,
                pendingPhysicalRecallPreviousLength > 0f
                    ? pendingPhysicalRecallPreviousLength
                    : initialDeployedLength);

        physicalRecallCapturePending =
            false;

        pendingPhysicalRecallPreviousLength =
            0f;

        if (payloadDock != null &&
            payload != null &&
            payloadDock.GetDockState(payload) !=
                TetherDockState.Free)
        {
            payloadDock.TryRelease(
                payload);
        }

        if (restoreTetherIfPossible &&
            payload != null)
        {
            RestoreTetherAfterFailedPhysicalRecall(
                payload,
                previousLength);
        }
    }

    private void RestoreTetherAfterFailedPhysicalRecall(
        TetherPayload payload,
        float previousLength)
    {
        if (payload == null)
            return;

        if (!payload.SetTetherCollisionLayerActive(
                true))
        {
            return;
        }

        if (tetherConstraint == null)
            return;

        Boat owningBoat =
            GetComponentInParent<Boat>();

        Rigidbody2D boatBody =
            owningBoat != null
                ? owningBoat.GetComponent<Rigidbody2D>()
                : null;

        tetherConstraint.Bind(
            TetherExitPoint,
            boatBody,
            payload,
            Mathf.Max(
                0.05f,
                previousLength),
            0f,
            0f);
    }

    /// <summary>
    /// Permanently detaches the currently deployed payload from this deployment
    /// module without destroying the WorldItem or returning its ItemInstance to
    /// storage. The WorldItem remains exactly where physics left it.
    ///
    /// Persistence after this point is governed by ItemDefinition.WorldPersistence.
    /// No BoatOwnedItem ownership is created here.
    /// </summary>
    public bool TryCutLooseDeployedPayload(
        out WorldItem releasedWorldItem)
    {
        releasedWorldItem =
            null;

        if (deployedWorldItem == null ||
            _deployedItemInstance == null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot cut loose: no deployed payload is tracked.",
                this);

            return false;
        }

        // If cut-loose arrives during the short dock-capture window, cancel the
        // dock claim first. Cut loose must leave an actually independent payload.
        if (physicalRecallCapturePending)
        {
            CancelPendingPhysicalRecall(
                restoreTetherIfPossible: false);
        }

        EndPayloadRetrieval();

        if (tetherConstraint != null)
            tetherConstraint.Detach();

        TetherPayload payload =
            deployedWorldItem.GetComponent<TetherPayload>();

        if (payload != null)
        {
            // Restore the payload's original world collision layers. From this
            // moment onward it is an ordinary independent WorldItem, not a live
            // tether payload.
            payload.SetTetherCollisionLayerActive(
                false);
        }

        BoatOwnedItem owned =
            deployedWorldItem.GetComponent<BoatOwnedItem>();

        if (owned != null &&
            owned.IsOwnedByBoat)
        {
            // A released tether payload must never continue contributing explicit
            // mass/COM to the boat through BoatOwnedItem.
            owned.ClearOwnership();
        }

        releasedWorldItem =
            deployedWorldItem;

        deployedWorldItem =
            null;

        _deployedItemInstance =
            null;

        RefreshStoredPayloadVisual();

        if (storageModule != null)
        {
            storageModule.EnsureContainer();
            storageModule.ContainerState?.NotifyChanged();
        }

        SetDeploymentState(
            TetherDeploymentState.CutLoose);

        PayloadChanged?.Invoke(
            this);

        return true;
    }

    public bool TryRestoreDeployedPayload(
        ItemInstance item,
        Vector3 worldPosition,
        Quaternion worldRotation,
        float deployedLengthMeters,
        out TetherPayload payload)
    {
        payload =
            null;

        if (item == null ||
            item.Definition == null)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot restore deployed payload: item is null/invalid.",
                this);

            return false;
        }

        if (HasDeployedPayload)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot restore deployed payload: one is already deployed.",
                this);

            return false;
        }

        if (!TryGetPayloadSlot(
                out InventorySlot slot))
        {
            return false;
        }

        if (!slot.IsEmpty)
        {
            Debug.LogWarning(
                $"[TetherDeploymentModule:{name}] Cannot restore deployed payload: " +
                $"payload slot {PayloadSlotIndex} is already occupied.",
                this);

            return false;
        }

        // Reuse the normal deployment path rather than maintaining a second
        // spawn/authority-transfer implementation. The item is temporarily placed
        // in storage, then TryDeployStoredPayload transfers the SAME ItemInstance
        // into its WorldItem representation.
        slot.Set(
            item);

        storageModule.ContainerState.NotifyChanged();

        bool deployedSuccessfully =
            keepStoredPayloadPhysical
                ? TryDeployStoredPhysicalPayload(
                    out payload,
                    allowCapturingDockClaim: true)
                : TryDeployStoredPayload(
                    out payload);

        if (!deployedSuccessfully ||
            payload == null ||
            deployedWorldItem == null)
        {
            return false;
        }

        Rigidbody2D rb =
            payload.Rigidbody;

        Vector3 safeWorldPosition =
            worldPosition;

        if (rb != null)
        {
            // Apply saved rotation while the payload is still at its known-safe
            // deployment point. The subsequent cast therefore uses the actual
            // restored collider orientation.
            rb.rotation =
                worldRotation.eulerAngles.z;

            deployedWorldItem.transform.rotation =
                worldRotation;

            Physics2D.SyncTransforms();

            // Live stored physical payloads (diving bells/cages) begin restore at their
            // authored dock on the boat. Sweeping that shell from the dock toward the
            // saved world pose can immediately hit the boat's own hull and incorrectly
            // leave the payload near the dock, after which gravity merely drops it back
            // onto the restored tether length. For these payloads the snapshot pose is
            // authoritative: restore it directly before the first physics step.
            //
            // Non-live stored payloads (for example ordinary anchors spawned only when
            // deployed) keep the conservative sweep used to avoid restoring into terrain.
            safeWorldPosition =
                keepStoredPayloadPhysical
                    ? worldPosition
                    : ResolveSafeRestoredWorldPosition(
                        rb,
                        worldPosition);

            rb.position =
                safeWorldPosition;

            rb.linearVelocity =
                Vector2.zero;

            rb.angularVelocity =
                0f;

            // Rigidbody2D pose writes can reach the Transform hierarchy on the next
            // physics synchronization. DivingBellAirVolume samples a CHILD transform
            // very early in FixedUpdate, so make the restored world pose visible to
            // the whole hierarchy immediately rather than allowing one dock-position
            // atmosphere sample to erase persisted trapped-air state.
            deployedWorldItem.transform.SetPositionAndRotation(
                safeWorldPosition,
                worldRotation);

            Physics2D.SyncTransforms();

            rb.WakeUp();
        }
        else
        {
            deployedWorldItem.transform.SetPositionAndRotation(
                safeWorldPosition,
                worldRotation);
        }

        if (tetherConstraint != null)
        {
            tetherConstraint.SetDeployedLength(
                Mathf.Max(
                    0.05f,
                    deployedLengthMeters));

            tetherConstraint.BeginBreakProtection(
                restoreTetherBreakProtectionSeconds);
        }

        RefreshStoredPayloadVisual();
        RefreshDeploymentState();

        return true;
    }

    private Vector3 ResolveSafeRestoredWorldPosition(
        Rigidbody2D rb,
        Vector3 desiredWorldPosition)
    {
        if (rb == null)
            return desiredWorldPosition;

        Vector2 start =
            rb.position;

        Vector2 desired =
            desiredWorldPosition;

        Vector2 delta =
            desired -
            start;

        float distance =
            delta.magnitude;

        if (distance <= 0.0001f)
            return desiredWorldPosition;

        Vector2 direction =
            delta /
            distance;

        int payloadLayer =
            rb.gameObject.layer;

        int collisionMask =
            Physics2D.GetLayerCollisionMask(
                payloadLayer);

        ContactFilter2D filter =
            new ContactFilter2D();

        filter.SetLayerMask(
            collisionMask);

        filter.useTriggers =
            false;

        RaycastHit2D[] hits =
            new RaycastHit2D[16];

        int hitCount =
            rb.Cast(
                direction,
                filter,
                hits,
                distance);

        float nearestDistance =
            float.PositiveInfinity;

        for (int i = 0;
             i < hitCount;
             i++)
        {
            RaycastHit2D hit =
                hits[i];

            if (hit.collider == null)
                continue;

            if (hit.distance < 0f)
                continue;

            nearestDistance =
                Mathf.Min(
                    nearestDistance,
                    hit.distance);
        }

        if (float.IsPositiveInfinity(
                nearestDistance))
        {
            return desiredWorldPosition;
        }

        float safeDistance =
            Mathf.Max(
                0f,
                nearestDistance -
                Mathf.Max(
                    0f,
                    restoreCollisionSkinMeters));

        Vector2 resolved =
            start +
            direction *
            safeDistance;

        return new Vector3(
            resolved.x,
            resolved.y,
            desiredWorldPosition.z);
    }

    public void BeginPayloadRetrieval()
    {
        TetherPayload payload =
            DeployedPayload;

        if (payload is ITetherRetrievalAwarePayload retrievalAware)
        {
            retrievalAware.BeginTetherRetrieval();
        }
    }

    public void EndPayloadRetrieval()
    {
        TetherPayload payload =
            DeployedPayload;

        if (payload is ITetherRetrievalAwarePayload retrievalAware)
        {
            retrievalAware.EndTetherRetrieval();
        }
    }

    private void RefreshDeploymentState()
    {
        if (deployedWorldItem == null)
        {
            if (HasStoredPayload)
            {
                SetDeploymentState(
                    TetherDeploymentState.Stowed);
            }
            else if (deploymentState !=
                     TetherDeploymentState.CutLoose)
            {
                SetDeploymentState(
                    TetherDeploymentState.Stowed);
            }

            return;
        }

        TetherPayload payload = DeployedPayload;

        if (payload == null)
        {
            // A deployed world item that has somehow lost its TetherPayload is
            // still physically deployed, so do not falsely report it as stowed.
            SetDeploymentState(TetherDeploymentState.Suspended);
            return;
        }

        ITetherBottomContactProvider bottomContact =
            payload as ITetherBottomContactProvider;

        if (bottomContact == null ||
            !bottomContact.IsOnBottom)
        {
            SetDeploymentState(TetherDeploymentState.Suspended);
            return;
        }

        ITetherHoldingStateProvider holdingProvider =
            payload as ITetherHoldingStateProvider;

        if (holdingProvider != null)
        {
            SetDeploymentState(
                holdingProvider.HasActiveHold
                    ? TetherDeploymentState.Holding
                    : TetherDeploymentState.Bottomed);

            return;
        }

        // Bottom contact alone does not imply that a generic payload is holding.
        SetDeploymentState(TetherDeploymentState.Bottomed);
    }

    private void SetDeploymentState(TetherDeploymentState next)
    {
        if (deploymentState == next)
            return;

        deploymentState = next;
        DeploymentStateChanged?.Invoke(this, deploymentState);
    }

    private bool TryGetPayloadSlot(out InventorySlot slot)
    {
        slot = null;

        CacheRefs();

        if (storageModule == null)
            return false;

        storageModule.EnsureContainer();

        ItemContainerState container =
            storageModule.ContainerState;

        if (container == null)
            return false;

        int index = PayloadSlotIndex;

        if (index < 0 || index >= container.SlotCount)
            return false;

        slot = container.GetSlot(index);
        return slot != null;
    }

    private void CacheRefs()
    {
        if (storageModule == null)
            storageModule = GetComponent<StorageModule>();

        if (tetherConstraint == null)
            tetherConstraint = GetComponent<TetherConstraint2D>();

        if (payloadDock == null)
            payloadDock = GetComponent<TetherPayloadDock>();
    }

    private void BindContainer()
    {
        CacheRefs();

        if (storageModule == null)
            return;

        storageModule.EnsureContainer();

        ItemContainerState container =
            storageModule.ContainerState;

        if (ReferenceEquals(
                _subscribedContainer,
                container))
        {
            return;
        }

        UnbindContainer();

        _subscribedContainer =
            container;

        if (_subscribedContainer != null)
            _subscribedContainer.Changed += HandleContainerChanged;
    }

    private void UnbindContainer()
    {
        if (_subscribedContainer != null)
            _subscribedContainer.Changed -= HandleContainerChanged;

        _subscribedContainer = null;
    }

    private void HandleContainerChanged()
    {
        if (!_syncingStoredPhysicalPayload)
            SyncStoredPhysicalPayload();

        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
        PayloadChanged?.Invoke(this);
    }

    private void RefreshStoredPayloadVisual()
    {
        if (keepStoredPayloadPhysical)
        {
            SuppressStoredPayloadVisual();
            return;
        }

        ItemInstance storedItem =
            StoredPayload;

        if (storedItem == null ||
            storedItem.Definition == null)
        {
            SuppressStoredPayloadVisual();
            return;
        }

        EnsureStoredPayloadVisualRenderer();

        if (storedPayloadVisualRenderer == null)
            return;

        ItemDefinition definition =
            storedItem.Definition;

        SpriteRenderer sourceRenderer =
            definition.WorldPrefab != null
                ? definition.WorldPrefab.GetComponentInChildren<SpriteRenderer>(true)
                : null;

        Sprite sprite =
            sourceRenderer != null
                ? sourceRenderer.sprite
                : definition.Icon;

        if (sprite == null)
        {
            SuppressStoredPayloadVisual();
            return;
        }

        storedPayloadVisualRenderer.sprite =
            sprite;

        if (sourceRenderer != null)
        {
            storedPayloadVisualRenderer.color =
                sourceRenderer.color;

            storedPayloadVisualRenderer.flipX =
                sourceRenderer.flipX;

            storedPayloadVisualRenderer.flipY =
                sourceRenderer.flipY;

            storedPayloadVisualRenderer.sharedMaterial =
                sourceRenderer.sharedMaterial;
        }

        SpriteRenderer hostRenderer =
            FindHostSortingRenderer();

        if (hostRenderer != null)
        {
            storedPayloadVisualRenderer.sortingLayerID =
                hostRenderer.sortingLayerID;

            storedPayloadVisualRenderer.sortingOrder =
                hostRenderer.sortingOrder +
                1 +
                storedPayloadSortingOrderOffset;
        }
        else if (sourceRenderer != null)
        {
            // Fallback only if the installed Anchor Hole/module has no renderer.
            storedPayloadVisualRenderer.sortingLayerID =
                sourceRenderer.sortingLayerID;

            storedPayloadVisualRenderer.sortingOrder =
                sourceRenderer.sortingOrder +
                1 +
                storedPayloadSortingOrderOffset;
        }

        storedPayloadVisualRenderer.transform.localScale =
            Vector3.one *
            Mathf.Max(
                0.01f,
                storedPayloadVisualScale);

        // This renderer is state-owned by TetherDeploymentModule. Boat-wide
        // visibility systems may toggle Renderer.enabled, but forceRenderingOff
        // remains our authoritative veto while the physical payload is deployed.
        storedPayloadVisualRenderer.forceRenderingOff = false;
        storedPayloadVisualRenderer.enabled = true;
    }

    private void SuppressStoredPayloadVisual()
    {
        if (storedPayloadVisualRenderer == null)
            return;

        // BoatVisualStateController is allowed to toggle Renderer.enabled on
        // exterior installed-module renderers during board/unboard transitions.
        // forceRenderingOff prevents that generic visibility pass from reviving
        // this stored-only proxy while its real WorldItem is deployed.
        storedPayloadVisualRenderer.forceRenderingOff = true;
        storedPayloadVisualRenderer.enabled = false;
    }

    private SpriteRenderer FindHostSortingRenderer()
    {
        // Prefer a renderer on the installed module root itself.
        SpriteRenderer renderer =
            GetComponent<SpriteRenderer>();

        if (renderer != null &&
            renderer != storedPayloadVisualRenderer)
        {
            return renderer;
        }

        // Then walk upward through the installed Anchor Hole/module hierarchy.
        Transform current =
            transform.parent;

        while (current != null)
        {
            renderer =
                current.GetComponent<SpriteRenderer>();

            if (renderer != null &&
                renderer != storedPayloadVisualRenderer)
            {
                return renderer;
            }

            current =
                current.parent;
        }

        // Final host fallback: a renderer somewhere under this installed module.
        SpriteRenderer[] childRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < childRenderers.Length; i++)
        {
            renderer =
                childRenderers[i];

            if (renderer != null &&
                renderer != storedPayloadVisualRenderer)
            {
                return renderer;
            }
        }

        return null;
    }

    private void EnsureStoredPayloadVisualRenderer()
    {
        if (storedPayloadVisualRenderer != null)
            return;

        Transform parent =
            PayloadHangPoint;

        GameObject visual =
            new GameObject("StoredPayloadVisual");

        visual.transform.SetParent(
            parent,
            false);

        visual.transform.localPosition =
            Vector3.zero;

        visual.transform.localRotation =
            Quaternion.identity;

        visual.transform.localScale =
            Vector3.one *
            Mathf.Max(
                0.01f,
                storedPayloadVisualScale);

        storedPayloadVisualRenderer =
            visual.AddComponent<SpriteRenderer>();
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Log Stored Payload")]
    private void DebugLogStoredPayload()
    {
        if (TryGetStoredPayload(out ItemInstance item) &&
            item != null &&
            item.Definition != null)
        {
            Debug.Log(
                $"[TetherDeploymentModule:{name}] Payload slot {PayloadSlotIndex} contains " +
                $"'{item.Definition.DisplayName}' instanceId='{item.InstanceId}' " +
                $"physicalStored={keepStoredPayloadPhysical} shell={(dockedPhysicalWorldItem != null ? dockedPhysicalWorldItem.name : "NONE")}.",
                this);
        }
        else
        {
            Debug.Log(
                $"[TetherDeploymentModule:{name}] Payload slot {PayloadSlotIndex} is empty.",
                this);
        }
    }

    [ContextMenu("Debug/Deploy Stored Payload")]
    private void DebugDeployStoredPayload()
    {
        if (TryDeployStoredPayload(out TetherPayload payload))
        {
            Debug.Log(
                $"[TetherDeploymentModule:{name}] Deployed payload at {PayloadHangPoint.position}. " +
                $"Initial tether length={initialDeployedLength:F2} m.",
                this);
        }
    }

    [ContextMenu("Debug/Recall Deployed Payload")]
    private void DebugRecallDeployedPayload()
    {
        if (TryRecallDeployedPayload())
        {
            Debug.Log(
                physicalRecallCapturePending
                    ? $"[TetherDeploymentModule:{name}] Physical payload recall accepted; dock capture is in progress."
                    : $"[TetherDeploymentModule:{name}] Recalled deployed payload to storage slot {PayloadSlotIndex}.",
                this);
        }
    }
#endif
}
