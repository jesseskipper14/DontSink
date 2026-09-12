using UnityEngine;
using MiniGames;

/// <summary>
/// Opens the dedicated WinchCartridge for an installed WinchModule.
/// Add one runner to the scene alongside the other overlay runners.
/// </summary>
[DisallowMultipleComponent]
public sealed class WinchOverlayRunner : MonoBehaviour
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
                "[WinchOverlayRunner] OpenForHardpoint called with null hardpoint.");

            return false;
        }

        if (!hardpoint.HasInstalledModule ||
            hardpoint.InstalledModule == null)
        {
            Debug.LogWarning(
                "[WinchOverlayRunner] Winch hardpoint has no installed module.",
                hardpoint);

            return false;
        }

        WinchModule winch =
            hardpoint.InstalledModule
                .GetComponent<WinchModule>();

        if (winch == null)
        {
            Debug.LogWarning(
                $"[WinchOverlayRunner] Installed module on '{hardpoint.HardpointId}' is not a WinchModule.",
                hardpoint);

            return false;
        }

        if (!ResolveOverlay())
            return false;

        WinchReadoutSource readout =
            new WinchReadoutSource(
                hardpoint,
                winch);

        WinchCartridge cartridge =
            new WinchCartridge(
                hardpoint,
                winch,
                readout);

        MiniGameContext ctx =
            new MiniGameContext
            {
                targetId =
                    !string.IsNullOrWhiteSpace(
                        hardpoint.HardpointId)
                        ? $"winch_console:{hardpoint.HardpointId}"
                        : $"winch_console:{hardpoint.GetInstanceID()}",
                difficulty =
                    1f,
                pressure =
                    0f,
                seed =
                    0
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
            "[WinchOverlayRunner] Missing MiniGameOverlayHost.",
            this);

        return false;
    }
}
