using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatModuleStatePersistence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

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

            var snap = new BoatModuleStateSnapshot
            {
                version = 1,
                hardpointId = hp.HardpointId,
                moduleId = installed.Definition != null ? installed.Definition.ModuleId : null,
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

        for (int i = 0; i < manifest.modules.Count; i++)
        {
            BoatModuleStateSnapshot snap = manifest.modules[i];
            if (snap == null || string.IsNullOrWhiteSpace(snap.hardpointId))
                continue;

            Hardpoint hp = FindHardpointById(hardpoints, snap.hardpointId);
            if (hp == null)
            {
                LogWarning($"No hardpoint found for saved id='{snap.hardpointId}'. Skipping module state.");
                continue;
            }

            if (!hp.HasInstalledModule || hp.InstalledModule == null)
            {
                LogWarning($"Hardpoint '{snap.hardpointId}' has no installed module. Skipping saved state.");
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
                Log($"Installed module on '{snap.hardpointId}' has no supported persistence component.");
            }
        }
    }

    public void RestoreAll(BoatModuleStateManifest moduleManifest, BoatPowerSnapshot powerSnapshot)
    {
        // Power first so engines/turrets using boat power can turn back on successfully.
        RestorePowerSnapshot(powerSnapshot);
        RestoreModuleManifest(moduleManifest);
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