using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class BoatLooseItemPersistence : MonoBehaviour
{
    [SerializeField] private Boat boat;
    [SerializeField] private BoatItemRegistry itemRegistry;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private void Awake()
    {
        if (boat == null)
            boat = GetComponent<Boat>();

        if (itemRegistry == null)
            itemRegistry = GetComponent<BoatItemRegistry>();
    }

    public BoatLooseItemManifest CaptureManifest()
    {
        var manifest = new BoatLooseItemManifest();

        if (boat == null || itemRegistry == null)
        {
            LogWarning(
                $"CaptureManifest skipped | boat={(boat != null ? boat.name : "NULL")} " +
                $"itemRegistry={(itemRegistry != null ? itemRegistry.name : "NULL")}");
            return manifest;
        }

        List<BoatOwnedItem> items = itemRegistry.SnapshotItems();

        foreach (BoatOwnedItem owned in items)
        {
            if (owned == null)
                continue;

            WorldItem worldItem = owned.GetComponent<WorldItem>();
            if (worldItem == null ||
                worldItem.Instance == null ||
                worldItem.Instance.Definition == null)
            {
                continue;
            }

            if (worldItem.Instance.Definition.WorldPersistence ==
                WorldItemPersistencePolicy.Never)
            {
                continue;
            }

            ItemInstanceSnapshot itemSnapshot = worldItem.Instance.ToSnapshot();
            if (itemSnapshot == null)
                continue;

            Vector3 localPos = boat.transform.InverseTransformPoint(worldItem.transform.position);
            float localRotZ = Mathf.DeltaAngle(
                boat.transform.eulerAngles.z,
                worldItem.transform.eulerAngles.z
            );

            BoatSecuredItem secured = worldItem.GetComponent<BoatSecuredItem>();
            MoneyChestSlotSecuredItem moneySlotSecured = worldItem.GetComponent<MoneyChestSlotSecuredItem>();

            bool isMoneySlotSecured =
                moneySlotSecured != null &&
                moneySlotSecured.IsSecured;

            bool isCargoSecured =
                !isMoneySlotSecured &&
                secured != null &&
                secured.IsSecured;

            bool hasBellContext =
                TryCaptureDivingBellContext(
                    worldItem,
                    out string bellPayloadInstanceId,
                    out Vector2 bellLocalPosition,
                    out float bellLocalRotationZ);

            // Bell containment is a physical loose-item context, not ordinary boat securing.
            // Do not persist contradictory securing state if an item somehow still has a stale
            // securing marker while physically living in the bell.
            if (hasBellContext)
            {
                isMoneySlotSecured = false;
                isCargoSecured = false;
            }

            manifest.looseItems.Add(new BoatLooseItemSnapshot
            {
                version = 3,
                owningBoatInstanceId = boat.BoatInstanceId,
                item = itemSnapshot,
                localPosition = localPos,
                localRotationZ = localRotZ,

                wasContainedInDivingBell = hasBellContext,
                divingBellPayloadInstanceId = hasBellContext
                    ? bellPayloadInstanceId
                    : null,
                divingBellLocalPosition = hasBellContext
                    ? bellLocalPosition
                    : Vector2.zero,
                divingBellLocalRotationZ = hasBellContext
                    ? bellLocalRotationZ
                    : 0f,

                isSecured = isMoneySlotSecured || isCargoSecured,

                secureZoneStableId = isMoneySlotSecured
                    ? moneySlotSecured.SlotStableId
                    : secured != null ? secured.SecureZoneStableId : null,

                secureSlotIndex = isMoneySlotSecured
                    ? -1
                    : secured != null ? secured.SecureSlotIndex : -1,

                secureQualityMax01 = isMoneySlotSecured
                    ? 1f
                    : secured != null ? secured.SecureQualityMax01 : 0f,

                secureQualityCurrent01 = isMoneySlotSecured
                    ? 1f
                    : secured != null ? secured.SecureQualityCurrent01 : 0f,

                securedLocalPosition = isMoneySlotSecured
                    ? moneySlotSecured.SecuredLocalPosition
                    : secured != null ? secured.SecuredLocalPosition : Vector2.zero,

                securedLocalRotationZ = isMoneySlotSecured
                    ? moneySlotSecured.SecuredLocalRotationZ
                    : secured != null ? secured.SecuredLocalRotationZ : 0f,

                usedRope = !isMoneySlotSecured && secured != null && secured.UsedRope,

                ropeBonus01 = isMoneySlotSecured
                    ? 0f
                    : secured != null ? secured.RopeBonus01 : 0f
            });
        }

        CapturePersistentWorldItems(
            manifest);

        Log(
            $"CaptureManifest complete | boatLoose={manifest.looseItems.Count} " +
            $"persistentWorld={(manifest.persistentWorldItems != null ? manifest.persistentWorldItems.Count : 0)}");

        return manifest;
    }

    public void RestoreManifest(BoatLooseItemManifest manifest)
    {
        if (manifest == null)
        {
            Log("RestoreManifest skipped: manifest is null.");
            return;
        }

        if (manifest.looseItems == null)
            manifest.looseItems = new List<BoatLooseItemSnapshot>();

        if (manifest.persistentWorldItems == null)
            manifest.persistentWorldItems = new List<PersistentWorldItemSnapshot>();

        if (boat == null || itemRegistry == null || itemCatalog == null)
        {
            Debug.LogError(
                $"[BoatLooseItemPersistence:{name}] Missing refs. " +
                $"boat={(boat != null ? boat.name : "NULL")} " +
                $"itemRegistry={(itemRegistry != null ? itemRegistry.name : "NULL")} " +
                $"itemCatalog={(itemCatalog != null ? itemCatalog.name : "NULL")}",
                this);
            return;
        }

        Log($"RestoreManifest BEGIN | count={manifest.looseItems.Count}");

        for (int i = 0; i < manifest.looseItems.Count; i++)
        {
            RestoreLooseItem(manifest.looseItems[i]);
        }

        RestorePersistentWorldItems(
            manifest);

        Log("RestoreManifest END");
    }

    private void RestoreLooseItem(BoatLooseItemSnapshot snapshot)
    {
        if (snapshot == null || snapshot.item == null)
            return;

        ItemInstance itemInstance = ItemInstance.FromSnapshot(snapshot.item, itemCatalog);
        if (itemInstance == null || itemInstance.Definition == null)
            return;

        WorldItem prefab = itemInstance.Definition.WorldPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[BoatLooseItemPersistence:{name}] No WorldPrefab for itemId='{snapshot.item.itemId}'.", this);
            return;
        }

        Vector3 worldPos = boat.transform.TransformPoint(snapshot.localPosition);
        Quaternion worldRot = boat.transform.rotation * Quaternion.Euler(0f, 0f, snapshot.localRotationZ);

        DivingBellOccupancy restoredBell = null;

        if (snapshot.wasContainedInDivingBell &&
            !string.IsNullOrWhiteSpace(snapshot.divingBellPayloadInstanceId))
        {
            if (TryResolveDivingBellByPayloadInstanceId(
                    snapshot.divingBellPayloadInstanceId,
                    out restoredBell) &&
                restoredBell != null)
            {
                worldPos =
                    restoredBell.transform.TransformPoint(
                        snapshot.divingBellLocalPosition);

                worldRot =
                    restoredBell.transform.rotation *
                    Quaternion.Euler(
                        0f,
                        0f,
                        snapshot.divingBellLocalRotationZ);
            }
            else
            {
                LogWarning(
                    $"BellItem restore fallback | itemId='{snapshot.item.itemId}' " +
                    $"instanceId='{snapshot.item.instanceId}' expectedBellPayload='{snapshot.divingBellPayloadInstanceId}'. " +
                    "Referenced bell was not available; using saved boat-local pose.");
            }
        }

        WorldItem spawned = Instantiate(prefab, worldPos, worldRot);
        spawned.Initialize(itemInstance);

        EnsureBoatOwnedItemPolicies(spawned, out BoatOwnedItem owned);

        owned.AssignToBoat(boat);

        bool restoredBellContainment =
            restoredBell != null &&
            RestoreDivingBellContainment(
                spawned,
                owned,
                restoredBell);

        if (snapshot.isSecured &&
            !restoredBellContainment)
        {
            if (!TryRestoreMoneyChestSlotSecured(spawned, snapshot))
            {
                BoatSecuredItem secured = spawned.GetComponent<BoatSecuredItem>();
                if (secured == null)
                    secured = spawned.gameObject.AddComponent<BoatSecuredItem>();

                BoatSecureZone zone = FindSecureZone(snapshot.secureZoneStableId);

                secured.RestoreSecuredState(
                    boat,
                    zone,
                    snapshot.secureSlotIndex,
                    snapshot.secureQualityMax01,
                    snapshot.secureQualityCurrent01,
                    snapshot.securedLocalPosition,
                    snapshot.securedLocalRotationZ,
                    snapshot.usedRope,
                    snapshot.ropeBonus01);
            }
        }

        Log(
            $"Restored loose item | itemId='{snapshot.item.itemId}' " +
            $"instanceId='{snapshot.item.instanceId}' " +
            $"boatId='{boat.BoatInstanceId}' pos={worldPos} " +
            $"bellContained={restoredBellContainment}");
    }

    private bool TryCaptureDivingBellContext(
        WorldItem worldItem,
        out string bellPayloadInstanceId,
        out Vector2 bellLocalPosition,
        out float bellLocalRotationZ)
    {
        bellPayloadInstanceId = null;
        bellLocalPosition = Vector2.zero;
        bellLocalRotationZ = 0f;

        if (worldItem == null ||
            boat == null)
        {
            return false;
        }

        DivingBellContainedItem contained =
            worldItem.GetComponent<DivingBellContainedItem>();

        if (contained == null ||
            !contained.IsContainedInBell ||
            contained.CurrentBell == null)
        {
            return false;
        }

        DivingBellOccupancy bell =
            contained.CurrentBell;

        if (!TryResolvePayloadItemForBell(
                bell,
                out ItemInstance bellPayloadItem) ||
            bellPayloadItem == null ||
            string.IsNullOrWhiteSpace(
                bellPayloadItem.InstanceId))
        {
            LogWarning(
                $"BellItem capture fallback | item='{worldItem.name}' is contained in bell '{bell.name}', " +
                "but no owning tether payload ItemInstance could be resolved. Saving ordinary boat-local pose only.");

            return false;
        }

        bellPayloadInstanceId =
            bellPayloadItem.InstanceId;

        bellLocalPosition =
            bell.transform.InverseTransformPoint(
                worldItem.transform.position);

        bellLocalRotationZ =
            Mathf.DeltaAngle(
                bell.transform.eulerAngles.z,
                worldItem.transform.eulerAngles.z);

        return true;
    }

    private bool TryResolvePayloadItemForBell(
        DivingBellOccupancy bell,
        out ItemInstance payloadItem)
    {
        payloadItem = null;

        if (bell == null ||
            boat == null)
        {
            return false;
        }

        TetherDeploymentModule[] deployments =
            boat.GetComponentsInChildren<TetherDeploymentModule>(
                true);

        for (int i = 0;
             i < deployments.Length;
             i++)
        {
            TetherDeploymentModule deployment =
                deployments[i];

            if (deployment == null)
                continue;

            DivingBellOccupancy deployedBell =
                ResolveBellOccupancy(
                    deployment.DeployedWorldItem);

            if (ReferenceEquals(
                    deployedBell,
                    bell))
            {
                payloadItem =
                    deployment.ReservedPayloadItem;

                return payloadItem != null;
            }

            DivingBellOccupancy dockedBell =
                ResolveBellOccupancy(
                    deployment.DockedPhysicalWorldItem);

            if (ReferenceEquals(
                    dockedBell,
                    bell))
            {
                payloadItem =
                    deployment.StoredPayload;

                return payloadItem != null;
            }
        }

        return false;
    }

    private bool TryResolveDivingBellByPayloadInstanceId(
        string payloadInstanceId,
        out DivingBellOccupancy bell)
    {
        bell = null;

        if (boat == null ||
            string.IsNullOrWhiteSpace(
                payloadInstanceId))
        {
            return false;
        }

        TetherDeploymentModule[] deployments =
            boat.GetComponentsInChildren<TetherDeploymentModule>(
                true);

        for (int i = 0;
             i < deployments.Length;
             i++)
        {
            TetherDeploymentModule deployment =
                deployments[i];

            if (deployment == null)
                continue;

            ItemInstance deployedItem =
                deployment.ReservedPayloadItem;

            if (deployedItem != null &&
                string.Equals(
                    deployedItem.InstanceId,
                    payloadInstanceId,
                    System.StringComparison.Ordinal))
            {
                bell =
                    ResolveBellOccupancy(
                        deployment.DeployedWorldItem);

                if (bell != null)
                    return true;
            }

            ItemInstance storedItem =
                deployment.StoredPayload;

            if (storedItem != null &&
                string.Equals(
                    storedItem.InstanceId,
                    payloadInstanceId,
                    System.StringComparison.Ordinal))
            {
                bell =
                    ResolveBellOccupancy(
                        deployment.DockedPhysicalWorldItem);

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
            worldItem.GetComponentInChildren<DivingBellOccupancy>(
                true);
    }

    private bool RestoreDivingBellContainment(
        WorldItem spawned,
        BoatOwnedItem owned,
        DivingBellOccupancy bell)
    {
        if (spawned == null ||
            owned == null ||
            bell == null)
        {
            return false;
        }

        DivingBellContainedItem contained =
            spawned.GetComponent<DivingBellContainedItem>();

        if (contained == null)
        {
            contained =
                spawned.gameObject
                    .AddComponent<DivingBellContainedItem>();
        }

        if (!contained.AssignToBell(
                bell))
        {
            LogWarning(
                $"BellItem restore failed | item='{spawned.name}' resolved bell='{bell.name}', " +
                "but DivingBellContainedItem.AssignToBell rejected the restore. Item remains ordinary boat-owned loose cargo.");

            return false;
        }

        // Match BoatOwnedItemEscapeTracker semantics immediately rather than waiting
        // a frame: docked bell cargo is physically supported by the boat; deployed
        // bell cargo contributes through DivingBellMassAggregator instead.
        owned.SetPhysicallyContainedByOwningBoat(
            bell.IsDocked);

        return true;
    }

    private void CapturePersistentWorldItems(
        BoatLooseItemManifest manifest)
    {
        if (manifest == null)
            return;

        if (manifest.persistentWorldItems == null)
        {
            manifest.persistentWorldItems =
                new List<PersistentWorldItemSnapshot>();
        }

        PersistentWorldItemSnapshot contextTemplate =
            BuildCurrentWorldContextTemplate();

        List<PersistentWorldItemSnapshot> merged =
            new List<PersistentWorldItemSnapshot>();

        // Carry forward persistent items belonging to OTHER contexts. The current
        // context is replaced from live scene state below, which naturally removes
        // snapshots for items that were picked up/recovered since the last save.
        BoatLooseItemManifest previousManifest =
            GameState.I != null &&
            GameState.I.boat != null
                ? GameState.I.boat.looseItems
                : null;

        if (previousManifest?.persistentWorldItems != null)
        {
            for (int i = 0;
                 i < previousManifest.persistentWorldItems.Count;
                 i++)
            {
                PersistentWorldItemSnapshot old =
                    previousManifest.persistentWorldItems[i];

                if (old == null ||
                    old.item == null)
                {
                    continue;
                }

                if (IsSameWorldContext(
                        old,
                        contextTemplate))
                {
                    continue;
                }

                merged.Add(
                    old);
            }
        }

        HashSet<string> capturedInstanceIds =
            new HashSet<string>();

        WorldItem[] worldItems =
            FindObjectsByType<WorldItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        for (int i = 0;
             i < worldItems.Length;
             i++)
        {
            WorldItem worldItem =
                worldItems[i];

            if (worldItem == null ||
                worldItem.Instance == null ||
                worldItem.Instance.Definition == null)
            {
                continue;
            }

            if (worldItem.gameObject.scene !=
                gameObject.scene)
            {
                continue;
            }

            if (worldItem.Instance.Definition.WorldPersistence !=
                WorldItemPersistencePolicy.PersistentWorld)
            {
                continue;
            }

            BoatOwnedItem owned =
                worldItem.GetComponent<BoatOwnedItem>();

            if (owned != null &&
                owned.IsOwnedByBoat)
            {
                // While aboard, ordinary boat-loose persistence owns this item.
                // This avoids a second snapshot for the same ItemInstance.
                continue;
            }

            ItemInstanceSnapshot itemSnapshot =
                worldItem.Instance.ToSnapshot();

            if (itemSnapshot == null ||
                string.IsNullOrWhiteSpace(
                    itemSnapshot.instanceId))
            {
                continue;
            }

            if (!capturedInstanceIds.Add(
                    itemSnapshot.instanceId))
            {
                continue;
            }

            merged.Add(
                new PersistentWorldItemSnapshot
                {
                    version =
                        1,

                    item =
                        itemSnapshot,

                    contextKind =
                        contextTemplate.contextKind,

                    sceneName =
                        contextTemplate.sceneName,

                    nodeStableId =
                        contextTemplate.nodeStableId,

                    routeFromNodeId =
                        contextTemplate.routeFromNodeId,

                    routeToNodeId =
                        contextTemplate.routeToNodeId,

                    routeSeed =
                        contextTemplate.routeSeed,

                    worldPosition =
                        worldItem.transform.position,

                    worldRotationZ =
                        worldItem.transform.eulerAngles.z
                });
        }

        manifest.persistentWorldItems =
            merged;
    }

    private void RestorePersistentWorldItems(
        BoatLooseItemManifest manifest)
    {
        if (manifest?.persistentWorldItems == null ||
            itemCatalog == null)
        {
            return;
        }

        PersistentWorldItemSnapshot currentContext =
            BuildCurrentWorldContextTemplate();

        HashSet<string> liveInstanceIds =
            CollectLiveWorldItemInstanceIds();

        for (int i = 0;
             i < manifest.persistentWorldItems.Count;
             i++)
        {
            PersistentWorldItemSnapshot snapshot =
                manifest.persistentWorldItems[i];

            if (snapshot == null ||
                snapshot.item == null ||
                !IsSameWorldContext(
                    snapshot,
                    currentContext))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(
                    snapshot.item.instanceId) &&
                liveInstanceIds.Contains(
                    snapshot.item.instanceId))
            {
                continue;
            }

            ItemInstance item =
                ItemInstance.FromSnapshot(
                    snapshot.item,
                    itemCatalog);

            if (item == null ||
                item.Definition == null ||
                item.Definition.WorldPersistence !=
                    WorldItemPersistencePolicy.PersistentWorld)
            {
                continue;
            }

            WorldItem prefab =
                item.Definition.WorldPrefab;

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[BoatLooseItemPersistence:{name}] Cannot restore persistent world item " +
                    $"itemId='{snapshot.item.itemId}': no WorldPrefab.",
                    this);

                continue;
            }

            WorldItem spawned =
                Instantiate(
                    prefab,
                    snapshot.worldPosition,
                    Quaternion.Euler(
                        0f,
                        0f,
                        snapshot.worldRotationZ));

            if (spawned == null)
                continue;

            spawned.Initialize(
                item);

            BoatOwnedItem owned =
                spawned.GetComponent<BoatOwnedItem>();

            if (owned != null)
                owned.ClearOwnership();

            TetherPayload tetherPayload =
                spawned.GetComponent<TetherPayload>();

            if (tetherPayload != null)
            {
                tetherPayload.SetTetherCollisionLayerActive(
                    false);
            }

            if (!string.IsNullOrWhiteSpace(
                    snapshot.item.instanceId))
            {
                liveInstanceIds.Add(
                    snapshot.item.instanceId);
            }

            Log(
                $"Restored persistent world item | itemId='{snapshot.item.itemId}' " +
                $"instanceId='{snapshot.item.instanceId}' pos={snapshot.worldPosition}");
        }
    }

    private PersistentWorldItemSnapshot BuildCurrentWorldContextTemplate()
    {
        PersistentWorldItemSnapshot context =
            new PersistentWorldItemSnapshot
            {
                sceneName =
                    SceneManager.GetActiveScene().name
            };

        GameState gs =
            GameState.I;

        if (gs?.activeTravel != null)
        {
            context.contextKind =
                PersistentWorldItemContextKind.Route;

            context.routeFromNodeId =
                gs.activeTravel.fromNodeStableId;

            context.routeToNodeId =
                gs.activeTravel.toNodeStableId;

            context.routeSeed =
                gs.activeTravel.seed;

            return context;
        }

        if (gs?.player != null &&
            !string.IsNullOrWhiteSpace(
                gs.player.currentNodeId))
        {
            context.contextKind =
                PersistentWorldItemContextKind.Node;

            context.nodeStableId =
                gs.player.currentNodeId;

            return context;
        }

        context.contextKind =
            PersistentWorldItemContextKind.Scene;

        return context;
    }

    private static bool IsSameWorldContext(
        PersistentWorldItemSnapshot a,
        PersistentWorldItemSnapshot b)
    {
        if (a == null ||
            b == null ||
            a.contextKind != b.contextKind)
        {
            return false;
        }

        if (!string.Equals(
                a.sceneName,
                b.sceneName,
                System.StringComparison.Ordinal))
        {
            return false;
        }

        switch (a.contextKind)
        {
            case PersistentWorldItemContextKind.Node:
                return string.Equals(
                    a.nodeStableId,
                    b.nodeStableId,
                    System.StringComparison.Ordinal);

            case PersistentWorldItemContextKind.Route:
                return
                    a.routeSeed ==
                        b.routeSeed &&
                    string.Equals(
                        a.routeFromNodeId,
                        b.routeFromNodeId,
                        System.StringComparison.Ordinal) &&
                    string.Equals(
                        a.routeToNodeId,
                        b.routeToNodeId,
                        System.StringComparison.Ordinal);

            default:
                return true;
        }
    }

    private static HashSet<string> CollectLiveWorldItemInstanceIds()
    {
        HashSet<string> ids =
            new HashSet<string>();

        WorldItem[] worldItems =
            FindObjectsByType<WorldItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        for (int i = 0;
             i < worldItems.Length;
             i++)
        {
            WorldItem worldItem =
                worldItems[i];

            if (worldItem == null ||
                worldItem.Instance == null)
            {
                continue;
            }

            ItemInstanceSnapshot snapshot =
                worldItem.Instance.ToSnapshot();

            if (snapshot != null &&
                !string.IsNullOrWhiteSpace(
                    snapshot.instanceId))
            {
                ids.Add(
                    snapshot.instanceId);
            }
        }

        return ids;
    }

    private bool TryRestoreMoneyChestSlotSecured(
    WorldItem spawned,
    BoatLooseItemSnapshot snapshot)
    {
        if (spawned == null || snapshot == null)
            return false;

        MoneyChestState chest =
            spawned.GetComponent<MoneyChestState>() ??
            spawned.GetComponentInChildren<MoneyChestState>(true);

        if (chest == null)
            return false;

        MoneyChestSecureSlot slot =
            MoneyChestSecureSlot.FindByStableId(snapshot.secureZoneStableId);

        if (slot == null)
        {
            MoneyChestSecureSlot[] slots =
                boat != null
                    ? boat.GetComponentsInChildren<MoneyChestSecureSlot>(true)
                    : FindObjectsByType<MoneyChestSecureSlot>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

            if (slots != null && slots.Length > 0)
                slot = slots[0];
        }

        MoneyChestSlotSecuredItem marker = spawned.GetComponent<MoneyChestSlotSecuredItem>();
        if (marker == null)
            marker = spawned.gameObject.AddComponent<MoneyChestSlotSecuredItem>();

        marker.RestoreSecuredState(
            boat,
            slot,
            !string.IsNullOrWhiteSpace(snapshot.secureZoneStableId)
                ? snapshot.secureZoneStableId
                : slot != null ? slot.StableId : "money_chest_slot_01",
            snapshot.securedLocalPosition,
            snapshot.securedLocalRotationZ);

        if (slot != null)
            slot.AdoptRestoredChest(chest);

        Rigidbody2D rb = spawned.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        return true;
    }

    private BoatSecureZone FindSecureZone(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return null;

        BoatSecureZone[] zones = boat != null
            ? boat.GetComponentsInChildren<BoatSecureZone>(true)
            : FindObjectsByType<BoatSecureZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].StableId == stableId)
                return zones[i];
        }

        return null;
    }

    private void EnsureBoatOwnedItemPolicies(WorldItem spawned, out BoatOwnedItem owned)
    {
        owned = null;

        if (spawned == null)
            return;

        owned = spawned.GetComponent<BoatOwnedItem>();
        if (owned == null)
            owned = spawned.gameObject.AddComponent<BoatOwnedItem>();

        BoatOwnedItemLayerPolicy layerPolicy = spawned.GetComponent<BoatOwnedItemLayerPolicy>();
        if (layerPolicy == null)
            layerPolicy = spawned.gameObject.AddComponent<BoatOwnedItemLayerPolicy>();

        BoatOwnedItemVisualPolicy visualPolicy = spawned.GetComponent<BoatOwnedItemVisualPolicy>();
        if (visualPolicy == null)
            visualPolicy = spawned.gameObject.AddComponent<BoatOwnedItemVisualPolicy>();
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatLooseItemPersistence:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[BoatLooseItemPersistence:{name}] {msg}", this);
    }
}