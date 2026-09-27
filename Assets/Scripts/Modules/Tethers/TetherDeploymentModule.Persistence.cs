using UnityEngine;

// Behavior-preserving partial-class extraction.
// Responsibility: Persistence.
public sealed partial class TetherDeploymentModule
{
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
}
