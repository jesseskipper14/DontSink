using System.Collections.Generic;
using UnityEngine;

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
            if (worldItem == null || worldItem.Instance == null)
                continue;

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

            manifest.looseItems.Add(new BoatLooseItemSnapshot
            {
                version = 2,
                owningBoatInstanceId = boat.BoatInstanceId,
                item = itemSnapshot,
                localPosition = localPos,
                localRotationZ = localRotZ,

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

        Log($"CaptureManifest complete | count={manifest.looseItems.Count}");

        return manifest;
    }

    public void RestoreManifest(BoatLooseItemManifest manifest)
    {
        if (manifest == null || manifest.looseItems == null)
        {
            Log("RestoreManifest skipped: manifest/null list.");
            return;
        }

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

        WorldItem spawned = Instantiate(prefab, worldPos, worldRot);
        spawned.Initialize(itemInstance);

        EnsureBoatOwnedItemPolicies(spawned, out BoatOwnedItem owned);

        owned.AssignToBoat(boat);

        if (snapshot.isSecured)
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
            $"boatId='{boat.BoatInstanceId}' pos={worldPos}");
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