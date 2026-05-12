using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SaveLoadController : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private string profileId = "default";

    [Header("Default Slot")]
    [SerializeField] private string defaultSlotId = "manual_001";
    [SerializeField] private string defaultDisplayName = "Manual Save";

    [Header("Autosaves")]
    [SerializeField, Min(1)] private int maxAutosaves = 5;

    [Header("Scenes")]
    [SerializeField] private string nodeSceneName = "NodeScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    public string ProfileId => profileId;
    public string DefaultSlotId => defaultSlotId;
    public string NodeSceneName => nodeSceneName;

    public bool CanSaveNow(out string reason)
    {
        return SaveGameService.CanSaveInCurrentScene(nodeSceneName, out reason);
    }

    public bool IsInNodeScene()
    {
        return SaveGameService.IsCurrentSceneNodeScene(nodeSceneName);
    }

    public bool IsInMainMenuScene()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == mainMenuSceneName;
    }

    public bool SlotExists(string slotId)
    {
        return SaveGameService.SaveExists(NormalizeSlot(slotId), profileId);
    }

    public List<SaveSlotSummary> ListSlots()
    {
        return SaveGameService.ListSlots(profileId);
    }

    public SaveGameResult SaveSlot(string slotId, string displayName = null)
    {
        string finalSlot = NormalizeSlot(slotId);
        string finalDisplay = string.IsNullOrWhiteSpace(displayName)
            ? finalSlot
            : displayName;

        SaveGameResult result = SaveGameService.SaveSlot(
            finalSlot,
            profileId,
            finalDisplay,
            nodeSceneName);

        LogResult($"SaveSlot('{finalSlot}')", result);
        return result;
    }

    public SaveGameResult LoadSlot(string slotId)
    {
        string finalSlot = NormalizeSlot(slotId);

        SaveGameResult result = SaveGameService.LoadSlot(
            finalSlot,
            profileId,
            nodeSceneName);

        LogResult($"LoadSlot('{finalSlot}')", result);
        return result;
    }

    public SaveGameResult SaveDefaultSlot()
    {
        return SaveSlot(defaultSlotId, defaultDisplayName);
    }

    public SaveGameResult LoadDefaultSlot()
    {
        return LoadSlot(defaultSlotId);
    }

    public SaveGameResult SaveAutosave(string displayName = "Autosave")
    {
        SaveGameResult result = SaveGameService.SaveAutosave(
            profileId,
            displayName,
            nodeSceneName,
            maxAutosaves);

        LogResult("SaveAutosave", result);
        return result;
    }

    public SaveGameResult SaveAutosaveBeforeLoad()
    {
        return SaveAutosave("Autosave Before Load");
    }

    public SaveGameResult SaveAutosaveBeforeExit()
    {
        return SaveAutosave("Autosave Before Exit");
    }

    public void OpenSaveFolder()
    {
        SaveGameService.OpenSaveFolderInExplorer();
    }

    public bool ManualSlotExists()
    {
        return SlotExists(defaultSlotId);
    }

    public bool DeleteSlot(string slotId)
    {
        string finalSlot = string.IsNullOrWhiteSpace(slotId)
            ? defaultSlotId
            : slotId.Trim();

        bool deleted = SaveGameService.DeleteSlot(finalSlot, profileId);

        if (verboseLogging)
        {
            Debug.Log(
                $"[SaveLoadController:{name}] DeleteSlot('{finalSlot}') deleted={deleted}",
                this);
        }

        return deleted;
    }

    private string NormalizeSlot(string slotId)
    {
        return string.IsNullOrWhiteSpace(slotId)
            ? defaultSlotId
            : slotId.Trim();
    }

    [ContextMenu("DEBUG Save Default Slot")]
    private void DebugSaveDefaultSlot()
    {
        SaveDefaultSlot();
    }

    [ContextMenu("DEBUG Load Default Slot")]
    private void DebugLoadDefaultSlot()
    {
        LoadDefaultSlot();
    }

    [ContextMenu("DEBUG Save Autosave")]
    private void DebugSaveAutosave()
    {
        SaveAutosave();
    }

    [ContextMenu("DEBUG Open Save Folder")]
    private void DebugOpenSaveFolder()
    {
        OpenSaveFolder();
    }

    private void LogResult(string action, SaveGameResult result)
    {
        if (result.success)
        {
            if (verboseLogging)
                Debug.Log($"[SaveLoadController:{name}] {action} | {result}", this);
        }
        else
        {
            Debug.LogWarning($"[SaveLoadController:{name}] {action} | {result}", this);
        }
    }
}