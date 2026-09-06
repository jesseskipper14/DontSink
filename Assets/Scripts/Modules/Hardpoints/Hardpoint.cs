using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Hardpoint : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string hardpointId = "engine_01";

    [Tooltip("Primary/default accepted type.")]
    [SerializeField] private HardpointType hardpointType = HardpointType.Engine;

    [Tooltip("Optional additional module types this hardpoint accepts.")]
    [SerializeField] private HardpointType[] additionalAcceptedTypes;

    [Header("Mount")]
    [SerializeField] private Transform mountPoint;

    [Tooltip("Anchor point used to align installed module visuals. If null, MountPoint is used.")]
    [SerializeField] private Transform moduleAnchor;

    [Header("Placement Marker")]
    [Tooltip("Contextual 'a module can mount here' sprite. This should be the SpriteRenderer directly on the Hardpoint object, not an installed-module renderer or permanent mounting-rail art.")]
    [SerializeField] private SpriteRenderer placementMarkerRenderer;

    [Header("Controllers")]
    [Tooltip("Optional controller components that can operate the installed module. Priority is array order.")]
    [SerializeField] private MonoBehaviour[] controllers;

    [Header("Builder Starting Module")]
    [Tooltip("Optional module installed automatically for testing/starting boats.")]
    [SerializeField] private ModuleDefinition startingModuleDefinition;

    [SerializeField] private bool installStartingModuleOnAwake = true;

    [Header("Runtime")]
    [SerializeField] private InstalledModule installedModule;

    public string HardpointId => hardpointId;
    public HardpointType HardpointType => hardpointType;
    public Transform MountPoint => mountPoint != null ? mountPoint : transform;
    public Transform ModuleAnchor => moduleAnchor != null ? moduleAnchor : MountPoint;
    public InstalledModule InstalledModule => installedModule;
    public bool HasInstalledModule => installedModule != null;

    public SpriteRenderer PlacementMarkerRenderer
    {
        get
        {
            ResolvePlacementMarkerRenderer();
            return placementMarkerRenderer;
        }
    }

    public event System.Action<Hardpoint> StateChanged;

    public IReadOnlyList<MonoBehaviour> Controllers => controllers;
    public ModuleDefinition StartingModuleDefinition => startingModuleDefinition;

    private void Reset()
    {
        ResolvePlacementMarkerRenderer();
    }

    private void Awake()
    {
        ResolvePlacementMarkerRenderer();

        if (installStartingModuleOnAwake && !HasInstalledModule && startingModuleDefinition != null)
        {
            TryInstall(startingModuleDefinition, out _);
        }
    }

    public bool CanInstall(ModuleDefinition moduleDefinition)
    {
        if (moduleDefinition == null)
            return false;

        if (HasInstalledModule)
            return false;

        return ModuleMatchesAcceptedTypes(moduleDefinition);
    }

    public bool ModuleMatchesAcceptedTypes(ModuleDefinition moduleDefinition)
    {
        if (moduleDefinition == null)
            return false;

        HardpointType[] accepted = GetAcceptedTypes();

        for (int i = 0; i < accepted.Length; i++)
        {
            if (moduleDefinition.CanInstallOn(accepted[i]))
                return true;
        }

        return false;
    }

    public HardpointType[] GetAcceptedTypes()
    {
        List<HardpointType> result = new List<HardpointType>();
        result.Add(hardpointType);

        if (additionalAcceptedTypes != null)
        {
            for (int i = 0; i < additionalAcceptedTypes.Length; i++)
            {
                if (!result.Contains(additionalAcceptedTypes[i]))
                    result.Add(additionalAcceptedTypes[i]);
            }
        }

        return result.ToArray();
    }

    public string GetAcceptedTypesText()
    {
        HardpointType[] accepted = GetAcceptedTypes();

        if (accepted == null || accepted.Length == 0)
            return "None";

        string s = accepted[0].ToString();

        for (int i = 1; i < accepted.Length; i++)
            s += ", " + accepted[i];

        return s;
    }

    public bool HasController(
        MonoBehaviour controller)
    {
        if (controller == null ||
            controllers == null)
        {
            return false;
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            if (ReferenceEquals(
                    controllers[i],
                    controller))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Runtime-safe controller wiring. Array order remains authoritative for
    /// systems such as Helm station capacity.
    /// </summary>
    public bool TryAddController(
        MonoBehaviour controller)
    {
        if (controller == null)
            return false;

        if (HasController(controller))
            return true;

        int oldCount =
            controllers != null
                ? controllers.Length
                : 0;

        MonoBehaviour[] next =
            new MonoBehaviour[oldCount + 1];

        for (int i = 0; i < oldCount; i++)
            next[i] = controllers[i];

        next[oldCount] =
            controller;

        controllers =
            next;

        return true;
    }

    public bool TryRemoveController(
        MonoBehaviour controller)
    {
        if (controller == null ||
            controllers == null ||
            controllers.Length == 0)
        {
            return false;
        }

        int removeIndex =
            -1;

        for (int i = 0; i < controllers.Length; i++)
        {
            if (ReferenceEquals(
                    controllers[i],
                    controller))
            {
                removeIndex = i;
                break;
            }
        }

        if (removeIndex < 0)
            return false;

        if (controllers.Length == 1)
        {
            controllers =
                System.Array.Empty<MonoBehaviour>();

            return true;
        }

        MonoBehaviour[] next =
            new MonoBehaviour[
                controllers.Length - 1];

        int dst =
            0;

        for (int i = 0; i < controllers.Length; i++)
        {
            if (i == removeIndex)
                continue;

            next[dst++] =
                controllers[i];
        }

        controllers =
            next;

        return true;
    }

    public bool HasAnyController()
    {
        if (controllers == null)
            return false;

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                return true;
        }

        return false;
    }

    public bool TryGetFirstController<T>(out T controller) where T : class
    {
        controller = null;

        if (controllers == null)
            return false;

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] is T typed)
            {
                controller = typed;
                return true;
            }
        }

        return false;
    }

    public bool TryGetInstalledModuleComponent<T>(out T component) where T : Component
    {
        component = null;

        if (installedModule == null)
            return false;

        component = installedModule.GetComponent<T>();
        return component != null;
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

        return InstallWithoutCompatibilityCheck(moduleDefinition, out spawnedModule);
    }

    private bool InstallWithoutCompatibilityCheck(ModuleDefinition moduleDefinition, out InstalledModule spawnedModule)
    {
        spawnedModule = null;

        if (moduleDefinition == null || moduleDefinition.InstalledPrefab == null)
            return false;

        GameObject go = Instantiate(
            moduleDefinition.InstalledPrefab,
            MountPoint.position,
            MountPoint.rotation);

        go.transform.SetParent(MountPoint, worldPositionStays: true);

        AlignInstalledModuleToAnchor(go);

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
        spawnedModule = module;

        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb is IInstalledModuleLifecycle lifecycle)
                lifecycle.OnInstalled(this);
        }

        StateChanged?.Invoke(this);
        return true;
    }

    public bool TryRemove(out ModuleDefinition removedDefinition)
    {
        removedDefinition = null;

        if (!HasInstalledModule)
            return false;

        if (!CanRemoveInstalledModule(out string removalBlockReason))
        {
            Debug.LogWarning(
                $"[Hardpoint] Cannot remove module from '{HardpointId}': {removalBlockReason}",
                this);
            return false;
        }

        removedDefinition = installedModule.Definition;

        if (installedModule != null)
        {
            installedModule.DetachMassContributionFromBoat();

            foreach (MonoBehaviour mb in installedModule.GetComponents<MonoBehaviour>())
            {
                if (mb is IInstalledModuleLifecycle lifecycle)
                    lifecycle.OnRemoved();
            }

            Destroy(installedModule.gameObject);
        }

        installedModule = null;
        StateChanged?.Invoke(this);
        return removedDefinition != null;
    }

    private bool CanRemoveInstalledModule(out string reason)
    {
        reason = null;

        if (installedModule == null)
            return true;

        MonoBehaviour[] behaviours =
            installedModule.GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IInstalledModuleRemovalGuard guard)
                continue;

            if (guard.CanRemoveInstalledModule(out reason))
                continue;

            if (string.IsNullOrWhiteSpace(reason))
                reason = $"{behaviours[i].GetType().Name} blocked removal.";

            return false;
        }

        return true;
    }

    private void ResolvePlacementMarkerRenderer()
    {
        if (placementMarkerRenderer == null)
            placementMarkerRenderer = GetComponent<SpriteRenderer>();
    }

