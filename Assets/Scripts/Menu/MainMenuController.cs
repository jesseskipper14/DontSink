using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string nodeSceneName = "NodeScene";

    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button profilesButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("New Game Defaults")]
    [SerializeField] private string defaultBoatInstanceId = "boat_001";
    [SerializeField] private string defaultStartingNodeId = "";
    [SerializeField] private bool clearPlayerSceneContextOnNewGame = true;

    [Header("Save / Load")]
    [SerializeField] private SaveLoadController saveLoadController;
    [SerializeField] private SaveLoadPanelUI saveLoadPanel;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private void Awake()
    {
        WireButtons();

        // These exist as visual promises for now. Tiny lies, but organized ones.
        if (loadGameButton != null && saveLoadController != null)
            loadGameButton.interactable = saveLoadController.ManualSlotExists();

        if (profilesButton != null)
            profilesButton.interactable = false;

        if (settingsButton != null)
            settingsButton.interactable = false;

        if (saveLoadController == null)
            saveLoadController = FindAnyObjectByType<SaveLoadController>(FindObjectsInactive.Include);

        if (saveLoadPanel == null)
            saveLoadPanel = FindAnyObjectByType<SaveLoadPanelUI>(FindObjectsInactive.Include);
    }

    private void WireButtons()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(StartNewGame);
            newGameButton.onClick.AddListener(StartNewGame);
        }

        if (loadGameButton != null)
        {
            loadGameButton.onClick.RemoveListener(LoadGameStub);
            loadGameButton.onClick.AddListener(LoadGameStub);
        }

        if (profilesButton != null)
        {
            profilesButton.onClick.RemoveListener(ProfilesStub);
            profilesButton.onClick.AddListener(ProfilesStub);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(SettingsStub);
            settingsButton.onClick.AddListener(SettingsStub);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    public void StartNewGame()
    {
        Log("StartNewGame");

        EnsureCoreSingletons();
        ResetGameStateForNewGame();

        SceneManager.LoadScene(nodeSceneName);
    }

    private void EnsureCoreSingletons()
    {
        if (GameState.I == null)
        {
            GameObject go = new GameObject("GameState");
            go.AddComponent<GameState>();
            Log("Created GameState because none existed.");
        }

        if (SceneTransitionController.I == null)
        {
            GameObject go = new GameObject("SceneTransitionController");
            go.AddComponent<SceneTransitionController>();
            Log("Created SceneTransitionController because none existed.");
        }
    }

    private void ResetGameStateForNewGame()
    {
        GameState gs = GameState.I;
        if (gs == null)
        {
            Debug.LogError("[MainMenuController] Cannot reset GameState because GameState.I is null.", this);
            return;
        }

        gs.activeTravel = null;

        if (gs.player == null)
            gs.player = new WorldMapPlayerState();

        if (!string.IsNullOrWhiteSpace(defaultStartingNodeId))
            gs.player.currentNodeId = defaultStartingNodeId;

        gs.player.lockedDestinationNodeId = null;
        gs.player.lockedSourceNodeId = null;

        gs.worldMap = new WorldMapSimState();

        gs.playerLoadout = null;

        gs.boat = new BoatSaveState
        {
            boatPrefabGuid = "",
            boatInstanceId = string.IsNullOrWhiteSpace(defaultBoatInstanceId)
                ? "boat_001"
                : defaultBoatInstanceId,

            cargo = new System.Collections.Generic.List<CargoManifest.Snapshot>(),
            looseItems = new BoatLooseItemManifest(),
            moduleStates = new BoatModuleStateManifest(),
            compartmentStates = new BoatCompartmentStateManifest(),
            accessStates = new BoatAccessStateManifest(),
            transformState = null,
            power = null
        };

        if (clearPlayerSceneContextOnNewGame)
            gs.playerSceneContext = null;

        gs.LogState("MainMenuController.ResetGameStateForNewGame");
    }

    public void LoadGameStub()
    {
        if (saveLoadPanel == null)
            saveLoadPanel = FindAnyObjectByType<SaveLoadPanelUI>(FindObjectsInactive.Include);

        if (saveLoadPanel == null)
        {
            Debug.LogWarning("[MainMenuController] Cannot open load UI: no SaveLoadPanelUI found.", this);
            return;
        }

        saveLoadPanel.Open();
    }

    public void ProfilesStub()
    {
        Debug.Log("[MainMenuController] Profiles are not implemented yet.", this);
    }

    public void SettingsStub()
    {
        Debug.Log("[MainMenuController] Settings are not implemented yet.", this);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[MainMenuController:{name}] {msg}", this);
    }
}