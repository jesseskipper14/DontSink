#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{
    /// <summary>
    /// Builder-time validation for the hardpoint's authored Starting Module.
    ///
    /// Runtime/manual module installation is separately protected by
    /// Hardpoint.CanInstall(...), so a large module cannot later be installed on
    /// a hardpoint whose current boat placement does not provide its required
    /// support span.
    /// </summary>
    private static bool TryValidatePlacedHardpointSupport(
        GameObject placed,
        Transform boatRoot)
    {
        if (placed == null ||
            boatRoot == null)
        {
            return true;
        }

        Hardpoint hardpoint =
            placed.GetComponent<Hardpoint>() ??
            placed.GetComponentInChildren<Hardpoint>(
                true);

        if (hardpoint == null)
            return true;

        ModuleDefinition module =
            hardpoint.StartingModuleDefinition;

        if (module == null ||
            !module.RequiresBoatSupport)
        {
            return true;
        }

        HardpointSupportFootprint support =
            hardpoint.GetComponent<HardpointSupportFootprint>() ??
            hardpoint.GetComponentInChildren<HardpointSupportFootprint>(
                true);

        if (support == null)
        {
            Debug.LogWarning(
                $"[BoatBuilder] Cannot place hardpoint '{placed.name}' with starting module " +
                $"'{module.DisplayName}'. Module requires " +
                $"{module.RequiredBoatSupportWidth:0.##} m of boat support, but the hardpoint " +
                "prefab has no HardpointSupportFootprint.",
                placed);

            Undo.DestroyObjectImmediate(
                placed);

            return false;
        }

        if (support.TryValidateSupportWidth(
                module.RequiredBoatSupportWidth,
                boatRoot,
                out string reason))
        {
            return true;
        }

        Debug.LogWarning(
            $"[BoatBuilder] Cannot place hardpoint '{placed.name}' with starting module " +
            $"'{module.DisplayName}'. Required boat support is invalid. {reason}",
            placed);

        Undo.DestroyObjectImmediate(
            placed);

        return false;
    }
}
#endif
