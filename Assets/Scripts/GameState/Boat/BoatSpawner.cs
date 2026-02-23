using UnityEngine;

public sealed class BoatSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private string spawnTag = "Respawn";
    [SerializeField] private Vector3 fallbackSpawn = Vector3.zero;

    [Header("Catalogs")]
    [SerializeField] private BoatCatalog boatCatalog;
    [SerializeField] private CargoCatalog cargoCatalog;

    [Header("Fallback")]
    [SerializeField] private GameObject defaultBoatPrefab;

    private void Start()
    {
        var gs = GameState.I;

        Transform sp = ResolveSpawnPointSafe();
        Vector3 pos = sp != null ? sp.position : fallbackSpawn;

        // Decide which boat GUID we should spawn:
        // - if traveling: use payload boatPrefabGuid
        // - else: use saved boatPrefabGuid
        string boatGuid =
            gs != null && gs.activeTravel != null ? gs.activeTravel.boatPrefabGuid :
            gs != null ? gs.boat.boatPrefabGuid : null;

        GameObject prefab = null;

        if (boatCatalog != null && !string.IsNullOrEmpty(boatGuid))
            prefab = boatCatalog.Resolve(boatGuid);

        if (prefab == null)
            prefab = defaultBoatPrefab;

        if (prefab == null)
        {
            Debug.LogError("[BoatSpawner] No boat prefab resolved (catalog + fallback are null).");
            return;
        }

        var boatGO = Instantiate(prefab, pos, Quaternion.identity);
        boatGO.name = "PlayerBoat(Clone)";

        // Assign instance id if available
        if (gs != null)
        {
            var boat = boatGO.GetComponent<Boat>();
            if (boat != null && !string.IsNullOrEmpty(gs.boat.boatInstanceId))
                boat.SetBoatInstanceId(gs.boat.boatInstanceId);

            // Ensure we remember the boat prefab GUID we actually spawned
            var id = boatGO.GetComponent<BoatIdentity>();
            if (id != null)
                gs.boat.boatPrefabGuid = id.BoatGuid;
        }

        // Restore cargo
        if (cargoCatalog != null && gs != null)
        {
            var manifest =
                gs.activeTravel != null && gs.activeTravel.cargoManifest != null ? gs.activeTravel.cargoManifest :
                gs.boat.cargo;

            if (manifest != null && manifest.Count > 0)
                CargoManifest.Restore(boatGO.transform, manifest, cargoCatalog);
        }

        // If using auto-register, refresh after id assignment (optional)
        var auto = boatGO.GetComponent<BoatAutoRegister>();
        if (auto != null) auto.RefreshRegistration();
    }

    private Transform ResolveSpawnPointSafe()
    {
        if (spawnPoint != null) return spawnPoint;

        if (string.IsNullOrEmpty(spawnTag))
            return null;

        try
        {
            var go = GameObject.FindGameObjectWithTag(spawnTag);
            if (go != null)
            {
                spawnPoint = go.transform;
                return spawnPoint;
            }

            Debug.LogWarning($"[BoatSpawner] No object found with tag '{spawnTag}'.");
            return null;
        }
        catch (UnityException e)
        {
            Debug.LogError($"[BoatSpawner] Tag lookup failed for '{spawnTag}'. Add it in Tags & Layers. Exception: {e.Message}");
            return null;
        }
    }
}