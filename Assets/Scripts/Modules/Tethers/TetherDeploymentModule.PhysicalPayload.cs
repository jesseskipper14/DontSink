using UnityEngine;

// Behavior-preserving partial-class extraction.
// Responsibility: PhysicalPayload.
public sealed partial class TetherDeploymentModule
{
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
}
