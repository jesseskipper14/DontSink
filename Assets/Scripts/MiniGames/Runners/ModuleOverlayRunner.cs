using UnityEngine;
using MiniGames;

[DisallowMultipleComponent]
public sealed class ModuleOverlayRunner : MonoBehaviour
{
    [SerializeField] private MiniGameOverlayHost overlay;

    private void Reset()
    {
        overlay = FindAnyObjectByType<MiniGameOverlayHost>();
    }

    private void Awake()
    {
        if (overlay == null)
            overlay = FindAnyObjectByType<MiniGameOverlayHost>();
    }

    public bool OpenForHardpoint(Hardpoint hardpoint)
    {
        if (hardpoint == null)
        {
            Debug.LogWarning("[ModuleOverlayRunner] OpenForHardpoint called with null hardpoint.");
            return false;
        }

        if (!hardpoint.HasInstalledModule || hardpoint.InstalledModule == null)
        {
            Debug.LogWarning("[ModuleOverlayRunner] Hardpoint has no installed module.", hardpoint);
            return false;
        }

        if (overlay == null)
        {
            overlay = FindAnyObjectByType<MiniGameOverlayHost>();
            if (overlay == null)
            {
                Debug.LogError("[ModuleOverlayRunner] Missing MiniGameOverlayHost.");
                return false;
            }
        }

        var ctx = new MiniGameContext
        {
            targetId = hardpoint.HardpointId,
            difficulty = 1f,
            pressure = 0f,
            seed = 0
        };

        var cart = new ModuleCartridge(hardpoint);
        overlay.Open(cart, ctx);
        return true;
    }
}