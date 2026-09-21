using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatModuleStatePersistence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    [Tooltip("Optional explicit/fallback module lookup for save/load. " +
             "Normal inventory-linked modules are also discovered automatically through ItemDefinitionCatalog.")]
    [SerializeField] private ModuleDefinition[] moduleDefinitions;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public ItemDefinitionCatalog ItemCatalog => itemCatalog;

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
            }

            manifest.modules.Add(snap);
        }

        CaptureHelmLinks(
            manifest);

        Log(
            $"Captured module state manifest. modules={manifest.modules.Count}, " +
            $"helmLinks={(manifest.helmLinks != null ? manifest.helmLinks.Count : 0)}");

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
                Log($"Restored StorageModule contents/cargo rack on '{snap.hardpointId}'.");

                // Storage restore replaces the ItemContainerState object. A tether
                // deployment module may still be subscribed to the previous container
                // until its first Update, but BoatSpawner restores tether state and
                // loose BellItems before that Update can run. Reconcile the stowed
                // physical payload shell immediately so dependent restores can resolve it.
                if (installed.TryGetComponent(out TetherDeploymentModule tetherDeployment))
                {
                    tetherDeployment.RefreshStoredPayloadAfterStorageRestore();
                    Log($"Refreshed tether stored-payload runtime on '{snap.hardpointId}' after storage restore.");
                }
            }
        }

        RestoreHelmLinks(
            manifest,
            hardpoints);
    }

    public void RestoreAll(BoatModuleStateManifest moduleManifest, BoatPowerSnapshot powerSnapshot)
    {
        // Power first so engines/turrets using boat power can turn back on successfully.
        RestorePowerSnapshot(powerSnapshot);
        RestoreModuleManifest(moduleManifest);
    }

    private void CaptureHelmLinks(
        BoatModuleStateManifest manifest)
    {
        if (manifest == null)
            return;

        manifest.version = 2;

        if (manifest.helmLinks == null)
        {
            manifest.helmLinks =
                new List<BoatHelmLinkSnapshot>();
        }
        else
        {
            manifest.helmLinks.Clear();
        }

        PilotChairInteractable[] chairs =
            GetComponentsInChildren<PilotChairInteractable>(
                true);

        for (int i = 0;
             i < chairs.Length;
             i++)
        {
            PilotChairInteractable chair =
                chairs[i];

            if (chair == null ||
                chair.LinkedHelmHardpoint == null)
            {
                continue;
            }

            Hardpoint helmHardpoint =
                chair.LinkedHelmHardpoint;

            string stationId =
                chair.PersistenceStationId;

            if (string.IsNullOrWhiteSpace(
                    stationId) ||
                string.IsNullOrWhiteSpace(
                    helmHardpoint.HardpointId))
            {
                LogWarning(
                    $"Skipping Helm link capture for chair '{chair.name}': " +
                    "missing station or hardpoint persistence ID.");
                continue;
            }

            int order =
                GetPilotControllerOrder(
                    helmHardpoint,
                    chair);

            if (order < 0)
            {
                LogWarning(
                    $"Skipping Helm link capture for chair '{chair.name}': " +
                    $"Helm hardpoint '{helmHardpoint.HardpointId}' does not list it as a controller.");
                continue;
            }

            manifest.helmLinks.Add(
                new BoatHelmLinkSnapshot
                {
                    version = 1,
                    pilotStationId =
                        stationId,
                    helmHardpointId =
                        helmHardpoint.HardpointId,
                    controllerOrder =
                        order
                });
        }
    }

    private void RestoreHelmLinks(
        BoatModuleStateManifest manifest,
        Hardpoint[] hardpoints)
    {
        if (manifest == null)
            return;

        // Backward compatibility:
        // v1 saves predate runtime Helm wiring. Preserve whatever authored
        // links exist on the boat prefab rather than treating absence as an
        // authoritative "disconnect everything."
        if (manifest.version < 2)
        {
            Log(
                $"Skipping Helm link restore for legacy module manifest v{manifest.version}.");

            return;
        }

        PilotChairInteractable[] chairs =
            GetComponentsInChildren<PilotChairInteractable>(
                true);

        // v2+ list is authoritative, including an intentionally empty list.
        for (int i = 0;
             i < chairs.Length;
             i++)
        {
            if (chairs[i] != null &&
                chairs[i].LinkedHelmHardpoint != null)
            {
                chairs[i].UnlinkHelm();
            }
        }

        if (manifest.helmLinks == null ||
            manifest.helmLinks.Count == 0)
        {
            Log(
                "Restored Helm links. Saved wiring is empty.");

            return;
        }

        List<BoatHelmLinkSnapshot> links =
            new List<BoatHelmLinkSnapshot>();

        for (int i = 0;
             i < manifest.helmLinks.Count;
             i++)
        {
            BoatHelmLinkSnapshot link =
                manifest.helmLinks[i];

            if (link != null)
                links.Add(link);
        }

        links.Sort(
            CompareHelmLinksForRestore);

        int restored =
            0;

        for (int i = 0;
             i < links.Count;
             i++)
        {
            BoatHelmLinkSnapshot link =
                links[i];

            if (string.IsNullOrWhiteSpace(
                    link.pilotStationId) ||
                string.IsNullOrWhiteSpace(
                    link.helmHardpointId))
            {
                continue;
            }

            PilotChairInteractable chair =
                FindPilotChairByPersistenceId(
                    chairs,
                    link.pilotStationId);

            if (chair == null)
            {
                LogWarning(
                    $"No PilotChair found for saved station id='{link.pilotStationId}'. " +
                    "Skipping Helm link.");
                continue;
            }

            Hardpoint helmHardpoint =
                FindHardpointById(
                    hardpoints,
                    link.helmHardpointId);

            if (helmHardpoint == null)
            {
                LogWarning(
                    $"No Hardpoint found for saved Helm id='{link.helmHardpointId}'. " +
                    $"Skipping station '{link.pilotStationId}'.");
                continue;
            }

            if (!chair.TryLinkToHelm(
                    helmHardpoint))
            {
                LogWarning(
                    $"Failed to restore station '{link.pilotStationId}' -> " +
                    $"Helm '{link.helmHardpointId}'.");
                continue;
            }

            restored++;
        }

        Log(
            $"Restored Helm links. restored={restored}, saved={links.Count}");
    }

    private static int GetPilotControllerOrder(
        Hardpoint hardpoint,
        PilotChairInteractable chair)
    {
        if (hardpoint == null ||
            chair == null ||
            hardpoint.Controllers == null)
        {
            return -1;
        }

        int pilotOrder =
            0;

        IReadOnlyList<MonoBehaviour> controllers =
            hardpoint.Controllers;

        for (int i = 0;
             i < controllers.Count;
             i++)
        {
            if (controllers[i] is not PilotChairInteractable candidate)
                continue;

            if (ReferenceEquals(
                    candidate,
                    chair))
            {
                return pilotOrder;
            }

            pilotOrder++;
        }

        return -1;
    }

    private static int CompareHelmLinksForRestore(
        BoatHelmLinkSnapshot a,
        BoatHelmLinkSnapshot b)
    {
        if (ReferenceEquals(a, b))
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int helmCompare =
            string.Compare(
                a.helmHardpointId,
                b.helmHardpointId,
                System.StringComparison.Ordinal);

        if (helmCompare != 0)
            return helmCompare;

        int orderCompare =
            a.controllerOrder.CompareTo(
                b.controllerOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(
            a.pilotStationId,
            b.pilotStationId,
            System.StringComparison.Ordinal);
    }

    private static PilotChairInteractable FindPilotChairByPersistenceId(
        PilotChairInteractable[] chairs,
        string stationId)
    {
        if (chairs == null ||
            string.IsNullOrWhiteSpace(
                stationId))
        {
            return null;
        }

        for (int i = 0;
             i < chairs.Length;
             i++)
        {
            PilotChairInteractable chair =
                chairs[i];

            if (chair == null)
                continue;

            if (string.Equals(
                    chair.PersistenceStationId,
                    stationId,
                    System.StringComparison.Ordinal))
            {
                return chair;
            }
        }

        return null;
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
                "Ensure its ItemDefinition is in ItemDefinitionCatalog, or add the ModuleDefinition to the explicit fallback list.");
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

        // Keep the old explicit list as an override/fallback for unusual modules that
        // are not represented by an inventory ItemDefinition.
        if (moduleDefinitions != null)
        {
            for (int i = 0; i < moduleDefinitions.Length; i++)
            {
                ModuleDefinition def = moduleDefinitions[i];
                if (def == null)
                    continue;

                if (string.Equals(def.ModuleId, moduleId, System.StringComparison.Ordinal))
                    return def;
            }
        }

        // Normal installable modules already have ItemDefinitions and those items live
        // in the ItemDefinitionCatalog. Use that as the authoritative general lookup so
        // adding a new rudder/keel/anchor does not also require maintaining a second
        // inspector array just for persistence.
        if (itemCatalog != null)
        {
            IReadOnlyList<ItemDefinition> items = itemCatalog.GetAllItems();

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ItemDefinition item = items[i];
                    if (item == null || !item.IsModule)
                        continue;

                    ModuleDefinition def = item.ModuleDefinition;
                    if (def == null)
                        continue;

                    if (string.Equals(def.ModuleId, moduleId, System.StringComparison.Ordinal))
                        return def;
                }
            }
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