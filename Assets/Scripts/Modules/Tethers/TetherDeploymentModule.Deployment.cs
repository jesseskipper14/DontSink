using UnityEngine;

// Behavior-preserving partial-class extraction.
// Responsibility: Deployment.
public sealed partial class TetherDeploymentModule
{
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

        if (!HasGameplayAuthority)
            return false;

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
        if (!HasGameplayAuthority)
            return false;

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
    public void BeginPayloadRetrieval()
    {
        if (!HasGameplayAuthority)
            return;

        TetherPayload payload =
            DeployedPayload;

        if (payload is ITetherRetrievalAwarePayload retrievalAware)
        {
            retrievalAware.BeginTetherRetrieval();
        }
    }
    public void EndPayloadRetrieval()
    {
        if (!HasGameplayAuthority)
            return;

        TetherPayload payload =
            DeployedPayload;

        if (payload is ITetherRetrievalAwarePayload retrievalAware)
        {
            retrievalAware.EndTetherRetrieval();
        }
    }
}
