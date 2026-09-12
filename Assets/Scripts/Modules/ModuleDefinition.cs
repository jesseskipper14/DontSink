using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Module/Module Definition")]
public sealed class ModuleDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string moduleId;
    [SerializeField] private string displayName = "Module";

    [Header("Compatibility")]
    [SerializeField] private HardpointType[] allowedHardpointTypes;

    [Header("Visual / Runtime")]
    [SerializeField] private GameObject installedPrefab;

    [Header("Inventory Link")]
    [SerializeField] private ItemDefinition itemDefinition;

    [Header("Installed Storage (available after this module is installed)")]
    [Tooltip("Defines storage provided by the INSTALLED module. This is separate from portable-container settings on the module item's ItemDefinition.")]
    [FormerlySerializedAs("storage")]
    [SerializeField] private StorageModuleDefinition installedStorage = new StorageModuleDefinition();

    public string ModuleId => moduleId;
    public string DisplayName => displayName;
    public GameObject InstalledPrefab => installedPrefab;
    // Compatibility alias. ItemDefinition is the single authored source of truth.
    public float BaseMass =>
        itemDefinition != null
            ? itemDefinition.UnitMass
            : 0f;
    public ItemDefinition ItemDefinition => itemDefinition;
    public HardpointType[] AllowedHardpointTypes => allowedHardpointTypes;

    public StorageModuleDefinition InstalledStorage => installedStorage;
    // Compatibility alias used by existing runtime code.
    public StorageModuleDefinition Storage => installedStorage;
    public bool HasStorage => installedStorage != null && installedStorage.HasStorage;
    public bool IsFixedStorage => HasStorage && installedStorage.IsFixedStorage;
    public bool IsContainerRack => HasStorage && installedStorage.IsContainerRack;

    public bool CanInstallOn(HardpointType hardpointType)
    {
        if (allowedHardpointTypes == null)
            return false;

        for (int i = 0; i < allowedHardpointTypes.Length; i++)
        {
            if (allowedHardpointTypes[i] == hardpointType)
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (installedStorage == null)
            installedStorage = new StorageModuleDefinition();
    }
#endif
}