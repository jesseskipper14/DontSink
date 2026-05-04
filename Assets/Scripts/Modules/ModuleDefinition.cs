using UnityEngine;

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

    public string ModuleId => moduleId;
    public string DisplayName => displayName;
    public GameObject InstalledPrefab => installedPrefab;
    public ItemDefinition ItemDefinition => itemDefinition;
    public HardpointType[] AllowedHardpointTypes => allowedHardpointTypes;

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
}