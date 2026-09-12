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

    private ItemInstance _deployedItemInstance;

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
    public TetherConstraint2D TetherConstraint => tetherConstraint;

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
        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
    }

    private void OnEnable()
    {
        CacheRefs();
        BindContainer();
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
            RefreshStoredPayloadVisual();
        }
    }

    private void FixedUpdate()
    {
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
        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
        PayloadChanged?.Invoke(this);
    }

    public void OnRemoved()
    {
        UnbindContainer();
        SetDeploymentState(TetherDeploymentState.Stowed);
        SuppressStoredPayloadVisual();
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

        if (!TryDeployStoredPayload(
                out payload) ||
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

            safeWorldPosition =
                ResolveSafeRestoredWorldPosition(
                    rb,
                    worldPosition);

            rb.position =
                safeWorldPosition;

            rb.linearVelocity =
                Vector2.zero;

            rb.angularVelocity =
                0f;

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
        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
        PayloadChanged?.Invoke(this);
    }

    private void RefreshStoredPayloadVisual()
    {
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
                $"'{item.Definition.DisplayName}' instanceId='{item.InstanceId}'.",
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
                $"[TetherDeploymentModule:{name}] Recalled deployed payload to storage slot {PayloadSlotIndex}.",
                this);
        }
    }
#endif
}
