using System;
using UnityEngine;

[Serializable]
public sealed class CargoCrateStoredSnapshot
{
    public int version = 1;

    public string instanceGuid;
    public string typeGuid;

    // Cached for UI/debug convenience.
    // Authoritative cargo payload is still payloadJson.
    public string itemId;
    public int quantity;

    public string payloadJson;
}

public static class CargoCrateSnapshotUtility
{
    public static CargoCrateStoredSnapshot Capture(CargoCrate crate)
    {
        if (crate == null)
            return null;

        CargoItemIdentity id = crate.GetComponent<CargoItemIdentity>();
        if (id == null)
            id = crate.gameObject.AddComponent<CargoItemIdentity>();

        string typeGuid = id.TypeGuid;
        if (string.IsNullOrWhiteSpace(id.InstanceGuid) || string.IsNullOrWhiteSpace(typeGuid))
        {
            Debug.LogWarning(
                $"[CargoCrateSnapshotUtility] Cannot capture cargo crate '{crate.name}': missing instanceGuid or typeGuid.",
                crate);

            return null;
        }

        string payloadJson = null;
        ICargoManifestPayload payload = crate.GetComponent<ICargoManifestPayload>();
        if (payload != null)
            payloadJson = payload.CapturePayloadJson();

        return new CargoCrateStoredSnapshot
        {
            version = 1,
            instanceGuid = id.InstanceGuid,
            typeGuid = typeGuid,
            itemId = crate.itemId,
            quantity = Mathf.Max(0, crate.quantity),
            payloadJson = payloadJson
        };
    }

    public static CargoCrate RestoreToWorld(
        CargoCrateStoredSnapshot snapshot,
        TradeCargoPrefabCatalog catalog,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Transform parent = null)
    {
        if (snapshot == null)
            return null;

        if (catalog == null)
        {
            Debug.LogWarning("[CargoCrateSnapshotUtility] Cannot restore cargo crate: catalog is null.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(snapshot.typeGuid))
        {
            Debug.LogWarning("[CargoCrateSnapshotUtility] Cannot restore cargo crate: snapshot typeGuid is empty.");
            return null;
        }

        GameObject prefab = catalog.Resolve(snapshot.typeGuid);
        if (prefab == null)
        {
            Debug.LogWarning($"[CargoCrateSnapshotUtility] Missing cargo prefab for typeGuid='{snapshot.typeGuid}'.");
            return null;
        }

        GameObject go = UnityEngine.Object.Instantiate(prefab, worldPosition, worldRotation, parent);
        go.name = $"{prefab.name}(RackCargo)";

        CargoItemIdentity id = go.GetComponent<CargoItemIdentity>();
        if (id == null)
            id = go.AddComponent<CargoItemIdentity>();

        id.ForceAssign(snapshot.instanceGuid, snapshot.typeGuid);

        ICargoManifestPayload payload = go.GetComponent<ICargoManifestPayload>();
        if (payload != null && !string.IsNullOrWhiteSpace(snapshot.payloadJson))
            payload.RestorePayloadJson(snapshot.payloadJson);

        CargoCrate crate = go.GetComponent<CargoCrate>();
        if (crate != null)
        {
            // Belt and suspenders. Payload restore should usually handle this.
            if (!string.IsNullOrWhiteSpace(snapshot.itemId))
                crate.itemId = snapshot.itemId;

            crate.quantity = Mathf.Max(0, snapshot.quantity);
        }

        return crate;
    }
}