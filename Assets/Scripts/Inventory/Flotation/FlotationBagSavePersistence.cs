using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit save/load persistence for deployed flotation bags.
///
/// This is intentionally separate from ordinary scene-transition persistence.
/// Scene transitions call BoatLooseItemPersistence without the save-only channel,
/// so deployed bags are discarded with the unloading scene and are assumed to
/// have deflated during the implied travel time.
/// </summary>
public static class FlotationBagSavePersistence
{
    public static bool UsesSaveOnlyPersistence(
        WorldItem worldItem)
    {
        if (worldItem == null)
            return false;

        DeployableFlotationBag bag =
            worldItem.GetComponent<DeployableFlotationBag>() ??
            worldItem.GetComponentInChildren<DeployableFlotationBag>(true);

        if (bag == null)
            return false;

        RuntimeFlotationAttachment attachment =
            bag.GetComponent<RuntimeFlotationAttachment>() ??
            worldItem.GetComponent<RuntimeFlotationAttachment>() ??
            worldItem.GetComponentInChildren<RuntimeFlotationAttachment>(true);

        // "Deployed" includes an unused bag that is currently rigged, plus any
        // bag that has crossed the irreversible activation boundary even if its
        // tether was later cut. Unattached, never-activated lift bags remain
        // ordinary WorldItems and use normal item persistence.
        return
            bag.HasActivated ||
            (attachment != null && attachment.IsAttached);
    }

    public static void Capture(
        BoatLooseItemManifest manifest,
        Boat boat,
        Scene scene)
    {
        if (manifest == null)
            return;

        manifest.saveOnlyFlotationBags =
            new List<FlotationBagSaveSnapshot>();

        DeployableFlotationBag[] bags =
            Object.FindObjectsByType<DeployableFlotationBag>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        HashSet<string> capturedInstanceIds =
            new HashSet<string>();

        for (int i = 0; i < bags.Length; i++)
        {
            DeployableFlotationBag bag = bags[i];

            if (bag == null ||
                bag.gameObject.scene != scene)
            {
                continue;
            }

            RuntimeFlotationAttachment attachment =
                bag.GetComponent<RuntimeFlotationAttachment>() ??
                bag.GetComponentInParent<RuntimeFlotationAttachment>();

            bool isDeployed =
                bag.HasActivated ||
                (attachment != null && attachment.IsAttached);

            if (!isDeployed)
                continue;

            WorldItem worldItem =
                bag.GetComponent<WorldItem>() ??
                bag.GetComponentInParent<WorldItem>();

            if (worldItem == null ||
                worldItem.Instance == null ||
                worldItem.Instance.Definition == null)
            {
                Warn(
                    $"Capture skipped bag='{bag.name}': WorldItem/ItemInstance/Definition is missing.",
                    boat);
                continue;
            }

            ItemInstanceSnapshot itemSnapshot =
                worldItem.Instance.ToSnapshot();

            if (itemSnapshot == null ||
                string.IsNullOrWhiteSpace(itemSnapshot.instanceId))
            {
                Warn(
                    $"Capture skipped bag='{bag.name}': ItemInstance snapshot or instanceId is missing.",
                    boat);
                continue;
            }

            if (!capturedInstanceIds.Add(itemSnapshot.instanceId))
                continue;

            FlotationBagSaveSnapshot snapshot =
                new FlotationBagSaveSnapshot
                {
                    version = 1,
                    item = itemSnapshot,
                    worldPosition = worldItem.transform.position,
                    worldRotationZ = worldItem.transform.eulerAngles.z,
                    state = bag.State,
                    hasActivated = bag.HasActivated,
                    stateElapsedSeconds = bag.StateElapsedSeconds,
                    flotation01 = bag.Flotation01,
                    targetKind = FlotationPersistenceTargetKind.None,
                    tetherLength =
                        attachment != null
                            ? Mathf.Max(0.01f, attachment.TetherLength)
                            : 0.65f
                };

            if (attachment != null && attachment.IsAttached)
            {
                if (attachment.TargetKind ==
                    FlotationAttachmentTargetKind.WorldFixed)
                {
                    snapshot.targetKind =
                        FlotationPersistenceTargetKind.WorldFixed;

                    snapshot.fixedWorldPoint =
                        attachment.FixedWorldPoint;
                }
                else if (attachment.TargetKind ==
                         FlotationAttachmentTargetKind.Rigidbody)
                {
                    snapshot.targetLocalPoint =
                        attachment.TargetLocalPoint;

                    if (!TryCaptureBodyIdentity(
                            boat,
                            attachment.TargetBody,
                            out snapshot.targetKind,
                            out snapshot.targetStableId))
                    {
                        // Save the bag anyway. On load it will fail soft to an
                        // unattached physical bag and use its existing detached
                        // deflation behavior.
                        snapshot.targetKind =
                            FlotationPersistenceTargetKind.None;

                        snapshot.targetStableId = null;

                        Warn(
                            $"Target has no stable save identity | " +
                            $"bag='{bag.name}' itemInstanceId='{itemSnapshot.instanceId}' " +
                            $"targetBody='{(attachment.TargetBody != null ? attachment.TargetBody.name : "NULL")}'. " +
                            "The bag will restore unattached.",
                            boat);
                    }
                }
            }

            manifest.saveOnlyFlotationBags.Add(snapshot);
        }
    }

