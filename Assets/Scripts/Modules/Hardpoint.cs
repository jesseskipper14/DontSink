using UnityEngine;

[DisallowMultipleComponent]
public sealed class Hardpoint : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string hardpointId = "engine_01";
    [SerializeField] private HardpointType hardpointType = HardpointType.Engine;

    [Header("Mount")]
    [SerializeField] private Transform mountPoint;

    [Header("Runtime")]
    [SerializeField] private InstalledModule installedModule;

    public string HardpointId => hardpointId;
    public HardpointType HardpointType => hardpointType;
    public Transform MountPoint => mountPoint != null ? mountPoint : transform;
    public InstalledModule InstalledModule => installedModule;
    public bool HasInstalledModule => installedModule != null;

    public bool CanInstall(ModuleDefinition moduleDefinition)
    {
        if (moduleDefinition == null)
            return false;

        if (HasInstalledModule)
            return false;

        return moduleDefinition.CanInstallOn(hardpointType);
    }

    public bool TryInstall(ModuleDefinition moduleDefinition, out InstalledModule spawnedModule)
    {
        spawnedModule = null;

        if (!CanInstall(moduleDefinition))
            return false;

        if (moduleDefinition.InstalledPrefab == null)
        {
            Debug.LogWarning($"[BoatHardpoint] Module '{moduleDefinition.DisplayName}' has no installed prefab.", this);
            return false;
        }

        GameObject go = Instantiate(
            moduleDefinition.InstalledPrefab,
            MountPoint.position,
            MountPoint.rotation);

        go.transform.SetParent(MountPoint, worldPositionStays: true);

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = false;
        }

        InstalledModule module = go.GetComponent<InstalledModule>();
        if (module == null)
            module = go.AddComponent<InstalledModule>();

        module.Initialize(moduleDefinition, this);
        installedModule = module;

        EngineModule engine = go.GetComponent<EngineModule>();
        if (engine != null)
            engine.InitializeFuel();

        PumpModule pump = go.GetComponent<PumpModule>();
        if (pump != null)
            pump.ResolveTargetCompartment();

        GeneratorModule generator = go.GetComponent<GeneratorModule>();
        if (generator != null)
        {
            generator.InitializeFuel();
            generator.ResolveOwnership();
        }

        return true;
    }

    public bool TryRemove(out ModuleDefinition removedDefinition)
    {
        removedDefinition = null;

        if (!HasInstalledModule)
            return false;

        removedDefinition = installedModule.Definition;

        if (installedModule != null)
            Destroy(installedModule.gameObject);

        installedModule = null;
        return removedDefinition != null;
    }
}