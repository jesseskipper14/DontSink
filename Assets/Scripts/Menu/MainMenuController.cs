using System.Collections.Generic;
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
    [SerializeField, Range(0f, 24f)] private float newGameStartHour = 10f;

    [Header("Save / Load")]
    [SerializeField] private SaveLoadController saveLoadController;
    [SerializeField] private SaveLoadPanelUI saveLoadPanel;

    [Header("Load Button")]
    [Tooltip("If true, Load Game is enabled only when at least one valid save exists. If false, any save file enables the panel, including invalid saves.")]
    [SerializeField] private bool requireValidSaveForLoadButton = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private void Awake()
    {
        ResolveRefs();
        WireButtons();
        RefreshButtonStates();
    }

    private void OnEnable()
    {
        ResolveRefs();
        RefreshButtonStates();
    }

    private void ResolveRefs()
    {
        if (saveLoadController == null)
            saveLoadController = FindAnyObjectByType<SaveLoadController>(FindObjectsInactive.Include);

        if (saveLoadPanel == null)
            saveLoadPanel = FindAnyObjectByType<SaveLoadPanelUI>(FindObjectsInactive.Include);
    }

    private void RefreshButtonStates()
    {
        if (loadGameButton != null)
            loadGameButton.interactable = HasLoadableSave();

        if (profilesButton != null)
            profilesButton.interactable = false;

        if (settingsButton != null)
            settingsButton.interactable = false;
    }

    private bool HasLoadableSave()
    {
        if (saveLoadController == null)
        {
            Log("HasLoadableSave=false because SaveLoadController is null.");
            return false;
        }

        List<SaveSlotSummary> slots = saveLoadController.ListSlots();

        if (slots == null || slots.Count == 0)
        {
            Log("HasLoadableSave=false because no save slots were found.");
            return false;
        }

        if (!requireValidSaveForLoadButton)
        {
            Log($"HasLoadableSave=true because save files exist. count={slots.Count}");
            return true;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            SaveSlotSummary slot = slots[i];
            if (slot != null && slot.isValid)
            {
                Log($"HasLoadableSave=true. First valid slot='{slot.slotId}'.");
                return true;
            }
        }

        Log($"HasLoadableSave=false. Save files exist but none are valid. count={slots.Count}");
        return false;
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
        gs.SetCelestialChartState(null, "MainMenuController.NewGame");
        ResetTimeOfDayForNewGame(gs);

        gs.playerLoadout = null;

        gs.boat = new BoatSaveState
        {
            boatPrefabGuid = "",
            boatInstanceId = string.IsNullOrWhiteSpace(defaultBoatInstanceId)
                ? "boat_001"
                : defaultBoatInstanceId,

            looseItems = new BoatLooseItemManifest(),
            moduleStates = new BoatModuleStateManifest(),
            compartmentStates = new BoatCompartmentStateManifest(),
            accessStates = new BoatAccessStateManifest(),
            transformState = null,
            power = null
        };

        if (clearPlayerSceneContextOnNewGame)
            gs.playerSceneContext = null;

        // Rebuild the keyed records from the reset legacy mirrors, not the previous session.
        gs.SetPlayerPersistenceStates(null, "MainMenuController.NewGame");

        MoneyChestTreasuryService treasury = gs.moneyChestTreasury != null
            ? gs.moneyChestTreasury : MoneyChestTreasuryService.Instance;
        if (treasury != null) treasury.ResetForNewGame();
        else gs.SetMoneyChestTreasuryState(null, "MainMenuController.NewGame");

        gs.LogState("MainMenuController.ResetGameStateForNewGame");
    }

    private void ResetTimeOfDayForNewGame(GameState gs)
    {
        var snapshot = new TimeOfDaySnapshot
        {
            isValid = true,
            currentTime = Mathf.Repeat(newGameStartHour, 24f),
            year = 1,
            month = 1,
            day = 1
        };
        gs.SetTimeOfDaySnapshot(snapshot, "MainMenuController.NewGame");

        if (ServiceRoot.Instance != null &&
            ServiceRoot.Instance.ApplyPersistedTimeState(snapshot, "MainMenuController.NewGame"))
            return;

        var manager = ServiceRoot.Instance != null ? ServiceRoot.Instance.TimeManager : null;
        if (manager == null) manager = FindAnyObjectByType<TimeOfDayManager>();
        if (manager != null) manager.ApplySnapshot(snapshot, forceNotify: true);
        // Without a live service, its normal Awake restore consumes the valid GameState snapshot.
    }

    public void LoadGameStub()
    {
        ResolveRefs();

        if (saveLoadPanel == null)
        {
            Debug.LogWarning("[MainMenuController] Cannot open load UI: no SaveLoadPanelUI found.", this);
            return;
        }

        saveLoadPanel.Open();

        // Save files may have changed since entering the menu.
        RefreshButtonStates();
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

#if UNITY_EDITOR
    [ContextMenu("DEBUG Refresh Button States")]
    private void DebugRefreshButtonStates()
    {
        ResolveRefs();
        RefreshButtonStates();
    }
#endif

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[MainMenuController:{name}] {msg}", this);
    }
}
