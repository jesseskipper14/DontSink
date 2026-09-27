using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class EscapeMenuUI : MonoBehaviour, IEscapeClosable
{
    [Header("Refs")]
    [SerializeField] private GameObject root;

    [Header("Behavior")]
    [SerializeField] private bool pauseTimeWhenOpen = false;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Escape")]
    [SerializeField] private int escapePriority = 0;

    [Header("Save / Load")]
    [SerializeField] private SaveLoadController saveLoadController;
    [SerializeField] private SaveLoadPanelUI saveLoadPanel;

    [Header("Debug Respawn")]
    [SerializeField] private ChoiceDialogUI respawnDialog;

    public int EscapePriority => escapePriority;
    public bool IsEscapeOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        SetOpen(false);

        if (saveLoadController == null)
            saveLoadController = FindAnyObjectByType<SaveLoadController>(FindObjectsInactive.Include);

        if (saveLoadPanel == null)
            saveLoadPanel = FindAnyObjectByType<SaveLoadPanelUI>(FindObjectsInactive.Include);

        if (respawnDialog == null)
            respawnDialog = FindAnyObjectByType<ChoiceDialogUI>(FindObjectsInactive.Include);
    }

    public void Open()
    {
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void Toggle()
    {
        SetOpen(!IsEscapeOpen);
    }

    public bool CloseFromEscape()
    {
        if (!IsEscapeOpen)
            return false;

        Close();
        return true;
    }

    public void Resume()
    {
        Close();
    }

    public void SaveGame()
    {
        if (saveLoadPanel == null)
            saveLoadPanel = FindAnyObjectByType<SaveLoadPanelUI>(FindObjectsInactive.Include);

        if (saveLoadPanel != null)
            saveLoadPanel.Open();
        else
            Debug.LogWarning("[EscapeMenuUI] Cannot open save UI: no SaveLoadPanelUI found.", this);
    }

    public void LoadGame()
    {
        if (saveLoadPanel == null)
            saveLoadPanel = FindAnyObjectByType<SaveLoadPanelUI>(FindObjectsInactive.Include);

        if (saveLoadPanel != null)
            saveLoadPanel.Open();
        else
            Debug.LogWarning("[EscapeMenuUI] Cannot open load UI: no SaveLoadPanelUI found.", this);
    }

    public void RespawnPlayer()
    {
        if (respawnDialog == null)
            respawnDialog = FindAnyObjectByType<ChoiceDialogUI>(FindObjectsInactive.Include);

        if (respawnDialog == null)
        {
            Debug.LogWarning(
                "[EscapeMenuUI] No ChoiceDialogUI found. Respawning with inventory kept.",
                this);

            ExecuteRespawn(keepInventory: true);
            return;
        }

        respawnDialog.Show(
            "Respawn Player?",
            "Respawn at the boat spawn point (or 0,0 if none exists). Keep your current inventory?",
            "Keep Inventory",
            primary: () => ExecuteRespawn(keepInventory: true),
            secondaryLabel: "Discard Inventory",
            secondary: () => ExecuteRespawn(keepInventory: false),
            cancelLabel: "Cancel");
    }

    private void ExecuteRespawn(bool keepInventory)
    {
        if (!PlayerDebugRespawnService.TryRespawn(keepInventory))
        {
            Debug.LogWarning(
                "[EscapeMenuUI] Debug respawn failed. No active PlayerDeathSystem/player could be resolved.",
                this);

            return;
        }

        Close();
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (saveLoadPanel == null)
            saveLoadPanel = FindAnyObjectByType<SaveLoadPanelUI>(FindObjectsInactive.Include);

        if (saveLoadPanel != null)
        {
            saveLoadPanel.PromptReturnToMainMenu(mainMenuSceneName);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        if (saveLoadPanel == null)
            saveLoadPanel = FindAnyObjectByType<SaveLoadPanelUI>(FindObjectsInactive.Include);

        if (saveLoadPanel != null)
        {
            saveLoadPanel.PromptQuit();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

    private void SetOpen(bool open)
    {
        if (root == null)
            return;

        root.SetActive(open);

        if (open)
        {
            EscapeCloseRegistry registry = EscapeCloseRegistry.GetOrFind();
            if (registry != null)
                registry.Register(this);
        }
        else if (EscapeCloseRegistry.I != null)
        {
            EscapeCloseRegistry.I.Unregister(this);
        }

        if (open)
            GameplayInputBlocker.Push(this);
        else
            GameplayInputBlocker.Pop(this);

        if (pauseTimeWhenOpen)
            Time.timeScale = open ? 0f : 1f;
    }

    private void OnDisable()
    {
        if (EscapeCloseRegistry.I != null)
            EscapeCloseRegistry.I.Unregister(this);

        if (pauseTimeWhenOpen)
            Time.timeScale = 1f;

        GameplayInputBlocker.Pop(this);
    }
}