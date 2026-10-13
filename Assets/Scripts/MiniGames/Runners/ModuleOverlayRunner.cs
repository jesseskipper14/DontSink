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

    public bool OpenForHardpoint(Hardpoint hardpoint, GameObject requester)
    {
        if (requester == null)
        {
            Debug.LogWarning("[ModuleOverlayRunner] Missing requester.", this);
            return false;
        }
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

        var cart = new ModuleCartridge(hardpoint, requester);
        overlay.Open(cart, ctx);
        return true;
    }
}