    public static void Restore(
        BoatLooseItemManifest manifest,
        Boat boat,
        ItemDefinitionCatalog itemCatalog,
        Scene scene)
    {
        if (manifest?.saveOnlyFlotationBags == null ||
            manifest.saveOnlyFlotationBags.Count == 0 ||
            itemCatalog == null)
        {
            return;
        }

        HashSet<string> liveInstanceIds =
            CollectLiveWorldItemInstanceIds(scene);

        List<FlotationBagSaveSnapshot> restoredSnapshots =
            new List<FlotationBagSaveSnapshot>();

        List<RuntimeFlotationAttachment> restoredAttachments =
            new List<RuntimeFlotationAttachment>();

        // Pass 1: spawn every bag and apply lifecycle state. Attachments are
        // deferred because one saved bag can legally target another saved bag.
        // Two-pass restore makes bag chains independent of snapshot order.
        for (int i = 0;
             i < manifest.saveOnlyFlotationBags.Count;
             i++)
        {
            FlotationBagSaveSnapshot snapshot =
                manifest.saveOnlyFlotationBags[i];

            if (snapshot == null ||
                snapshot.item == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.item.instanceId) &&
                liveInstanceIds.Contains(snapshot.item.instanceId))
            {
                Warn(
                    $"Restore skipped duplicate live ItemInstance " +
                    $"instanceId='{snapshot.item.instanceId}'.",
                    boat);
                continue;
            }

            ItemInstance item =
                ItemInstance.FromSnapshot(
                    snapshot.item,
                    itemCatalog);

            if (item == null ||
                item.Definition == null)
            {
                Warn(
                    $"Restore could not resolve itemId='{snapshot.item.itemId}'.",
                    boat);
                continue;
            }

            WorldItem prefab =
                item.Definition.WorldPrefab;

            if (prefab == null)
            {
                Warn(
                    $"Restore cannot spawn itemId='{snapshot.item.itemId}': WorldPrefab is missing.",
                    boat);
                continue;
            }

            DeployableFlotationBag prefabBag =
                prefab.GetComponent<DeployableFlotationBag>() ??
                prefab.GetComponentInChildren<DeployableFlotationBag>(true);

            if (prefabBag == null)
            {
                Warn(
                    $"Restore cannot spawn itemId='{snapshot.item.itemId}': " +
                    "WorldPrefab has no DeployableFlotationBag.",
                    boat);
                continue;
            }

            WorldItem spawned =
                Object.Instantiate(
                    prefab,
                    snapshot.worldPosition,
                    Quaternion.Euler(
                        0f,
                        0f,
                        snapshot.worldRotationZ));

            if (spawned == null)
                continue;

            spawned.Initialize(item);

            // Save-only flotation owns this physical manifestation. Do not let a
            // generic BoatOwnedItem marker turn it back into transition-persistent
            // cargo merely because the prefab carries that infrastructure.
            BoatOwnedItem owned =
                spawned.GetComponent<BoatOwnedItem>();

            if (owned != null)
                owned.ClearOwnership();

            DeployableFlotationBag bag =
                spawned.GetComponent<DeployableFlotationBag>() ??
                spawned.GetComponentInChildren<DeployableFlotationBag>(true);

            RuntimeFlotationAttachment attachment =
                spawned.GetComponent<RuntimeFlotationAttachment>() ??
                spawned.GetComponentInChildren<RuntimeFlotationAttachment>(true);

            if (bag == null)
            {
                Object.Destroy(spawned.gameObject);
                continue;
            }

            bag.ApplyRuntimeState(
                snapshot.state,
                snapshot.hasActivated,
                Mathf.Max(0f, snapshot.stateElapsedSeconds),
                Mathf.Clamp01(snapshot.flotation01));

            restoredSnapshots.Add(snapshot);
            restoredAttachments.Add(attachment);

            if (!string.IsNullOrWhiteSpace(snapshot.item.instanceId))
            {
                liveInstanceIds.Add(
                    snapshot.item.instanceId);
            }
        }

