using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(InstalledModule))]
[RequireComponent(typeof(StorageModule))]
public sealed partial class TetherDeploymentModule : MonoBehaviour, IInstalledModuleLifecycle
{
    [Header("Gameplay Authority")]
    [Tooltip(
        "Gameplay deploy/recall/retrieval mutations are host-authoritative. Persistence restore is " +
        "expected to run on authority; future clients should hydrate from replicated state rather than " +
        "replaying gameplay deployment commands.")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

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
    public bool HasGameplayAuthority =>
        GameplayAuthority.CanRun(gameplayAuthorityMode);

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
        if (HasGameplayAuthority)
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


    private void CacheRefs()
    {
        if (storageModule == null)
            storageModule = GetComponent<StorageModule>();

        if (tetherConstraint == null)
            tetherConstraint = GetComponent<TetherConstraint2D>();

        if (payloadDock == null)
            payloadDock = GetComponent<TetherPayloadDock>();
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
