using UnityEngine;

public sealed class BoatSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private string spawnTag = "Respawn";
    [SerializeField] private Vector3 fallbackSpawn = Vector3.zero;

    [Header("Boat Prefabs")]
    [SerializeField] private GameObject defaultBoatPrefab;

    private void Awake()
    {
        Debug.Log($"[BoatSpawner] Awake | enabled={enabled} | activeInHierarchy={gameObject.activeInHierarchy}");
    }

    private void OnEnable()
    {
        Debug.Log($"[BoatSpawner] OnEnable");
    }

    private void Start()
    {
        Debug.Log("[BoatSpawner] Start");

        var gs = GameState.I;
        Debug.Log($"[BoatSpawner] GameState.I is {(gs != null ? "present" : "NULL")}");

        if (defaultBoatPrefab == null)
        {
            Debug.LogError("[BoatSpawner] defaultBoatPrefab is NULL. Assign it in inspector.");
            return;
        }

        Transform sp = ResolveSpawnPointSafe();
        Vector3 pos = sp != null ? sp.position : fallbackSpawn;

        Debug.Log($"[BoatSpawner] Spawning at {pos} | spawnPoint={(sp != null ? sp.name : "NULL (fallback)")}");

        var go = Instantiate(defaultBoatPrefab, pos, Quaternion.identity);
        go.name = "PlayerBoat(Clone)";
        Debug.Log($"[BoatSpawner] Instantiated '{go.name}' active={go.activeInHierarchy}");

        // Assign ID if available
        if (gs != null && gs.boat != null)
        {
            var boat = go.GetComponent<Boat>();
            if (boat != null && !string.IsNullOrEmpty(gs.boat.boatInstanceId))
            {
                boat.SetBoatInstanceId(gs.boat.boatInstanceId);
                Debug.Log($"[BoatSpawner] Assigned BoatInstanceId='{boat.BoatInstanceId}'");
            }
            else
            {
                Debug.LogWarning("[BoatSpawner] Boat missing OR boatInstanceId missing.");
            }
        }

        // If using auto-register, refresh after id assignment (optional)
        var auto = go.GetComponent<BoatAutoRegister>();
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
            // This happens if the tag doesn't exist in Tag Manager.
            Debug.LogError($"[BoatSpawner] Tag lookup failed for '{spawnTag}'. Add it in Tags & Layers. Exception: {e.Message}");
            return null;
        }
    }
}
