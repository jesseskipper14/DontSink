using System.Collections.Generic;
using UnityEngine;

public static class SaveCompatibilityDiagnostics
{
    public static SaveCompatibilityReport ValidateBoatCatalog(BoatCatalog catalog)
    {
        SaveCompatibilityReport report = new();

        if (catalog == null)
        {
            report.Error("BoatCatalog is null. Cannot validate boat prefab GUIDs.");
            return report;
        }

        IReadOnlyList<BoatCatalog.Entry> entries = catalog.Entries;

        if (entries == null)
        {
            report.Error($"BoatCatalog '{catalog.name}' entries list is null.");
            return report;
        }

        if (entries.Count == 0)
        {
            report.Warning($"BoatCatalog '{catalog.name}' has no entries.");
            return report;
        }

        Dictionary<string, string> firstPrefabByGuid = new();

        for (int i = 0; i < entries.Count; i++)
        {
            BoatCatalog.Entry e = entries[i];

            if (e == null)
            {
                report.Warning($"BoatCatalog '{catalog.name}' entry {i} is null.");
                continue;
            }

            if (e.prefab == null)
            {
                report.Error($"BoatCatalog '{catalog.name}' entry {i} has no prefab.");
                continue;
            }

            string entryGuid = e.guid;

            if (string.IsNullOrWhiteSpace(entryGuid))
            {
                report.Error($"BoatCatalog '{catalog.name}' entry {i} prefab '{e.prefab.name}' has empty catalog GUID.");
                continue;
            }

            BoatIdentity identity = e.prefab.GetComponent<BoatIdentity>();

            if (identity == null)
            {
                report.Error($"BoatCatalog '{catalog.name}' entry {i} prefab '{e.prefab.name}' is missing BoatIdentity on prefab root.");
                continue;
            }

            string identityGuid = identity.BoatGuid;

            if (string.IsNullOrWhiteSpace(identityGuid))
            {
                report.Error($"BoatCatalog '{catalog.name}' entry {i} prefab '{e.prefab.name}' has empty BoatIdentity.BoatGuid.");
                continue;
            }

            if (entryGuid != identityGuid)
            {
                report.Error(
                    $"BoatCatalog '{catalog.name}' entry {i} prefab '{e.prefab.name}' GUID mismatch. " +
                    $"catalog='{entryGuid}', BoatIdentity='{identityGuid}'. Run Sync GUIDs From Prefabs or fix the prefab.");
            }

            if (firstPrefabByGuid.TryGetValue(entryGuid, out string firstPrefab))
            {
                report.Error(
                    $"BoatCatalog '{catalog.name}' duplicate boat GUID '{entryGuid}'. " +
                    $"First prefab='{firstPrefab}', duplicate prefab='{e.prefab.name}'.");
            }
            else
            {
                firstPrefabByGuid.Add(entryGuid, e.prefab.name);
            }
        }

        if (!report.HasErrors && !report.HasWarnings)
            report.AddInfo($"BoatCatalog '{catalog.name}' passed validation. Entries={entries.Count}.");

        return report;
    }

    public static SaveCompatibilityReport ValidateCurrentGameStateForSave(
        GameState gameState,
        BoatCatalog boatCatalog)
    {
        SaveCompatibilityReport report = new();

        if (gameState == null)
        {
            report.Error("GameState is null. Cannot validate save compatibility.");
            return report;
        }

        report.Merge(ValidateBoatCatalog(boatCatalog));
        report.Merge(ValidateBoatState(gameState.boat, boatCatalog, "GameState.boat"));

        return report;
    }

    public static SaveCompatibilityReport ValidateSaveFileForLoad(
        SaveGameFile file,
        BoatCatalog boatCatalog)
    {
        SaveCompatibilityReport report = new();

        if (file == null)
        {
            report.Error("SaveGameFile is null.");
            return report;
        }

        if (file.payload == null)
        {
            report.Error("Save payload is null.");
            return report;
        }

        report.Merge(ValidateBoatCatalog(boatCatalog));
        report.Merge(ValidateBoatState(file.payload.boat, boatCatalog, "Save payload boat"));

        return report;
    }

    public static SaveCompatibilityReport ValidateBoatState(
        BoatSaveState boatState,
        BoatCatalog boatCatalog,
        string label)
    {
        SaveCompatibilityReport report = new();

        if (boatState == null)
        {
            report.Error($"{label} is null.");
            return report;
        }

        if (string.IsNullOrWhiteSpace(boatState.boatInstanceId))
            report.Warning($"{label} has empty boatInstanceId.");

        if (string.IsNullOrWhiteSpace(boatState.boatPrefabGuid))
        {
            report.Error($"{label} has empty boatPrefabGuid. The boat prefab cannot be resolved safely.");
            return report;
        }

        if (boatCatalog == null)
        {
            report.Error($"{label} boatPrefabGuid='{boatState.boatPrefabGuid}' cannot be checked because BoatCatalog is null.");
            return report;
        }

        if (!TryFindBoatPrefabByGuid(
                boatCatalog,
                boatState.boatPrefabGuid,
                out GameObject prefab,
                out string reason))
        {
            report.Error($"{label} boatPrefabGuid='{boatState.boatPrefabGuid}' does not resolve. {reason}");
            return report;
        }

        report.AddInfo($"{label} boatPrefabGuid resolves to prefab '{prefab.name}'.");

        return report;
    }

    public static bool TryFindBoatPrefabByGuid(
        BoatCatalog catalog,
        string guid,
        out GameObject prefab,
        out string reason)
    {
        prefab = null;
        reason = null;

        if (catalog == null)
        {
            reason = "BoatCatalog is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(guid))
        {
            reason = "GUID is empty.";
            return false;
        }

        IReadOnlyList<BoatCatalog.Entry> entries = catalog.Entries;

        if (entries == null || entries.Count == 0)
        {
            reason = "BoatCatalog has no entries.";
            return false;
        }

        int matchCount = 0;
        GameObject first = null;

        for (int i = 0; i < entries.Count; i++)
        {
            BoatCatalog.Entry e = entries[i];
            if (e == null || e.prefab == null)
                continue;

            if (e.guid != guid)
                continue;

            matchCount++;

            if (first == null)
                first = e.prefab;
        }

        if (matchCount <= 0)
        {
            reason = "No catalog entry matched this GUID.";
            return false;
        }

        if (matchCount > 1)
        {
            reason = $"Catalog contains {matchCount} entries with this GUID.";
            return false;
        }

        prefab = first;
        return true;
    }
}