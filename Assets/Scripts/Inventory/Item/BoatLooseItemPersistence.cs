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

            manifest.looseItems.Add(new BoatLooseItemSnapshot
            {
                version = 1,
                owningBoatInstanceId = boat.BoatInstanceId,
                item = itemSnapshot,
                localPosition = localPos,
                localRotationZ = localRotZ
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

        Log(
            $"Restored loose item | itemId='{snapshot.item.itemId}' " +
            $"instanceId='{snapshot.item.instanceId}' " +
            $"boatId='{boat.BoatInstanceId}' pos={worldPos}");
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