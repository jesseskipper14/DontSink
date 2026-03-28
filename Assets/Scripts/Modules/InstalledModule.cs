using UnityEngine;

public sealed class InstalledModule : MonoBehaviour
{
    [SerializeField] private ModuleDefinition definition;
    [SerializeField] private Hardpoint ownerHardpoint;

    public ModuleDefinition Definition => definition;
    public Hardpoint OwnerHardpoint => ownerHardpoint;

    public void Initialize(ModuleDefinition moduleDefinition, Hardpoint hardpoint)
    {
        definition = moduleDefinition;
        ownerHardpoint = hardpoint;
    }
}