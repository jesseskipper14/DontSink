using System.Collections.Generic;
using UnityEngine;

public sealed class BoatSpawner : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private string spawnTag = "Respawn";
    [SerializeField] private Vector3 fallbackSpawn = Vector3.zero;

    [Header("Catalogs")]
    [SerializeField] private BoatCatalog boatCatalog;
    [SerializeField] private TradeCargoPrefabCatalog tradeCargoPrefabCatalog;

    [Header("Fallback")]
    [SerializeField] private GameObject defaultBoatPrefab;

    private void Start()
    {
        Log("Start BEGIN");

        GameState gs = GameState.I;
        Log($"GameState.I={(gs != null ? gs.name : "NULL")}");

        if (gs != null)
            gs.LogState("BoatSpawner.Start BEGIN");

        Transform sp = ResolveSpawnPointSafe();
        Vector3 pos = sp != null ? sp.position : fallbackSpawn;

        Log(
            $"Spawn point resolved | spawnPoint={(sp != null ? sp.name : "NULL")} " +
            $"| pos={pos} | fallbackSpawn={fallbackSpawn}");

        string boatGuid = ResolveBoatGuid(gs, out string boatGuidSource);
        Log($"Resolved desired boatGuid='{boatGuid}' from source='{boatGuidSource}'");

        GameObject prefab = ResolveBoatPrefab(boatGuid, out string prefabSource);

        if (prefab == null)
        {
            LogError(
                "No boat prefab resolved. " +
                $"boatGuid='{boatGuid}', source='{boatGuidSource}', " +
                $"boatCatalog={(boatCatalog != null ? boatCatalog.name : "NULL")}, " +
                $"defaultBoatPrefab={(defaultBoatPrefab != null ? defaultBoatPrefab.name : "NULL")}");

            return;
        }

        Log($"Spawning prefab='{prefab.name}' from prefabSource='{prefabSource}' at pos={pos}");

        GameObject boatGO = Instantiate(prefab, pos, Quaternion.identity);
        boatGO.name = "PlayerBoat(Clone)";

        Log($"Instantiated boatGO='{boatGO.name}' prefabSource='{prefabSource}'");

        AssignBoatIdentity(gs, boatGO);
        RestoreBoatTransform(gs, boatGO);
        RestoreModulesAndPower(gs, boatGO);
        RestoreCompartments(gs, boatGO);
        RestoreAccessStates(gs, boatGO);
        RestoreCargo(gs, boatGO);
        RestoreLooseItems(gs, boatGO);
        RefreshAutoRegistration(boatGO);

        if (gs != null)
            gs.LogState("BoatSpawner.Start END");

        Log("Start END");
    }

    private string ResolveBoatGuid(GameState gs, out string source)
    {
        source = "none";

        if (gs == null)
        {
            LogWarning("ResolveBoatGuid: GameState is NULL. Will fall back.");
            return null;
        }

        if (gs.activeTravel != null)
        {
            source = "GameState.activeTravel.boatPrefabGuid";

            string guid = gs.activeTravel.boatPrefabGuid;

            if (string.IsNullOrWhiteSpace(guid))
            {
                LogWarning(
                    "ResolveBoatGuid: activeTravel exists but boatPrefabGuid is EMPTY. " +
                    "BoatSpawner will likely use fallback unless saved boat handling is added here.");
            }

            return guid;
        }

        source = "GameState.boat.boatPrefabGuid";

        if (gs.boat == null)
        {
            LogWarning("ResolveBoatGuid: GameState.boat is NULL. Will fall back.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(gs.boat.boatPrefabGuid))
            LogWarning("ResolveBoatGuid: saved GameState.boat.boatPrefabGuid is EMPTY. Will fall back.");

        return gs.boat.boatPrefabGuid;
    }

    private GameObject ResolveBoatPrefab(string boatGuid, out string source)
    {
        source = "none";

        if (boatCatalog == null)
        {
            LogWarning("ResolveBoatPrefab: boatCatalog is NULL. Cannot resolve by GUID.");
        }
        else if (string.IsNullOrWhiteSpace(boatGuid))
        {
            LogWarning("ResolveBoatPrefab: boatGuid is empty. Skipping catalog lookup.");
        }
        else
        {
            Log($"ResolveBoatPrefab: trying catalog lookup for boatGuid='{boatGuid}'.");

            GameObject resolved = boatCatalog.Resolve(boatGuid);
            if (resolved != null)
            {
                source = $"BoatCatalog.Resolve('{boatGuid}')";
                Log($"ResolveBoatPrefab: catalog resolved prefab='{resolved.name}'.");
                return resolved;
            }

            LogWarning($"ResolveBoatPrefab: catalog failed to resolve boatGuid='{boatGuid}'.");
        }

        if (defaultBoatPrefab != null)
        {
            source = "defaultBoatPrefab fallback";
            LogWarning($"ResolveBoatPrefab: using fallback defaultBoatPrefab='{defaultBoatPrefab.name}'.");
            return defaultBoatPrefab;
        }

        source = "failed";
        return null;
    }

    private void AssignBoatIdentity(GameState gs, GameObject boatGO)
    {
        if (boatGO == null)
            return;

        Boat boat = boatGO.GetComponent<Boat>();
        BoatIdentity identity = boatGO.GetComponent<BoatIdentity>();

        Log(
            $"AssignBoatIdentity BEGIN | boatComponent={(boat != null ? "OK" : "NULL")} " +
            $"| identity={(identity != null ? identity.BoatGuid : "NULL")}");

        if (gs == null)
        {
            LogWarning("AssignBoatIdentity skipped because GameState is NULL.");
            return;
        }

        string desiredInstanceId = ResolveBoatInstanceId(gs, out string instanceIdSource);

        if (boat != null)
        {
            if (!string.IsNullOrWhiteSpace(desiredInstanceId))
            {
                Log($"AssignBoatIdentity: setting BoatInstanceId='{desiredInstanceId}' from source='{instanceIdSource}'.");
                boat.SetBoatInstanceId(desiredInstanceId);
            }
            else
            {
                LogWarning("AssignBoatIdentity: desired BoatInstanceId is empty. Leaving boat instance id unchanged.");
            }
        }
        else
        {
            LogWarning("AssignBoatIdentity: spawned prefab has no Boat component.");
        }

        if (identity != null)
        {
            string previousGuid = gs.boat != null ? gs.boat.boatPrefabGuid : "NULL_BOAT_STATE";

            if (gs.boat != null)
                gs.boat.boatPrefabGuid = identity.BoatGuid;

            Log(
                $"AssignBoatIdentity: remembered spawned BoatIdentity guid. " +
                $"previous gs.boat.boatPrefabGuid='{previousGuid}', new='{identity.BoatGuid}'.");
        }
        else
        {
            LogWarning(
                "AssignBoatIdentity: spawned prefab has no BoatIdentity. " +
                "GameState.boat.boatPrefabGuid was not updated from spawned prefab.");
        }

        Log("AssignBoatIdentity END");
    }

    private void RestoreBoatTransform(GameState gs, GameObject boatGO)
    {
        Log("RestoreBoatTransform BEGIN");

        if (gs == null)
        {
            LogWarning("RestoreBoatTransform skipped: GameState is NULL.");
            return;
        }

        if (boatGO == null)
        {
            LogWarning("RestoreBoatTransform skipped: boatGO is NULL.");
            return;
        }

        BoatTransformSnapshot snapshot = gs.boat != null ? gs.boat.transformState : null;
        if (snapshot == null)
        {
            Log("RestoreBoatTransform skipped: no saved transform state.");
            return;
        }

        Vector3 p = boatGO.transform.position;
        p.y = snapshot.worldY;
        boatGO.transform.position = p;

        Rigidbody2D rb = boatGO.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Prevent inherited spawn/drop momentum from causing the freshly restored boat
            // to belly-flop itself into the sea like a dramatic idiot.
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        Log($"RestoreBoatTransform END | restored worldY={snapshot.worldY:F3}");
    }

    private string ResolveBoatInstanceId(GameState gs, out string source)
    {
        source = "none";

        if (gs == null)
            return null;

        if (gs.activeTravel != null && !string.IsNullOrWhiteSpace(gs.activeTravel.boatInstanceId))
        {
            source = "GameState.activeTravel.boatInstanceId";
            return gs.activeTravel.boatInstanceId;
        }

        if (gs.boat != null && !string.IsNullOrWhiteSpace(gs.boat.boatInstanceId))
        {
            source = "GameState.boat.boatInstanceId";
            return gs.boat.boatInstanceId;
        }

        LogWarning("ResolveBoatInstanceId: no valid boat instance id found.");
        return null;
    }

    private void RestoreCargo(GameState gs, GameObject boatGO)
    {
        Log("RestoreCargo BEGIN");

        if (tradeCargoPrefabCatalog == null)
        {
            LogWarning("RestoreCargo skipped because tradeCargoPrefabCatalog is NULL.");
            return;
        }

        if (gs == null)
        {
            LogWarning("RestoreCargo skipped because GameState is NULL.");
            return;
        }

        if (boatGO == null)
        {
            LogWarning("RestoreCargo skipped because boatGO is NULL.");
            return;
        }

        List<CargoManifest.Snapshot> manifest = null;
        string manifestSource = "none";

        if (gs.activeTravel != null && gs.activeTravel.cargoManifest != null)
        {
            manifest = gs.activeTravel.cargoManifest;
            manifestSource = "GameState.activeTravel.cargoManifest";
        }
        else if (gs.boat != null)
        {
            manifest = gs.boat.cargo;
            manifestSource = "GameState.boat.cargo";
        }

        Log(
            $"RestoreCargo resolved manifestSource='{manifestSource}' " +
            $"| count={(manifest != null ? manifest.Count : -1)}");

        if (manifest != null && manifest.Count > 0)
        {
            Log($"RestoreCargo: restoring {manifest.Count} cargo snapshots.");
            CargoManifest.Restore(boatGO.transform, manifest, tradeCargoPrefabCatalog);
        }
        else
        {
            Log("RestoreCargo: no cargo to restore.");
        }

        Log("RestoreCargo END");
    }

    private void RestoreModulesAndPower(GameState gs, GameObject boatGO)
    {
        Log("RestoreModulesAndPower BEGIN");

        if (gs == null)
        {
            LogWarning("RestoreModulesAndPower skipped: GameState is NULL.");
            return;
        }

        if (boatGO == null)
        {
            LogWarning("RestoreModulesAndPower skipped: boatGO is NULL.");
            return;
        }

        BoatModuleStatePersistence persistence = boatGO.GetComponent<BoatModuleStatePersistence>();
        if (persistence == null)
        {
            LogWarning($"RestoreModulesAndPower skipped: spawned boat '{boatGO.name}' has no BoatModuleStatePersistence.");
            return;
        }

        BoatModuleStateManifest moduleManifest = gs.boat != null ? gs.boat.moduleStates : null;
        BoatPowerSnapshot powerSnapshot = gs.boat != null ? gs.boat.power : null;

        int moduleCount = moduleManifest?.modules != null ? moduleManifest.modules.Count : -1;

        Log(
            $"RestoreModulesAndPower resolved | moduleCount={moduleCount} " +
            $"| power={(powerSnapshot != null ? $"{powerSnapshot.currentPower:F1}/{powerSnapshot.maxPower:F1}" : "NULL")}");

        persistence.RestoreAll(moduleManifest, powerSnapshot);

        Log("RestoreModulesAndPower END");
    }

    private void RestoreCompartments(GameState gs, GameObject boatGO)
    {
        Log("RestoreCompartments BEGIN");

        if (gs == null)
        {
            LogWarning("RestoreCompartments skipped: GameState is NULL.");
            return;
        }

        if (boatGO == null)
        {
            LogWarning("RestoreCompartments skipped: boatGO is NULL.");
            return;
        }

        BoatCompartmentStatePersistence persistence = boatGO.GetComponent<BoatCompartmentStatePersistence>();
        if (persistence == null)
        {
            LogWarning($"RestoreCompartments skipped: spawned boat '{boatGO.name}' has no BoatCompartmentStatePersistence.");
            return;
        }

        BoatCompartmentStateManifest manifest = gs.boat != null ? gs.boat.compartmentStates : null;
        int count = manifest?.compartments != null ? manifest.compartments.Count : -1;

        Log($"RestoreCompartments resolved | count={count}");

        persistence.RestoreManifest(manifest);

        Log("RestoreCompartments END");
    }


    private void RestoreAccessStates(GameState gs, GameObject boatGO)
    {
        Log("RestoreAccessStates BEGIN");

        if (gs == null)
        {
            LogWarning("RestoreAccessStates skipped: GameState is NULL.");
            return;
        }

        if (boatGO == null)
        {
            LogWarning("RestoreAccessStates skipped: boatGO is NULL.");
            return;
        }

        BoatAccessStatePersistence persistence = boatGO.GetComponent<BoatAccessStatePersistence>();
        if (persistence == null)
        {
            LogWarning($"RestoreAccessStates skipped: spawned boat '{boatGO.name}' has no BoatAccessStatePersistence.");
            return;
        }

        BoatAccessStateManifest manifest = gs.boat != null ? gs.boat.accessStates : null;
        int count = manifest?.accessPoints != null ? manifest.accessPoints.Count : -1;

        Log($"RestoreAccessStates resolved | count={count}");

        persistence.RestoreManifest(manifest);

        BoatVisualStateController visuals = boatGO.GetComponent<BoatVisualStateController>();
        if (visuals != null)
            visuals.ApplyMode(BoatVisibilityMode.UnboardedExterior);

        Log("RestoreAccessStates END");
    }

    private void RestoreLooseItems(GameState gs, GameObject boatGO)
    {
        Log("RestoreLooseItems BEGIN");

        if (gs == null)
        {
            LogWarning("RestoreLooseItems skipped because GameState is NULL.");
            return;
        }

        if (boatGO == null)
        {
            LogWarning("RestoreLooseItems skipped because boatGO is NULL.");
            return;
        }

        BoatLooseItemManifest manifest = gs.boat != null ? gs.boat.looseItems : null;

        Log(
            $"RestoreLooseItems resolved manifest={(manifest != null ? "OK" : "NULL")} " +
            $"| count={(manifest?.looseItems != null ? manifest.looseItems.Count : -1)}");

        if (manifest == null || manifest.looseItems == null || manifest.looseItems.Count == 0)
        {
            Log("RestoreLooseItems: no loose items to restore.");
            return;
        }

        BoatLooseItemPersistence persistence = boatGO.GetComponent<BoatLooseItemPersistence>();
        if (persistence == null)
        {
            LogWarning(
                $"RestoreLooseItems skipped because spawned boat '{boatGO.name}' has no BoatLooseItemPersistence.");
            return;
        }

        persistence.RestoreManifest(manifest);

        Log("RestoreLooseItems END");
    }

    private void RefreshAutoRegistration(GameObject boatGO)
    {
        if (boatGO == null)
            return;

        BoatAutoRegister auto = boatGO.GetComponent<BoatAutoRegister>();

        if (auto == null)
        {
            LogWarning("RefreshAutoRegistration: spawned boat has no BoatAutoRegister.");
            return;
        }

        Log("RefreshAutoRegistration: calling BoatAutoRegister.RefreshRegistration().");
        auto.RefreshRegistration();
    }

    private Transform ResolveSpawnPointSafe()
    {
        if (spawnPoint != null)
        {
            Log($"ResolveSpawnPointSafe: using assigned spawnPoint='{spawnPoint.name}'.");
            return spawnPoint;
        }

        if (string.IsNullOrEmpty(spawnTag))
        {
            LogWarning("ResolveSpawnPointSafe: spawnTag is empty. Using fallbackSpawn.");
            return null;
        }

        try
        {
            Log($"ResolveSpawnPointSafe: looking for object with tag='{spawnTag}'.");

            GameObject go = GameObject.FindGameObjectWithTag(spawnTag);
            if (go != null)
            {
                spawnPoint = go.transform;
                Log($"ResolveSpawnPointSafe: found spawnPoint='{spawnPoint.name}'.");
                return spawnPoint;
            }

            LogWarning($"ResolveSpawnPointSafe: no object found with tag '{spawnTag}'. Using fallbackSpawn.");
            return null;
        }
        catch (UnityException e)
        {
            LogError($"ResolveSpawnPointSafe: tag lookup failed for '{spawnTag}'. Add it in Tags & Layers. Exception: {e.Message}");
            return null;
        }
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatSpawner:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[BoatSpawner:{name}] {msg}", this);
    }

    private void LogError(string msg)
    {
        Debug.LogError($"[BoatSpawner:{name}] {msg}", this);
    }
}