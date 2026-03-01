using System.Collections.Generic;
using UnityEngine;

public static class CargoManifest
{
    [System.Serializable]
    public struct Snapshot
    {
        public string instanceGuid;
        public string typeGuid;

        public bool secured;

        public Vector2 localPos;   // relative to boat root
        public float localRotZ;    // degrees (2D)

        // NEW: optional per-item payload (crate itemId, quantity, etc.)
        public string payloadJson;

        public Snapshot(string instanceGuid, string typeGuid, bool secured, Vector2 localPos, float localRotZ, string payloadJson)
        {
            this.instanceGuid = instanceGuid;
            this.typeGuid = typeGuid;
            this.secured = secured;
            this.localPos = localPos;
            this.localRotZ = localRotZ;
            this.payloadJson = payloadJson;
        }
    }

    public static List<Snapshot> Capture(Transform boatRoot, Collider2D boardedVolumeCollider)
    {
        var result = new List<Snapshot>(64);
        if (boatRoot == null) return result;

        var byInstance = new HashSet<string>();

        // 1) Secured = parented to boatRoot
        var securedItems = boatRoot.GetComponentsInChildren<CargoItemIdentity>(true);
        for (int i = 0; i < securedItems.Length; i++)
        {
            var id = securedItems[i];
            if (id == null) continue;
            if (string.IsNullOrEmpty(id.InstanceGuid) || string.IsNullOrEmpty(id.TypeGuid)) continue;

            if (byInstance.Add(id.InstanceGuid))
            {
                Vector2 lp = boatRoot.InverseTransformPoint(id.transform.position);
                float lz = GetLocalRotZ(boatRoot, id.transform);
                string payload = CapturePayload(id);

                result.Add(new Snapshot(id.InstanceGuid, id.TypeGuid, secured: true, localPos: lp, localRotZ: lz, payloadJson: payload));
            }
        }

        // 2) Unsecured = overlapping boarded volume
        if (boardedVolumeCollider != null)
        {
            var hits = new Collider2D[256];
            var filter = new ContactFilter2D();
            filter.useTriggers = true;
            filter.useLayerMask = false;

            int count = Physics2D.OverlapCollider(boardedVolumeCollider, filter, hits);
            for (int i = 0; i < count; i++)
            {
                var col = hits[i];
                if (col == null) continue;

                var id = col.GetComponentInParent<CargoItemIdentity>();
                if (id == null) continue;
                if (string.IsNullOrEmpty(id.InstanceGuid) || string.IsNullOrEmpty(id.TypeGuid)) continue;

                if (id.transform.IsChildOf(boatRoot)) continue;

                if (byInstance.Add(id.InstanceGuid))
                {
                    Vector2 lp = boatRoot.InverseTransformPoint(id.transform.position);
                    float lz = GetLocalRotZ(boatRoot, id.transform);
                    string payload = CapturePayload(id);

                    result.Add(new Snapshot(id.InstanceGuid, id.TypeGuid, secured: false, localPos: lp, localRotZ: lz, payloadJson: payload));
                }
            }
        }

        return result;
    }

    public static void Restore(
        Transform boatRoot,
        IReadOnlyList<Snapshot> manifest,
        TradeCargoPrefabCatalog tradeCargoPrefabCatalog)
    {
        if (boatRoot == null || manifest == null || tradeCargoPrefabCatalog == null) return;

        for (int i = 0; i < manifest.Count; i++)
        {
            var s = manifest[i];
            if (string.IsNullOrEmpty(s.instanceGuid) || string.IsNullOrEmpty(s.typeGuid)) continue;

            var prefab = tradeCargoPrefabCatalog.Resolve(s.typeGuid);
            if (prefab == null)
            {
                Debug.LogWarning($"[CargoManifest] Missing cargo prefab for typeGuid='{s.typeGuid}'.");
                continue;
            }

            Vector3 worldPos = boatRoot.TransformPoint(s.localPos);
            Quaternion worldRot = boatRoot.rotation * Quaternion.Euler(0f, 0f, s.localRotZ);

            var go = Object.Instantiate(prefab, worldPos, worldRot);
            go.name = $"{prefab.name}(Cargo)";

            var id = go.GetComponent<CargoItemIdentity>();
            if (id == null) id = go.AddComponent<CargoItemIdentity>();
            id.ForceAssign(s.instanceGuid, s.typeGuid);

            RestorePayload(go, s.payloadJson);

            if (s.secured)
            {
                go.transform.SetParent(boatRoot, true);

                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                }
            }
            else
            {
                go.transform.SetParent(null, true);

                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.bodyType = RigidbodyType2D.Dynamic;
            }

            var crate = go.GetComponent<CargoCrate>();
            if (crate != null)
            {
                // Find scene authority and apply its policy to this crate immediately.
                var store = Object.FindFirstObjectByType<PhysicalCrateItemStore>(FindObjectsInactive.Exclude);
                if (store != null)
                    store.ApplyPolicyTo(crate); // we’ll add this method
            }
        }
    }

    private static string CapturePayload(CargoItemIdentity id)
    {
        if (id == null) return null;
        var payload = id.GetComponent<ICargoManifestPayload>();
        return payload != null ? payload.CapturePayloadJson() : null;
    }

    private static void RestorePayload(GameObject go, string payloadJson)
    {
        if (go == null) return;
        if (string.IsNullOrEmpty(payloadJson)) return;
        var payload = go.GetComponent<ICargoManifestPayload>();
        if (payload != null)
            payload.RestorePayloadJson(payloadJson);
    }

    private static float GetLocalRotZ(Transform boatRoot, Transform item)
    {
        float worldZ = item.rotation.eulerAngles.z;
        float boatZ = boatRoot.rotation.eulerAngles.z;
        return Mathf.DeltaAngle(boatZ, worldZ);
    }
}
