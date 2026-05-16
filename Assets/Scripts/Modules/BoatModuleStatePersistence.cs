using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatModuleStatePersistence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    [Tooltip("Temporary/simple module lookup for save/load. Add every ModuleDefinition that can be installed on this boat.")]
    [SerializeField] private ModuleDefinition[] moduleDefinitions;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private void Awake()
    {
        if (boat == null)
            boat = GetComponent<Boat>();
    }

    public BoatModuleStateManifest CaptureModuleManifest()
    {
        var manifest = new BoatModuleStateManifest();

        Hardpoint[] hardpoints = GetComponentsInChildren<Hardpoint>(true);

        for (int i = 0; i < hardpoints.Length; i++)
        {
            Hardpoint hp = hardpoints[i];
            if (hp == null || !hp.HasInstalledModule || hp.InstalledModule == null)
                continue;

            InstalledModule installed = hp.InstalledModule;

            string moduleId = installed.Definition != null
                ? installed.Definition.ModuleId
                : null;

            if (string.IsNullOrWhiteSpace(moduleId))
            {
                LogWarning(
                    $"Hardpoint '{hp.HardpointId}' has installed module '{installed.name}' but its Definition/ModuleId is missing. " +
                    "Snapshot will be incomplete.");
            }

            var snap = new BoatModuleStateSnapshot
            {
                version = 1,
                hardpointId = hp.HardpointId,
                moduleId = moduleId,
                isOn = false,
                fuelContainer = null
            };

            if (installed.TryGetComponent(out GeneratorModule generator))
            {
                snap.isOn = generator.IsOn;
                snap.fuelContainer = generator.CaptureFuelContainerSnapshot();
            }
            else if (installed.TryGetComponent(out EngineModule engine))
            {
                snap.isOn = engine.IsOn;
                snap.fuelContainer = engine.CaptureFuelContainerSnapshot();
            }
            else if (installed.TryGetComponent(out PumpModule pump))
            {
                snap.isOn = pump.IsOn;
            }
            else if (installed.TryGetComponent(out TurretModule turret))
            {
                snap.isOn = turret.IsOn;
            }

            if (installed.TryGetComponent(out StorageModule storage))
            {
                snap.storageContainer = storage.CaptureContainerSnapshot();
                snap.cargoRack = storage.CaptureCargoRackSnapshot();
            }

            manifest.modules.Add(snap);
        }

        Log($"Captured module state manifest. count={manifest.modules.Count}");
        return manifest;
    }

    public BoatPowerSnapshot CapturePowerSnapshot()
    {
        BoatPowerState power = GetComponent<BoatPowerState>();
        if (power == null)
        {
            Log("CapturePowerSnapshot skipped: no BoatPowerState.");
            return null;
        }

        return new BoatPowerSnapshot
        {
            version = 1,
            currentPower = power.CurrentPower,
            maxPower = power.MaxPower
        };
    }

    public void RestorePowerSnapshot(BoatPowerSnapshot snapshot)
    {
        if (snapshot == null)
        {
            Log("RestorePowerSnapshot skipped: snapshot null.");
            return;
        }

        if (snapshot.maxPower <= 0f)
        {
            Log(
                $"RestorePowerSnapshot skipped: invalid/uninitialized maxPower={snapshot.maxPower:F2}. " +
                "Keeping prefab BoatPowerState defaults.");

            return;
        }

        BoatPowerState power = GetComponent<BoatPowerState>();
        if (power == null)
            power = gameObject.AddComponent<BoatPowerState>();

        power.SetMaxPower(snapshot.maxPower);
        power.SetCurrentPower(snapshot.currentPower);

        Log($"Restored power. current={snapshot.currentPower:F2}, max={snapshot.maxPower:F2}");
    }

    public void RestoreModuleManifest(BoatModuleStateManifest manifest)
    {
        if (manifest == null || manifest.modules == null)
        {
            Log("RestoreModuleManifest skipped: manifest/list null.");
            return;
        }

        if (itemCatalog == null)
        {
            Debug.LogError($"[BoatModuleStatePersistence:{name}] Missing ItemDefinitionCatalog.", this);
            return;
        }

        Hardpoint[] hardpoints = GetComponentsInChildren<Hardpoint>(true);

        Dictionary<string, BoatModuleStateSnapshot> savedByHardpointId =
            new Dictionary<string, BoatModuleStateSnapshot>();

        for (int i = 0; i < manifest.modules.Count; i++)
        {
            BoatModuleStateSnapshot snap = manifest.modules[i];

            if (snap == null || string.IsNullOrWhiteSpace(snap.hardpointId))
                continue;

            savedByHardpointId[snap.hardpointId] = snap;
        }

        // Pass 1:
        // The save manifest is authoritative. If a hardpoint has no saved module
        // snapshot, it should be empty after load.
        for (int i = 0; i < hardpoints.Length; i++)
        {
            Hardpoint hp = hardpoints[i];

            if (hp == null || string.IsNullOrWhiteSpace(hp.HardpointId))
                continue;

            if (savedByHardpointId.ContainsKey(hp.HardpointId))
                continue;

            if (!hp.HasInstalledModule || hp.InstalledModule == null)
                continue;

            ModuleDefinition removedDefinition;
            bool removed = hp.TryRemove(out removedDefinition);

            if (removed)
            {
                Log(
                    $"Removed installed module from hardpoint '{hp.HardpointId}' because save manifest has no module snapshot for it.");
            }
            else
            {
                LogWarning(
                    $"Tried to clear hardpoint '{hp.HardpointId}' because save manifest has no module snapshot, but TryRemove failed.");
            }
        }

        // Pass 2:
        // Restore every saved module.
        foreach (KeyValuePair<string, BoatModuleStateSnapshot> pair in savedByHardpointId)
        {
            BoatModuleStateSnapshot snap = pair.Value;

            Hardpoint hp = FindHardpointById(hardpoints, snap.hardpointId);
            if (hp == null)
            {
                LogWarning($"No hardpoint found for saved id='{snap.hardpointId}'. Skipping module state.");
                continue;
            }

            EnsureSavedModuleInstalled(hp, snap);

            if (!hp.HasInstalledModule || hp.InstalledModule == null)
            {
                LogWarning($"Hardpoint '{snap.hardpointId}' still has no installed module after restore attempt. Skipping saved state.");
                continue;
            }

            InstalledModule installed = hp.InstalledModule;

            if (installed.TryGetComponent(out GeneratorModule generator))
            {
                generator.RestorePersistentState(snap.isOn, snap.fuelContainer, itemCatalog);
                Log($"Restored GeneratorModule state on '{snap.hardpointId}'. isOn={snap.isOn}");
            }
            else if (installed.TryGetComponent(out EngineModule engine))
            {
                engine.RestorePersistentState(snap.isOn, snap.fuelContainer, itemCatalog);
                Log($"Restored EngineModule state on '{snap.hardpointId}'. isOn={snap.isOn}");
            }
            else if (installed.TryGetComponent(out PumpModule pump))
            {
                pump.RestorePersistentState(snap.isOn);
                Log($"Restored PumpModule state on '{snap.hardpointId}'. isOn={snap.isOn}");
            }
            else if (installed.TryGetComponent(out TurretModule turret))
            {
                turret.RestorePersistentState(snap.isOn);
                Log($"Restored TurretModule state on '{snap.hardpointId}'. isOn={snap.isOn}");
            }
            else
            {
                // Expected for passive modules like lockers/racks.
                Log($"Installed module on '{snap.hardpointId}' has no active persistence component.");
            }

            if (installed.TryGetComponent(out StorageModule storage))
            {
                storage.RestoreContainerSnapshot(snap.storageContainer, itemCatalog);
                storage.RestoreCargoRackSnapshot(snap.cargoRack);
                Log($"Restored StorageModule contents/cargo rack on '{snap.hardpointId}'.");
            }
        }
    }

    public void RestoreAll(BoatModuleStateManifest moduleManifest, BoatPowerSnapshot powerSnapshot)
    {
        // Power first so engines/turrets using boat power can turn back on successfully.
        RestorePowerSnapshot(powerSnapshot);
        RestoreModuleManifest(moduleManifest);
    }

    private void EnsureSavedModuleInstalled(Hardpoint hp, BoatModuleStateSnapshot snap)
    {
        if (hp == null || snap == null)
            return;

        if (string.IsNullOrWhiteSpace(snap.moduleId))
        {
            LogWarning($"Saved module on hardpoint '{snap.hardpointId}' has no moduleId. Cannot restore install.");
            return;
        }

        if (hp.HasInstalledModule && hp.InstalledModule != null)
        {
            string currentId = hp.InstalledModule.Definition != null
                ? hp.InstalledModule.Definition.ModuleId
                : null;

            if (string.Equals(currentId, snap.moduleId, System.StringComparison.Ordinal))
                return;

            LogWarning(
                $"Hardpoint '{hp.HardpointId}' has installed moduleId='{currentId}', " +
                $"but save expects moduleId='{snap.moduleId}'. Replacing installed module.");

            hp.TryRemove(out _);
        }

        ModuleDefinition module = FindModuleDefinitionById(snap.moduleId);
        if (module == null)
        {
            LogWarning(
                $"Could not resolve ModuleDefinition for moduleId='{snap.moduleId}'. " +
                "Add it to BoatModuleStatePersistence.moduleDefinitions on the boat prefab.");
            return;
        }

        if (!hp.CanInstall(module))
        {
            LogWarning(
                $"Cannot restore module '{module.DisplayName}' to hardpoint '{hp.HardpointId}'. " +
                $"Hardpoint accepts: {hp.GetAcceptedTypesText()}");
            return;
        }

        if (!hp.TryInstall(module, out InstalledModule installed) || installed == null)
        {
            LogWarning(
                $"TryInstall failed while restoring module '{module.DisplayName}' " +
                $"to hardpoint '{hp.HardpointId}'.");
            return;
        }

        Log($"Restored installed module '{module.DisplayName}' on hardpoint '{hp.HardpointId}'.");
    }

    private ModuleDefinition FindModuleDefinitionById(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        if (moduleDefinitions == null)
            return null;

        for (int i = 0; i < moduleDefinitions.Length; i++)
        {
            ModuleDefinition def = moduleDefinitions[i];
            if (def == null)
                continue;

            if (string.Equals(def.ModuleId, moduleId, System.StringComparison.Ordinal))
                return def;
        }

        return null;
    }

    private static Hardpoint FindHardpointById(Hardpoint[] hardpoints, string hardpointId)
    {
        if (hardpoints == null || string.IsNullOrWhiteSpace(hardpointId))
            return null;

        for (int i = 0; i < hardpoints.Length; i++)
        {
            Hardpoint hp = hardpoints[i];
            if (hp != null && hp.HardpointId == hardpointId)
                return hp;
        }

        return null;
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatModuleStatePersistence:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[BoatModuleStatePersistence:{name}] {msg}", this);
    }
}