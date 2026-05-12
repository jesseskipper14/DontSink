using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveGameService
{
    private const string SavesFolderName = "Saves";
    private const string DefaultProfileId = "default";

    private const string AutosavePrefix = "autosave_";
    private const int DefaultMaxAutosaves = 5;

    public static string SaveRoot =>
        Path.Combine(Application.persistentDataPath, SavesFolderName);

    public static string GetProfileFolder(string profileId = DefaultProfileId)
    {
        string safeProfile = SanitizeFileName(string.IsNullOrWhiteSpace(profileId)
            ? DefaultProfileId
            : profileId);

        return Path.Combine(SaveRoot, safeProfile);
    }

    public static string GetSlotPath(string slotId, string profileId = DefaultProfileId)
    {
        string safeSlot = SanitizeFileName(string.IsNullOrWhiteSpace(slotId)
            ? "manual_001"
            : slotId);

        return Path.Combine(GetProfileFolder(profileId), safeSlot + ".json");
    }

    public static SaveGameResult SaveSlot(
        string slotId,
        string profileId,
        string displayName,
        string nodeSceneName)
    {
        GameState gs = GameState.I;
        if (gs == null)
            return new SaveGameResult(false, "GameState.I is null.");

        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene != nodeSceneName)
        {
            return new SaveGameResult(
                false,
                currentScene == "BoatScene"
                    ? "Save is disabled during travel. Return to a node to save."
                    : $"Save is disabled here. Saving is only allowed in '{nodeSceneName}'.");
        }

        if (HasMeaningfulActiveTravel(gs.activeTravel))
        {
            return new SaveGameResult(
                false,
                "Save is disabled during active travel.");
        }

        RefreshRuntimeStateBeforeSave(gs);

        string path = GetSlotPath(slotId, profileId);
        string now = DateTime.UtcNow.ToString("O");

        SaveGameFile existing = TryReadExisting(path);

        var file = new SaveGameFile
        {
            schemaVersion = SaveSchema.CurrentVersion,
            gameVersion = Application.version,
            saveId = string.IsNullOrWhiteSpace(slotId) ? "manual_001" : slotId,
            profileId = string.IsNullOrWhiteSpace(profileId) ? DefaultProfileId : profileId,
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Manual Save" : displayName,
            createdUtc = existing != null && !string.IsNullOrWhiteSpace(existing.createdUtc)
                ? existing.createdUtc
                : now,
            updatedUtc = now,
            payload = BuildPayload(gs, currentScene)
        };

        // v1 is NodeScene-only. Keep this explicit, because JsonUtility enjoys fake-null chaos.
        if (file.payload != null)
            file.payload.activeTravel = null;

        try
        {
            string json = JsonUtility.ToJson(file, prettyPrint: true);
            WriteAllTextAtomic(path, json);

            return new SaveGameResult(true, "Saved game.", path);
        }
        catch (Exception ex)
        {
            return new SaveGameResult(false, $"Failed to write save: {ex.Message}", path);
        }
    }

    public static SaveGameResult SaveAutosave(
        string profileId,
        string displayName,
        string nodeSceneName,
        int maxAutosaves = DefaultMaxAutosaves)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string slotId = AutosavePrefix + timestamp;

        SaveGameResult result = SaveSlot(
            slotId,
            profileId,
            string.IsNullOrWhiteSpace(displayName) ? "Autosave" : displayName,
            nodeSceneName);

        if (result.success)
            PruneAutosaves(profileId, maxAutosaves);

        return result;
    }

    public static void PruneAutosaves(
        string profileId = DefaultProfileId,
        int maxAutosaves = DefaultMaxAutosaves)
    {
        maxAutosaves = Mathf.Max(0, maxAutosaves);
        if (maxAutosaves <= 0)
            return;

        List<SaveSlotSummary> all = ListSlots(profileId);
        List<SaveSlotSummary> autosaves = new List<SaveSlotSummary>();

        for (int i = 0; i < all.Count; i++)
        {
            SaveSlotSummary s = all[i];
            if (s == null)
                continue;

            if (!string.IsNullOrWhiteSpace(s.slotId) &&
                s.slotId.StartsWith(AutosavePrefix, StringComparison.OrdinalIgnoreCase))
            {
                autosaves.Add(s);
            }
        }

        autosaves.Sort((a, b) => string.CompareOrdinal(b.updatedUtc, a.updatedUtc));

        for (int i = maxAutosaves; i < autosaves.Count; i++)
        {
            SaveSlotSummary old = autosaves[i];
            if (old == null || string.IsNullOrWhiteSpace(old.slotId))
                continue;

            DeleteSlot(old.slotId, profileId);
        }
    }

    public static SaveGameResult LoadSlot(
        string slotId,
        string profileId,
        string nodeSceneName)
    {
        string path = GetSlotPath(slotId, profileId);

        if (!File.Exists(path))
            return new SaveGameResult(false, "Save file does not exist.", path);

        SaveGameFile file;

        try
        {
            string json = File.ReadAllText(path);
            file = JsonUtility.FromJson<SaveGameFile>(json);
        }
        catch (Exception ex)
        {
            return new SaveGameResult(false, $"Failed to read/parse save: {ex.Message}", path);
        }

        SaveGameResult validation = ValidateForLoad(file, nodeSceneName, path);
        if (!validation.success)
            return validation;

        EnsureCoreSingletons();

        GameState gs = GameState.I;
        if (gs == null)
            return new SaveGameResult(false, "GameState.I is null after ensuring singletons.", path);

        ApplyPayloadToGameState(gs, file.payload);

        SceneManager.LoadScene(nodeSceneName);

        return new SaveGameResult(true, "Loaded game into GameState and loading NodeScene.", path);
    }

    public static bool SaveExists(string slotId, string profileId = DefaultProfileId)
    {
        return File.Exists(GetSlotPath(slotId, profileId));
    }

    public static bool CanSaveInCurrentScene(string nodeSceneName, out string reason)
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene != nodeSceneName)
        {
            reason = currentScene == "BoatScene"
                ? "Save is disabled during travel. Return to a node to save."
                : $"Save is disabled here. Saving is only allowed in '{nodeSceneName}'.";

            return false;
        }

        GameState gs = GameState.I;
        if (gs != null && HasMeaningfulActiveTravel(gs.activeTravel))
        {
            reason = "Save is disabled during active travel.";
            return false;
        }

        reason = null;
        return true;
    }

    public static bool IsCurrentSceneNodeScene(string nodeSceneName)
    {
        return SceneManager.GetActiveScene().name == nodeSceneName;
    }

    public static List<SaveSlotSummary> ListSlots(string profileId = DefaultProfileId)
    {
        var result = new List<SaveSlotSummary>();

        string folder = GetProfileFolder(profileId);
        if (!Directory.Exists(folder))
            return result;

        string[] files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);

        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i];
            SaveSlotSummary summary = ReadSummary(path, profileId);
            result.Add(summary);
        }

        result.Sort((a, b) => string.CompareOrdinal(b.updatedUtc, a.updatedUtc));
        return result;
    }

    public static SaveSlotSummary ReadSummary(string path, string profileId = DefaultProfileId)
    {
        var summary = new SaveSlotSummary
        {
            slotId = Path.GetFileNameWithoutExtension(path),
            profileId = profileId,
            path = path,
            isValid = false
        };

        try
        {
            string json = File.ReadAllText(path);
            SaveGameFile file = JsonUtility.FromJson<SaveGameFile>(json);

            if (file == null)
            {
                summary.invalidReason = "File parsed to null.";
                return summary;
            }

            summary.slotId = string.IsNullOrWhiteSpace(file.saveId)
                ? summary.slotId
                : file.saveId;

            summary.profileId = string.IsNullOrWhiteSpace(file.profileId)
                ? profileId
                : file.profileId;

            summary.displayName = file.displayName;
            summary.createdUtc = file.createdUtc;
            summary.updatedUtc = file.updatedUtc;
            summary.schemaVersion = file.schemaVersion;
            summary.gameVersion = file.gameVersion;

            if (file.schemaVersion > SaveSchema.CurrentVersion)
            {
                summary.invalidReason = $"Save is from newer schema {file.schemaVersion}.";
                return summary;
            }

            if (file.schemaVersion < SaveSchema.MinimumSupportedVersion)
            {
                summary.invalidReason = $"Save schema {file.schemaVersion} is too old.";
                return summary;
            }

            if (file.payload == null)
            {
                summary.invalidReason = "Save payload is null.";
                return summary;
            }

            if (file.payload.boat == null)
            {
                summary.invalidReason = "Save boat state is null.";
                return summary;
            }

            summary.isValid = true;
            return summary;
        }
        catch (Exception ex)
        {
            summary.invalidReason = ex.Message;
            return summary;
        }
    }

    public static bool DeleteSlot(string slotId, string profileId = DefaultProfileId)
    {
        string path = GetSlotPath(slotId, profileId);
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    public static void OpenSaveFolderInExplorer()
    {
        Directory.CreateDirectory(SaveRoot);
        Application.OpenURL(SaveRoot);
    }

    private static void RefreshRuntimeStateBeforeSave(GameState gs)
    {
        SceneTransitionController transition = SceneTransitionController.I;

        if (transition != null)
        {
            transition.SaveCurrentPlayerLoadout();
            CapturePlayerSceneContext(gs, "SaveGameService.SaveSlot");
            transition.SaveCurrentBoatState("SaveGameService.SaveSlot");
            return;
        }

        Debug.LogWarning(
            "[SaveGameService] SceneTransitionController.I is null. " +
            "Saving existing GameState without refreshing runtime boat/loadout state. " +
            "This is probably not what you want, unless you enjoy haunted saves.");
    }

    private static SaveGamePayload BuildPayload(GameState gs, string currentScene)
    {
        return new SaveGamePayload
        {
            currentSceneName = currentScene,
            player = gs.player,
            worldMap = gs.worldMap,
            activeTravel = null,
            playerLoadout = gs.playerLoadout,
            playerSceneContext = gs.playerSceneContext,
            boat = gs.boat
        };
    }

    private static SaveGameResult ValidateForLoad(SaveGameFile file, string nodeSceneName, string path)
    {
        if (file == null)
            return new SaveGameResult(false, "Save file parsed to null.", path);

        if (file.schemaVersion > SaveSchema.CurrentVersion)
        {
            return new SaveGameResult(
                false,
                $"Save schema {file.schemaVersion} is newer than supported schema {SaveSchema.CurrentVersion}.",
                path);
        }

        if (file.schemaVersion < SaveSchema.MinimumSupportedVersion)
        {
            return new SaveGameResult(
                false,
                $"Save schema {file.schemaVersion} is older than minimum supported schema {SaveSchema.MinimumSupportedVersion}.",
                path);
        }

        if (file.schemaVersion != SaveSchema.CurrentVersion)
        {
            return new SaveGameResult(
                false,
                $"Save schema {file.schemaVersion} requires migration, but no migration exists yet.",
                path);
        }

        if (file.payload == null)
            return new SaveGameResult(false, "Save payload is null.", path);

        if (!string.IsNullOrWhiteSpace(file.payload.currentSceneName) &&
            file.payload.currentSceneName != nodeSceneName)
        {
            return new SaveGameResult(
                false,
                $"This save was made in '{file.payload.currentSceneName}', but v1 only loads NodeScene saves.",
                path);
        }

        if (HasMeaningfulActiveTravel(file.payload.activeTravel))
        {
            return new SaveGameResult(
                false,
                "This save contains meaningful activeTravel. v1 refuses to load mid-travel saves.",
                path);
        }

        if (file.payload.boat == null)
            return new SaveGameResult(false, "Save payload boat state is null.", path);

        return new SaveGameResult(true, "Save is valid.", path);
    }

    private static void ApplyPayloadToGameState(GameState gs, SaveGamePayload payload)
    {
        gs.player = payload.player ?? new WorldMapPlayerState();
        gs.worldMap = payload.worldMap ?? new WorldMapSimState();

        // v1 load always starts from NodeScene.
        gs.activeTravel = null;

        gs.playerLoadout = payload.playerLoadout;
        gs.playerSceneContext = payload.playerSceneContext;

        gs.SetBoatSaveState(payload.boat, "SaveGameService.LoadSlot");
        gs.LogState("SaveGameService.ApplyPayloadToGameState");
    }

    private static SaveGameFile TryReadExisting(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SaveGameFile>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureCoreSingletons()
    {
        if (GameState.I == null)
        {
            GameObject go = new GameObject("GameState");
            go.AddComponent<GameState>();
        }

        if (SceneTransitionController.I == null)
        {
            GameObject go = new GameObject("SceneTransitionController");
            go.AddComponent<SceneTransitionController>();
        }
    }

    private static void CapturePlayerSceneContext(GameState gs, string reason)
    {
        if (gs == null)
            return;

        PlayerBoardingState boarding = UnityEngine.Object.FindAnyObjectByType<PlayerBoardingState>();

        if (boarding == null)
        {
            gs.SetPlayerSceneContext(new PlayerSceneContextSnapshot
            {
                version = 1,
                hasValue = false,
                wasBoarded = false,
                boatInstanceId = null
            }, reason);

            return;
        }

        string boatInstanceId = null;

        if (boarding.IsBoarded && boarding.CurrentBoatRoot != null)
        {
            Boat boat =
                boarding.CurrentBoatRoot.GetComponent<Boat>() ??
                boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

            if (boat != null)
                boatInstanceId = boat.BoatInstanceId;
        }

        gs.SetPlayerSceneContext(new PlayerSceneContextSnapshot
        {
            version = 1,
            hasValue = true,
            wasBoarded = boarding.IsBoarded,
            boatInstanceId = boatInstanceId
        }, reason);
    }

    private static void WriteAllTextAtomic(string path, string contents)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string tempPath = path + ".tmp";
        string backupPath = path + ".bak";

        File.WriteAllText(tempPath, contents);

        if (File.Exists(path))
        {
            try
            {
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);

                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                return;
            }
            catch
            {
                // Fall through to manual replacement for platforms/filesystems where File.Replace is cranky.
            }
        }

        try
        {
            if (File.Exists(path))
            {
                File.Copy(path, backupPath, overwrite: true);
                File.Delete(path);
            }

            File.Move(tempPath, path);

            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
        catch
        {
            if (!File.Exists(path) && File.Exists(backupPath))
                File.Copy(backupPath, path, overwrite: true);

            throw;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "save";

        foreach (char c in Path.GetInvalidFileNameChars())
            raw = raw.Replace(c, '_');

        return raw.Trim();
    }

    private static bool HasMeaningfulActiveTravel(TravelPayload travel)
    {
        if (travel == null)
            return false;

        if (!string.IsNullOrWhiteSpace(travel.fromNodeStableId))
            return true;

        if (!string.IsNullOrWhiteSpace(travel.toNodeStableId))
            return true;

        if (!string.IsNullOrWhiteSpace(travel.boatInstanceId))
            return true;

        if (!string.IsNullOrWhiteSpace(travel.boatPrefabGuid))
            return true;

        if (travel.seed != 0)
            return true;

        if (!Mathf.Approximately(travel.routeLength, 0f))
            return true;

        if (travel.cargoManifest != null && travel.cargoManifest.Count > 0)
            return true;

        return false;
    }
}