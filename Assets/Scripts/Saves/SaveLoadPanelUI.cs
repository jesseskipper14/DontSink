using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SaveLoadPanelUI : MonoBehaviour, IEscapeClosable
{
    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private SaveLoadController saveLoad;
    [SerializeField] private ChoiceDialogUI dialog;

    [Header("Save List")]
    [SerializeField] private Transform slotListRoot;
    [SerializeField] private SaveSlotRowUI slotRowPrefab;

    [Header("Selected Save")]
    [SerializeField] private TMP_Text selectedSlotText;

    [Header("New / Save Inputs")]
    [SerializeField] private TMP_InputField slotIdInput;
    [SerializeField] private TMP_InputField displayNameInput;

    [Header("Buttons")]
    [SerializeField] private Button saveSelectedButton;
    [SerializeField] private Button loadSelectedButton;
    [SerializeField] private Button newSaveButton;
    [SerializeField] private Button deleteSelectedButton;
    [SerializeField] private Button openFolderButton;
    [SerializeField] private Button closeButton;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    [Header("Escape Routing")]
    [SerializeField] private bool closeViaGlobalEscapeRouter = true;
    [SerializeField] private int escapePriority = 900;

    [Header("Version Display")]
    [SerializeField] private TMP_Text versionText;

    [Header("Debug")]
    [SerializeField] private bool refreshSlotListOnOpen = true;

    private readonly List<SaveSlotRowUI> _spawnedRows = new();

    private SaveSlotSummary _selected;

    public int EscapePriority => escapePriority;
    public bool IsEscapeOpen => IsOpen;
    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (saveLoad == null)
            saveLoad = FindAnyObjectByType<SaveLoadController>(FindObjectsInactive.Include);

        if (dialog == null)
            dialog = FindAnyObjectByType<ChoiceDialogUI>(FindObjectsInactive.Include);

        if (saveSelectedButton != null)
            saveSelectedButton.onClick.AddListener(ClickSaveSelected);

        if (loadSelectedButton != null)
            loadSelectedButton.onClick.AddListener(ClickLoadSelected);

        if (newSaveButton != null)
            newSaveButton.onClick.AddListener(ClickNewSave);

        if (deleteSelectedButton != null)
            deleteSelectedButton.onClick.AddListener(ClickDeleteSelected);

        if (openFolderButton != null)
            openFolderButton.onClick.AddListener(OpenSaveFolder);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        Close();
    }

    private void OnDisable()
    {
        UnregisterFromEscape();
        GameplayInputBlocker.Pop(this);
    }

    public void Open()
    {
        if (root != null)
            root.SetActive(true);

        if (refreshSlotListOnOpen)
            RefreshSlotList();

        RegisterWithEscape();
        RefreshSelectedUI();
        RefreshButtons();
        RefreshVersionText();

        GameplayInputBlocker.Push(this);
    }

    public void Close()
    {
        if (root != null)
            root.SetActive(false);

        UnregisterFromEscape();
        GameplayInputBlocker.Pop(this);
    }

    public bool CloseFromEscape()
    {
        if (!IsOpen)
            return false;

        Close();
        return true;
    }

    public void SelectSlot(SaveSlotSummary summary)
    {
        _selected = summary;

        if (slotIdInput != null)
            slotIdInput.text = summary != null ? summary.slotId : "";

        if (displayNameInput != null)
            displayNameInput.text = summary != null ? summary.displayName : "";

        SetStatus(summary != null ? $"Selected '{summary.slotId}'." : "No save selected.");

        RefreshSelectedUI();
        RefreshButtons();
        RefreshRowSelectionVisuals();
    }

    public void RefreshSlotList()
    {
        ClearRows();

        if (saveLoad == null || slotListRoot == null || slotRowPrefab == null)
        {
            SetStatus("Save list UI is missing references.");
            return;
        }

        List<SaveSlotSummary> slots = saveLoad.ListSlots();

        if (slots.Count == 0)
        {
            _selected = null;
            SetStatus("No saves found.");
            RefreshSelectedUI();
            RefreshButtons();
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            SaveSlotRowUI row = Instantiate(slotRowPrefab, slotListRoot);
            row.Bind(this, slots[i]);
            _spawnedRows.Add(row);
        }

        if (_selected == null)
        {
            // Prefer first valid save, otherwise leave no selection.
            SaveSlotSummary firstValid = FindFirstValid(slots);
            if (firstValid != null)
                SelectSlot(firstValid);
            else
            {
                _selected = null;
                SetStatus("No valid saves found.");
                RefreshSelectedUI();
                RefreshButtons();
                RefreshRowSelectionVisuals();
            }

            return;
        }

        SaveSlotSummary refreshed = null;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].slotId == _selected.slotId)
            {
                refreshed = slots[i];
                break;
            }
        }

        if (refreshed != null && refreshed.isValid)
        {
            _selected = refreshed;
            RefreshSelectedUI();
            RefreshButtons();
            RefreshRowSelectionVisuals();
            return;
        }

        SaveSlotSummary fallback = FindFirstValid(slots);
        if (fallback != null)
            SelectSlot(fallback);
        else
        {
            _selected = null;
            SetStatus("No valid saves found.");
            RefreshSelectedUI();
            RefreshButtons();
            RefreshRowSelectionVisuals();
        }
    }

    private static SaveSlotSummary FindFirstValid(List<SaveSlotSummary> slots)
    {
        if (slots == null)
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].isValid)
                return slots[i];
        }

        return null;
    }

    private void ClickSaveSelected()
    {
        if (saveLoad == null)
        {
            SetStatus("No SaveLoadController found.");
            return;
        }

        if (_selected == null)
        {
            SetStatus("No save selected.");
            return;
        }

        if (!saveLoad.CanSaveNow(out string reason))
        {
            SetStatus(reason);
            return;
        }

        string slot = _selected.slotId;
        string display = GetDisplayName(slot);

        ShowOverwritePrompt(slot, display);
    }

    private void ClickNewSave()
    {
        if (saveLoad == null)
        {
            SetStatus("No SaveLoadController found.");
            return;
        }

        if (!saveLoad.CanSaveNow(out string reason))
        {
            SetStatus(reason);
            return;
        }

        string slot = GetTypedSlotId();
        string display = GetDisplayName(slot);

        if (string.IsNullOrWhiteSpace(slot))
        {
            SetStatus("Enter a save file name first.");
            return;
        }

        if (saveLoad.SlotExists(slot))
        {
            ShowOverwritePrompt(slot, display);
            return;
        }

        SaveNow(slot, display);
    }

    private void ClickLoadSelected()
    {
        if (saveLoad == null)
        {
            SetStatus("No SaveLoadController found.");
            return;
        }

        if (_selected == null)
        {
            SetStatus("No save selected.");
            return;
        }

        string slot = _selected.slotId;

        if (!saveLoad.SlotExists(slot))
        {
            SetStatus($"Save slot '{slot}' does not exist.");
            RefreshSlotList();
            return;
        }

        if (saveLoad.IsInMainMenuScene())
        {
            LoadNow(slot);
            return;
        }

        if (saveLoad.IsInNodeScene() && saveLoad.CanSaveNow(out _))
        {
            ShowLoadFromNodePrompt(slot);
            return;
        }

        ShowLoadUnsafePrompt(slot);
    }

    private void ShowOverwritePrompt(string slot, string display)
    {
        if (dialog == null)
        {
            SaveNow(slot, display);
            return;
        }

        dialog.Show(
            "Overwrite Save?",
            $"A save named '{slot}' already exists. Overwrite it?",
            "Overwrite",
            primary: () => SaveNow(slot, display),
            secondaryLabel: null,
            secondary: null,
            cancelLabel: "Cancel");
    }

    private void ShowLoadFromNodePrompt(string slot)
    {
        if (dialog == null)
        {
            LoadNow(slot);
            return;
        }

        dialog.Show(
            "Load Save?",
            "Save first, or current progress will be lost.",
            "Autosave First",
            primary: () =>
            {
                if (TrySaveAutosaveBeforeLoad())
                    LoadNow(slot);
            },
            secondaryLabel: "Load Without Saving",
            secondary: () => LoadNow(slot),
            cancelLabel: "Cancel");
    }

    private void ShowLoadUnsafePrompt(string slot)
    {
        if (dialog == null)
        {
            LoadNow(slot);
            return;
        }

        dialog.Show(
            "Load Save?",
            "Current voyage progress will be lost. Load anyway?",
            "Load",
            primary: () => LoadNow(slot),
            secondaryLabel: null,
            secondary: null,
            cancelLabel: "Cancel");
    }

    public void PromptReturnToMainMenu(string mainMenuSceneName)
    {
        if (saveLoad == null)
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        if (saveLoad.IsInNodeScene() && saveLoad.CanSaveNow(out _))
        {
            dialog.Show(
                "Return to Main Menu?",
                "Save first, or current progress will be lost.",
                "Autosave First",
                primary: () =>
                {
                    if (TrySaveAutosaveBeforeExit())
                        SceneManager.LoadScene(mainMenuSceneName);
                },
                secondaryLabel: "Leave Without Saving",
                secondary: () => SceneManager.LoadScene(mainMenuSceneName),
                cancelLabel: "Cancel");

            return;
        }

        dialog.Show(
            "Return to Main Menu?",
            "Current voyage progress will be lost. Return anyway?",
            "Return",
            primary: () => SceneManager.LoadScene(mainMenuSceneName),
            secondaryLabel: null,
            secondary: null,
            cancelLabel: "Cancel");
    }

    public void PromptQuit()
    {
        if (saveLoad != null && saveLoad.IsInNodeScene() && saveLoad.CanSaveNow(out _))
        {
            dialog.Show(
                "Quit Game?",
                "Save first, or current progress will be lost.",
                "Autosave First",
                primary: () =>
                {
                    if (TrySaveAutosaveBeforeExit())
                        QuitNow();
                },
                secondaryLabel: "Quit Without Saving",
                secondary: QuitNow,
                cancelLabel: "Cancel");

            return;
        }

        dialog.Show(
            "Quit Game?",
            "Current progress will be lost. Quit anyway?",
            "Quit",
            primary: QuitNow,
            secondaryLabel: null,
            secondary: null,
            cancelLabel: "Cancel");
    }

    private void SaveNow(string slot, string display)
    {
        SaveGameResult result = saveLoad.SaveSlot(slot, display);
        SetStatus(result.message);

        RefreshSlotList();

        if (result.success)
            SelectSlotById(slot);
    }

    private bool TrySaveAutosaveBeforeLoad()
    {
        if (saveLoad == null)
        {
            SetStatus("No SaveLoadController found.");
            return false;
        }

        SaveGameResult result = saveLoad.SaveAutosaveBeforeLoad();
        SetStatus(result.message);
        RefreshSlotList();
        return result.success;
    }

    private bool TrySaveAutosaveBeforeExit()
    {
        if (saveLoad == null)
        {
            SetStatus("No SaveLoadController found.");
            return false;
        }

        SaveGameResult result = saveLoad.SaveAutosaveBeforeExit();
        SetStatus(result.message);
        RefreshSlotList();
        return result.success;
    }

    private void LoadNow(string slot)
    {
        SaveGameResult result = saveLoad.LoadSlot(slot);
        SetStatus(result.message);
    }

    private void ClickDeleteSelected()
    {
        if (saveLoad == null)
        {
            SetStatus("No SaveLoadController found.");
            return;
        }

        if (_selected == null)
        {
            SetStatus("No save selected.");
            return;
        }

        string slot = _selected.slotId;

        if (dialog == null)
        {
            DeleteNow(slot);
            return;
        }

        dialog.Show(
            "Delete Save?",
            $"Delete save '{slot}'? This cannot be undone.",
            "Delete",
            primary: () => DeleteNow(slot),
            secondaryLabel: null,
            secondary: null,
            cancelLabel: "Cancel");
    }

    private void DeleteNow(string slot)
    {
        bool deleted = saveLoad.DeleteSlot(slot);

        if (deleted)
        {
            SetStatus($"Deleted save '{slot}'.");
            _selected = null;
            RefreshSlotList();
            RefreshSelectedUI();
            RefreshButtons();
            RefreshRowSelectionVisuals();
        }
        else
        {
            SetStatus($"Could not delete save '{slot}'.");
            RefreshSlotList();
        }
    }

    private void OpenSaveFolder()
    {
        if (saveLoad != null)
            saveLoad.OpenSaveFolder();
    }

    private void RefreshSelectedUI()
    {
        if (selectedSlotText == null)
            return;

        if (_selected == null)
        {
            selectedSlotText.text = "Selected Save:\n(none)";
            return;
        }

        string display = string.IsNullOrWhiteSpace(_selected.displayName)
            ? _selected.slotId
            : _selected.displayName;

        string valid = _selected.isValid
            ? "Valid"
            : $"Invalid: {_selected.invalidReason}";

        selectedSlotText.text =
            $"Selected Save:\n" +
            $"{_selected.slotId}\n" +
            $"{display}\n" +
            $"Updated: {FormatUtc(_selected.updatedUtc)}\n" +
            $"Schema: {_selected.schemaVersion}\n" +
            $"{valid}";
    }

    private void RefreshButtons()
    {
        bool hasSaveLoad = saveLoad != null;
        bool hasSelection = _selected != null && _selected.isValid;

        string saveDisabledReason = null;
        bool canSaveNow = false;

        if (hasSaveLoad)
            canSaveNow = saveLoad.CanSaveNow(out saveDisabledReason);

        if (saveSelectedButton != null)
            saveSelectedButton.interactable = canSaveNow && hasSelection;

        if (loadSelectedButton != null)
            loadSelectedButton.interactable = hasSaveLoad && hasSelection;

        if (newSaveButton != null)
            newSaveButton.interactable = canSaveNow;

        if (deleteSelectedButton != null)
            deleteSelectedButton.interactable = hasSaveLoad && hasSelection;

        if (!hasSaveLoad)
        {
            SetStatus("No SaveLoadController found.");
        }
        else if (!canSaveNow)
        {
            SetStatus(saveDisabledReason);
        }
        else if (_selected == null)
        {
            SetStatus("Select a save file.");
        }
    }

    private void RefreshVersionText()
    {
        if (versionText == null)
            return;

        versionText.text =
            $"Game Version: {Application.version}\n" +
            $"Save Version: {SaveSchema.CurrentVersion}";
    }

    private string GetTypedSlotId()
    {
        if (slotIdInput == null || string.IsNullOrWhiteSpace(slotIdInput.text))
            return saveLoad != null ? saveLoad.DefaultSlotId : "manual_001";

        return slotIdInput.text.Trim();
    }

    private string GetDisplayName(string fallback)
    {
        if (displayNameInput == null || string.IsNullOrWhiteSpace(displayNameInput.text))
            return fallback;

        return displayNameInput.text.Trim();
    }

    private void SelectSlotById(string slotId)
    {
        if (saveLoad == null || string.IsNullOrWhiteSpace(slotId))
            return;

        List<SaveSlotSummary> slots = saveLoad.ListSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].slotId == slotId)
            {
                SelectSlot(slots[i]);
                return;
            }
        }
    }

    private void ClearRows()
    {
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i] != null)
                Destroy(_spawnedRows[i].gameObject);
        }

        _spawnedRows.Clear();

        if (slotListRoot != null)
        {
            for (int i = slotListRoot.childCount - 1; i >= 0; i--)
                Destroy(slotListRoot.GetChild(i).gameObject);
        }
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg ?? "";
        else
            Debug.Log($"[SaveLoadPanelUI] {msg}", this);
    }

    private void RegisterWithEscape()
    {
        if (!closeViaGlobalEscapeRouter)
            return;

        EscapeCloseRegistry registry = EscapeCloseRegistry.TryGetOrFind();
        if (registry != null)
            registry.Register(this);
    }

    private void UnregisterFromEscape()
    {
        if (EscapeCloseRegistry.I != null)
            EscapeCloseRegistry.I.Unregister(this);
    }

    private void RefreshRowSelectionVisuals()
    {
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            SaveSlotRowUI row = _spawnedRows[i];
            if (row == null)
                continue;

            bool selected = _selected != null && row.SlotId == _selected.slotId;
            row.SetSelectedVisual(selected);
        }
    }

    private static string FormatUtc(string utc)
    {
        if (System.DateTime.TryParse(utc, out System.DateTime dt))
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        return utc;
    }

    private void QuitNow()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}