        // Pass 2: every flotation WorldItem now exists, including possible
        // bag-to-bag targets.
        for (int i = 0;
             i < restoredSnapshots.Count;
             i++)
        {
            FlotationBagSaveSnapshot snapshot =
                restoredSnapshots[i];

            RuntimeFlotationAttachment attachment =
                restoredAttachments[i];

            bool restoredAttachment =
                RestoreAttachment(
                    snapshot,
                    attachment,
                    boat,
                    scene);

            if (!restoredAttachment &&
                attachment != null)
            {
                attachment.ApplyDetachedState();
            }
        }
    }

    private static bool TryCaptureBodyIdentity(
        Boat boat,
        Rigidbody2D targetBody,
        out FlotationPersistenceTargetKind targetKind,
        out string targetStableId)
    {
        targetKind = FlotationPersistenceTargetKind.None;
        targetStableId = null;

        if (targetBody == null)
            return false;

        // Check player before walking upward to Boat. Boarded players can be
        // parented under the boat hierarchy, but the clicked body is still player.
        CharacterPlayer player =
            targetBody.GetComponent<CharacterPlayer>() ??
            targetBody.GetComponentInParent<CharacterPlayer>();

        if (player != null)
        {
            PlayerLoadoutPersistence playerPersistence =
                player.GetComponent<PlayerLoadoutPersistence>() ??
                player.GetComponentInParent<PlayerLoadoutPersistence>() ??
                player.GetComponentInChildren<PlayerLoadoutPersistence>(true);

            string playerKey =
                playerPersistence != null
                    ? playerPersistence.PersistenceKey
                    : GameState.I != null
                        ? GameState.I.LocalPlayerPersistenceKey
                        : GameState.DefaultPlayerPersistenceKey;

            if (!string.IsNullOrWhiteSpace(playerKey))
            {
                targetKind = FlotationPersistenceTargetKind.Player;
                targetStableId =
                    GameState.NormalizePlayerPersistenceKey(playerKey);
                return true;
            }
        }

        DivingBellOccupancy bell =
            targetBody.GetComponent<DivingBellOccupancy>() ??
            targetBody.GetComponentInParent<DivingBellOccupancy>() ??
            targetBody.GetComponentInChildren<DivingBellOccupancy>(true);

        if (bell != null &&
            TryResolvePayloadItemForBell(
                boat,
                bell,
                out ItemInstance bellPayloadItem) &&
            bellPayloadItem != null &&
            !string.IsNullOrWhiteSpace(bellPayloadItem.InstanceId))
        {
            targetKind = FlotationPersistenceTargetKind.DivingBellPayload;
            targetStableId = bellPayloadItem.InstanceId;
            return true;
        }

        WorldItem targetWorldItem =
            targetBody.GetComponent<WorldItem>() ??
            targetBody.GetComponentInParent<WorldItem>();

        if (targetWorldItem != null &&
            targetWorldItem.Instance != null &&
            !string.IsNullOrWhiteSpace(targetWorldItem.Instance.InstanceId))
        {
            targetKind = FlotationPersistenceTargetKind.WorldItem;
            targetStableId = targetWorldItem.Instance.InstanceId;
            return true;
        }

        Boat targetBoat =
            targetBody.GetComponent<Boat>() ??
            targetBody.GetComponentInParent<Boat>();

        if (targetBoat != null &&
            !string.IsNullOrWhiteSpace(targetBoat.BoatInstanceId))
        {
            targetKind = FlotationPersistenceTargetKind.Boat;
            targetStableId = targetBoat.BoatInstanceId;
            return true;
        }

        return false;
    }

    private static bool RestoreAttachment(
        FlotationBagSaveSnapshot snapshot,
        RuntimeFlotationAttachment attachment,
        Boat boat,
        Scene scene)
    {
        if (snapshot == null ||
            attachment == null)
        {
            return false;
        }

        float tetherLength =
            Mathf.Max(0.01f, snapshot.tetherLength);

        if (snapshot.targetKind ==
            FlotationPersistenceTargetKind.WorldFixed)
        {
            attachment.ApplyWorldAttachment(
                snapshot.fixedWorldPoint,
                tetherLength);

            return attachment.IsAttached;
        }

        if (snapshot.targetKind ==
            FlotationPersistenceTargetKind.None)
        {
            attachment.ApplyDetachedState();
            return false;
        }

        if (!TryResolveTargetBody(
                snapshot.targetKind,
                snapshot.targetStableId,
                boat,
                scene,
                out Rigidbody2D targetBody) ||
            targetBody == null)
        {
            Warn(
                $"Target could not be resolved | bagInstanceId='{snapshot.item?.instanceId}' " +
                $"target={snapshot.targetKind} targetId='{snapshot.targetStableId}'. " +
                "Restoring bag unattached.",
                boat);

            attachment.ApplyDetachedState();
            return false;
        }

        attachment.ApplyBodyAttachmentLocal(
            targetBody,
            snapshot.targetLocalPoint,
            tetherLength);

        return attachment.IsAttached;
    }

    private static bool TryResolveTargetBody(
        FlotationPersistenceTargetKind targetKind,
        string targetStableId,
        Boat boat,
        Scene scene,
        out Rigidbody2D targetBody)
    {
        targetBody = null;

        if (string.IsNullOrWhiteSpace(targetStableId))
            return false;

        switch (targetKind)
        {
            case FlotationPersistenceTargetKind.Boat:
            {
                if (boat != null &&
                    string.Equals(
                        boat.BoatInstanceId,
                        targetStableId,
                        System.StringComparison.Ordinal))
                {
                    targetBody = boat.GetComponent<Rigidbody2D>();
                }

                return targetBody != null;
            }

            case FlotationPersistenceTargetKind.Player:
            {
                string normalizedTargetKey =
                    GameState.NormalizePlayerPersistenceKey(
                        targetStableId);

                PlayerLoadoutPersistence[] players =
                    Object.FindObjectsByType<PlayerLoadoutPersistence>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

                for (int i = 0; i < players.Length; i++)
                {
                    PlayerLoadoutPersistence persistence = players[i];

                    if (persistence == null ||
                        persistence.gameObject.scene != scene ||
                        !string.Equals(
                            persistence.PersistenceKey,
                            normalizedTargetKey,
                            System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    CharacterPlayer player =
                        persistence.GetComponent<CharacterPlayer>() ??
                        persistence.GetComponentInParent<CharacterPlayer>() ??
                        persistence.GetComponentInChildren<CharacterPlayer>(true);

                    if (player == null)
                        continue;

                    targetBody = player.GetComponent<Rigidbody2D>();

                    if (targetBody != null)
                        return true;
                }

                return false;
            }

            case FlotationPersistenceTargetKind.DivingBellPayload:
            {
                if (!TryResolveDivingBellByPayloadInstanceId(
                        boat,
                        targetStableId,
                        out DivingBellOccupancy bell) ||
                    bell == null)
                {
                    return false;
                }

                TetherPayload payload = bell.Payload;

                targetBody =
                    payload != null
                        ? payload.Rigidbody
                        : bell.GetComponent<Rigidbody2D>() ??
                          bell.GetComponentInParent<Rigidbody2D>();

                return targetBody != null;
            }

            case FlotationPersistenceTargetKind.WorldItem:
            {
                WorldItem[] worldItems =
                    Object.FindObjectsByType<WorldItem>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

                for (int i = 0; i < worldItems.Length; i++)
                {
                    WorldItem worldItem = worldItems[i];

                    if (worldItem == null ||
                        worldItem.gameObject.scene != scene ||
                        worldItem.Instance == null ||
                        !string.Equals(
                            worldItem.Instance.InstanceId,
                            targetStableId,
                            System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    targetBody =
                        worldItem.GetComponent<Rigidbody2D>() ??
                        worldItem.GetComponentInChildren<Rigidbody2D>(true);

                    return targetBody != null;
                }

                return false;
            }

            default:
                return false;
        }
    }

    private static bool TryResolvePayloadItemForBell(
        Boat boat,
        DivingBellOccupancy bell,
        out ItemInstance payloadItem)
    {
        payloadItem = null;

        if (boat == null ||
            bell == null)
        {
            return false;
        }

        TetherDeploymentModule[] deployments =
            boat.GetComponentsInChildren<TetherDeploymentModule>(true);

        for (int i = 0; i < deployments.Length; i++)
        {
            TetherDeploymentModule deployment = deployments[i];
            if (deployment == null)
                continue;

            if (object.ReferenceEquals(
                    ResolveBellOccupancy(deployment.DeployedWorldItem),
                    bell))
            {
                payloadItem = deployment.ReservedPayloadItem;
                return payloadItem != null;
            }

            if (object.ReferenceEquals(
                    ResolveBellOccupancy(deployment.DockedPhysicalWorldItem),
                    bell))
            {
                payloadItem = deployment.StoredPayload;
                return payloadItem != null;
            }
        }

        return false;
    }

    private static bool TryResolveDivingBellByPayloadInstanceId(
        Boat boat,
        string payloadInstanceId,
        out DivingBellOccupancy bell)
    {
        bell = null;

        if (boat == null ||
            string.IsNullOrWhiteSpace(payloadInstanceId))
        {
            return false;
        }

        TetherDeploymentModule[] deployments =
            boat.GetComponentsInChildren<TetherDeploymentModule>(true);

        for (int i = 0; i < deployments.Length; i++)
        {
            TetherDeploymentModule deployment = deployments[i];
            if (deployment == null)
                continue;

            ItemInstance deployedItem = deployment.ReservedPayloadItem;

            if (deployedItem != null &&
                string.Equals(
                    deployedItem.InstanceId,
                    payloadInstanceId,
                    System.StringComparison.Ordinal))
            {
                bell = ResolveBellOccupancy(deployment.DeployedWorldItem);
                if (bell != null)
                    return true;
            }

            ItemInstance storedItem = deployment.StoredPayload;

            if (storedItem != null &&
                string.Equals(
                    storedItem.InstanceId,
                    payloadInstanceId,
                    System.StringComparison.Ordinal))
            {
                bell = ResolveBellOccupancy(deployment.DockedPhysicalWorldItem);
                if (bell != null)
                    return true;
            }
        }

        return false;
    }

    private static DivingBellOccupancy ResolveBellOccupancy(
        WorldItem worldItem)
    {
        if (worldItem == null)
            return null;

        return
            worldItem.GetComponent<DivingBellOccupancy>() ??
            worldItem.GetComponentInChildren<DivingBellOccupancy>(true);
    }

    private static HashSet<string> CollectLiveWorldItemInstanceIds(
        Scene scene)
    {
        HashSet<string> ids = new HashSet<string>();

        WorldItem[] worldItems =
            Object.FindObjectsByType<WorldItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < worldItems.Length; i++)
        {
            WorldItem worldItem = worldItems[i];

            if (worldItem == null ||
                worldItem.gameObject.scene != scene ||
                worldItem.Instance == null ||
                string.IsNullOrWhiteSpace(worldItem.Instance.InstanceId))
            {
                continue;
            }

            ids.Add(worldItem.Instance.InstanceId);
        }

        return ids;
    }

    private static void Warn(
        string message,
        Object context)
    {
        Debug.LogWarning(
            $"[FlotationBagSavePersistence] {message}",
            context);
    }
}
