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

        public Snapshot(string instanceGuid, string typeGuid, bool secured, Vector2 localPos, float localRotZ)
        {
            this.instanceGuid = instanceGuid;
            this.typeGuid = typeGuid;
            this.secured = secured;
            this.localPos = localPos;
            this.localRotZ = localRotZ;
        }
    }

    /// <summary>
    /// Capture:
    /// - secured: any CargoItemIdentity that is parented under boatRoot
    /// - unsecured: any CargoItemIdentity not parented under boatRoot, but overlapping boardedVolume collider
    /// </summary>
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

                result.Add(new Snapshot(id.InstanceGuid, id.TypeGuid, secured: true, localPos: lp, localRotZ: lz));
            }
        }

        // 2) Unsecured = overlapping boarded volume
        if (boardedVolumeCollider != null)
        {
            // Broad overlap: find colliders in volume using bounds, then filter by actual overlap.
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

                // If it's parented under boat root, it’s already “secured” capture above.
                if (id.transform.IsChildOf(boatRoot)) continue;

                if (byInstance.Add(id.InstanceGuid))
                {
                    Vector2 lp = boatRoot.InverseTransformPoint(id.transform.position);
                    float lz = GetLocalRotZ(boatRoot, id.transform);

                    result.Add(new Snapshot(id.InstanceGuid, id.TypeGuid, secured: false, localPos: lp, localRotZ: lz));
                }
            }
        }

        return result;
    }

    public static void Restore(
        Transform boatRoot,
        IReadOnlyList<Snapshot> manifest,
        CargoCatalog cargoCatalog)
    {
        if (boatRoot == null || manifest == null || cargoCatalog == null) return;

        for (int i = 0; i < manifest.Count; i++)
        {
            var s = manifest[i];
            if (string.IsNullOrEmpty(s.instanceGuid) || string.IsNullOrEmpty(s.typeGuid)) continue;

            var prefab = cargoCatalog.Resolve(s.typeGuid);
            if (prefab == null)
            {
                Debug.LogWarning($"[CargoManifest] Missing cargo prefab for typeGuid='{s.typeGuid}'.");
                continue;
            }

            Vector3 worldPos = boatRoot.TransformPoint(s.localPos);
            Quaternion worldRot = boatRoot.rotation * Quaternion.Euler(0f, 0f, s.localRotZ);

            var go = Object.Instantiate(prefab, worldPos, worldRot);
            go.name = $"{prefab.name}(Cargo)";

            // Ensure identity is set to match snapshot (stable per-item)
            var id = go.GetComponent<CargoItemIdentity>();
            if (id == null) id = go.AddComponent<CargoItemIdentity>();
            id.ForceAssign(s.instanceGuid, s.typeGuid);

            if (s.secured)
            {
                // Secured: parent to boat root so it always travels.
                go.transform.SetParent(boatRoot, true);

                // Optional: disable physics to prevent jitter while parented.
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
                // Unsecured: MUST NOT be parented (otherwise physics + parent motion gets weird).
                go.transform.SetParent(null, true);

                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                }
            }
        }
    }

    private static float GetLocalRotZ(Transform boatRoot, Transform item)
    {
        // 2D local rotation relative to boat root.
        float worldZ = item.rotation.eulerAngles.z;
        float boatZ = boatRoot.rotation.eulerAngles.z;
        return Mathf.DeltaAngle(boatZ, worldZ);
    }
}