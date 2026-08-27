using UnityEngine;
using MiniGames;

/// <summary>
/// Opens the dedicated read-only HelmCartridge for an installed HelmModule.
/// </summary>
[DisallowMultipleComponent]
public sealed class HelmOverlayRunner : MonoBehaviour
{
    [SerializeField] private MiniGameOverlayHost overlay;

    private void Reset()
    {
        overlay =
            FindAnyObjectByType<MiniGameOverlayHost>();
    }

    private void Awake()
    {
        ResolveOverlay();
    }

    public bool OpenForHardpoint(
        Hardpoint hardpoint)
    {
        if (hardpoint == null)
        {
            Debug.LogWarning(
                "[HelmOverlayRunner] OpenForHardpoint called with null hardpoint.");
            return false;
        }

        if (!hardpoint.HasInstalledModule ||
            hardpoint.InstalledModule == null)
        {
            Debug.LogWarning(
                "[HelmOverlayRunner] Helm hardpoint has no installed module.",
                hardpoint);
            return false;
        }

        HelmModule helm =
            hardpoint.InstalledModule
                .GetComponent<HelmModule>();

        if (helm == null)
        {
            Debug.LogWarning(
                $"[HelmOverlayRunner] Installed module on '{hardpoint.HardpointId}' is not a HelmModule.",
                hardpoint);
            return false;
        }

        if (!ResolveOverlay())
            return false;

        Boat boat =
            hardpoint.GetComponentInParent<Boat>();

        BoatPilotingSimulation simulation =
            ResolvePilotingSimulation(
                boat,
                hardpoint);

        IHelmNavigationReadoutSource navigation =
            ResolveNavigationSource(
                boat);

        if (navigation == null)
        {
            navigation =
                new DefaultHelmNavigationReadoutSource(
                    simulation);
        }

        HelmReadoutSource readout =
            new HelmReadoutSource(
                hardpoint,
                helm,
                simulation,
                navigation);

        HelmCartridge cartridge =
            new HelmCartridge(
                hardpoint,
                readout,
                simulation);

        MiniGameContext ctx =
            new MiniGameContext
            {
                targetId =
                    !string.IsNullOrWhiteSpace(
                        hardpoint.HardpointId)
                        ? $"helm_console:{hardpoint.HardpointId}"
                        : $"helm_console:{hardpoint.GetInstanceID()}",
                difficulty = 1f,
                pressure = 0f,
                seed = 0
            };

        overlay.Open(
            cartridge,
            ctx);

        return true;
    }

    private bool ResolveOverlay()
    {
        if (overlay == null)
        {
            overlay =
                FindAnyObjectByType<MiniGameOverlayHost>();
        }

        if (overlay != null)
            return true;

        Debug.LogError(
            "[HelmOverlayRunner] Missing MiniGameOverlayHost.",
            this);

        return false;
    }

    private static BoatPilotingSimulation ResolvePilotingSimulation(
        Boat boat,
        Hardpoint hardpoint)
    {
        if (boat != null)
        {
            BoatPilotingSimulation simulation =
                boat.GetComponent<BoatPilotingSimulation>() ??
                boat.GetComponentInChildren<BoatPilotingSimulation>(
                    true);

            if (simulation != null)
                return simulation;
        }

        return hardpoint != null
            ? hardpoint.GetComponentInParent<BoatPilotingSimulation>()
            : null;
    }

    private static IHelmNavigationReadoutSource ResolveNavigationSource(
        Boat boat)
    {
        if (boat == null)
            return null;

        MonoBehaviour[] behaviours =
            boat.GetComponentsInChildren<MonoBehaviour>(
                true);

        for (int i = 0;
             i < behaviours.Length;
             i++)
        {
            MonoBehaviour behaviour =
                behaviours[i];

            if (behaviour is IHelmNavigationReadoutSource source)
                return source;
        }

        return null;
    }
}