#if UNITY_EDITOR
    public bool EditorInstallStartingModule()
    {
        if (startingModuleDefinition == null)
        {
            Debug.LogWarning($"[Hardpoint] '{name}' has no Starting Module Definition assigned.", this);
            return false;
        }

        if (installedModule != null)
        {
            Debug.LogWarning($"[Hardpoint] '{name}' already has an installed module.", this);
            return false;
        }

        if (!ModuleMatchesAcceptedTypes(startingModuleDefinition))
        {
            Debug.LogWarning(
                $"[Hardpoint] Cannot install starting module '{startingModuleDefinition.DisplayName}' on '{name}'. " +
                $"Hardpoint accepts: {GetAcceptedTypesText()}",
                this);
            return false;
        }

        bool ok = TryInstall(startingModuleDefinition, out _);

        if (ok)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        return ok;
    }
#endif

#if UNITY_EDITOR
    public void EditorSetStartingModuleDefinition(ModuleDefinition moduleDefinition)
    {
        UnityEditor.Undo.RecordObject(this, "Set Starting Module Definition");
        startingModuleDefinition = moduleDefinition;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    public void EditorSetControllers(MonoBehaviour[] newControllers)
    {
        UnityEditor.Undo.RecordObject(this, "Set Hardpoint Controllers");
        controllers = newControllers;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    public bool EditorAddController(MonoBehaviour controller)
    {
        if (controller == null)
            return false;

        if (controllers != null)
        {
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] == controller)
                    return false;
            }
        }

        int oldCount = controllers != null ? controllers.Length : 0;
        MonoBehaviour[] next = new MonoBehaviour[oldCount + 1];

        for (int i = 0; i < oldCount; i++)
            next[i] = controllers[i];

        next[oldCount] = controller;

        EditorSetControllers(next);
        return true;
    }

    public bool EditorUninstallInstalledModule()
    {
        if (installedModule == null)
        {
            Debug.LogWarning($"[Hardpoint] '{name}' has no installed module to uninstall.", this);
            return false;
        }

        if (!CanRemoveInstalledModule(out string removalBlockReason))
        {
            Debug.LogWarning(
                $"[Hardpoint] Cannot uninstall module from '{HardpointId}': {removalBlockReason}",
                this);
            return false;
        }

        UnityEditor.Undo.RecordObject(this, "Uninstall Module From Hardpoint");

        installedModule.DetachMassContributionFromBoat();

        foreach (MonoBehaviour mb in installedModule.GetComponents<MonoBehaviour>())
        {
            if (mb is IInstalledModuleLifecycle lifecycle)
                lifecycle.OnRemoved();
        }

        GameObject moduleGO = installedModule.gameObject;
        installedModule = null;

        UnityEditor.Undo.DestroyObjectImmediate(moduleGO);

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        return true;
    }

    private void AlignInstalledModuleToAnchor(GameObject installedObject)
    {
        if (installedObject == null)
            return;

        Transform hardpointAnchor = ModuleAnchor;
        if (hardpointAnchor == null)
            return;

        InstalledModuleAnchor installedAnchor =
            installedObject.GetComponent<InstalledModuleAnchor>();

        if (installedAnchor == null)
            installedAnchor = installedObject.GetComponentInChildren<InstalledModuleAnchor>(true);

        Transform moduleAnchorTransform = installedAnchor != null
            ? installedAnchor.ModuleAnchor
            : installedObject.transform.Find("ModuleAnchor");

        if (moduleAnchorTransform == null)
        {
            Debug.LogWarning(
                $"[Hardpoint] Installed module '{installedObject.name}' has no InstalledModuleAnchor or child named 'ModuleAnchor'. Falling back to prefab root alignment.",
                installedObject);

            return;
        }

        Vector3 delta = hardpointAnchor.position - moduleAnchorTransform.position;
        installedObject.transform.position += delta;
    }
#endif
}