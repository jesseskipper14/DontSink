using MiniGames;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MoneyChestOverlayOpener : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MiniGameOverlayHost overlay;

    [Header("Visuals")]
    [SerializeField] private MoneyChestVisualSettings visualSettings = new MoneyChestVisualSettings();

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages = true;

    private void Reset()
    {
        overlay = FindFirstObjectByType<MiniGameOverlayHost>();
    }

    private void Awake()
    {
        if (overlay == null)
            overlay = FindFirstObjectByType<MiniGameOverlayHost>();
    }

    public bool Open(MoneyChestState chest)
    {
        if (chest == null)
        {
            LogWarning("Cannot open money chest overlay. Chest is null.");
            return false;
        }

        if (!chest.IsActive || chest.IsRetired)
        {
            LogWarning(
                $"Cannot open money chest overlay. Chest must be Active. " +
                $"id='{chest.ChestInstanceId}', state={chest.LifecycleState}");

            return false;
        }

        if (overlay == null)
            overlay = FindFirstObjectByType<MiniGameOverlayHost>();

        if (overlay == null)
        {
            LogWarning("Cannot open money chest overlay. No MiniGameOverlayHost found.");
            return false;
        }

        chest.SyncInstanceIdFromWorldItem();
        chest.EnsureInstanceId();

        int seed = BuildStableSeed(chest);

        var ctx = new MiniGameContext
        {
            targetId = chest.ChestInstanceId,
            difficulty = 1f,
            pressure = 0f,
            seed = seed
        };

        var cartridge = new MoneyChestCartridge(chest, visualSettings);
        overlay.Open(cartridge, ctx);

        Log(
            $"Opened money chest overlay. id='{chest.ChestInstanceId}', " +
            $"balance={chest.Balance}, seed={seed}");

        return true;
    }

    private int BuildStableSeed(MoneyChestState chest)
    {
        unchecked
        {
            int seed = 17;

            string id = chest != null ? chest.ChestInstanceId : null;
            if (!string.IsNullOrWhiteSpace(id))
            {
                for (int i = 0; i < id.Length; i++)
                    seed = seed * 31 + id[i];
            }

            if (GameState.I != null && GameState.I.player != null && !string.IsNullOrWhiteSpace(GameState.I.player.currentNodeId))
            {
                string node = GameState.I.player.currentNodeId;
                for (int i = 0; i < node.Length; i++)
                    seed = seed * 31 + node[i];
            }

            return seed;
        }
    }

    private void Log(string msg)
    {
        if (!logDebugMessages)
            return;

        Debug.Log($"[MoneyChestOverlayOpener:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        Debug.LogWarning($"[MoneyChestOverlayOpener:{name}] {msg}", this);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Open Local Money Chest")]
    private void DebugOpenLocalMoneyChest()
    {
        MoneyChestState chest = GetComponent<MoneyChestState>();
        if (chest == null)
            chest = GetComponentInParent<MoneyChestState>();

        if (chest == null)
            chest = GetComponentInChildren<MoneyChestState>(true);

        Open(chest);
    }
#endif
}