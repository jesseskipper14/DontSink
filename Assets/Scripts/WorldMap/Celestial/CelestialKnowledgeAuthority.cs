using System;
using System.Collections.Generic;
using UnityEngine;

public enum CelestialSubjectKind { LandmarkStar = 1, Nebula = 2, DeepSkyObject = 3, Constellation = 100 }

[Serializable]
public sealed class CelestialSubjectAnnotationSnapshot
{
    public string subjectStableId;
    public CelestialSubjectKind subjectKind;
    public string playerName;
    public string note;
    public int revision;
    public string lastEditedByPlayerKey;
    public bool HasData => !string.IsNullOrWhiteSpace(playerName) || !string.IsNullOrWhiteSpace(note);
    public CelestialSubjectAnnotationSnapshot Copy() => (CelestialSubjectAnnotationSnapshot)MemberwiseClone();
}

public readonly struct CelestialConstellationProgress
{
    public readonly int charted;
    public readonly int total;
    public bool Eligible => total >= 2 && charted == total;
    public CelestialConstellationProgress(int charted, int total) { this.charted = charted; this.total = total; }
    public override string ToString() => $"{charted} / {total} member stars charted";
}

/// <summary>Read-only evidence/knowledge queries. No view operation grants knowledge.</summary>
public static class CelestialKnowledgeQueries
{
    public static HashSet<string> CollectChartedObjectIds(CelestialChartStateSnapshot state, CelestialField field)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (state?.fragments == null || field == null || !field.IsValid) return ids;
        foreach (var fragment in state.fragments)
        {
            if (!MatchesField(fragment, field) || fragment.marks == null) continue;
            foreach (var mark in fragment.marks)
                if (mark != null && !string.IsNullOrWhiteSpace(mark.celestialObjectStableId))
                    ids.Add(mark.celestialObjectStableId);
        }
        return ids;
    }

    public static bool MatchesField(CelestialChartFragmentSnapshot fragment, CelestialField field) =>
        fragment != null && field != null && field.Identity != null &&
        fragment.worldSeed == field.WorldSeed && fragment.celestialGeneratorVersion == field.Identity.generatorVersion &&
        string.Equals(fragment.celestialConfigHash, field.Identity.configHash, StringComparison.Ordinal);

    public static bool IsCelestialObjectCharted(CelestialChartStateSnapshot state, CelestialField field, string id) =>
        !string.IsNullOrWhiteSpace(id) && CollectChartedObjectIds(state, field).Contains(id);

    public static bool IsConstellationKnown(CelestialChartStateSnapshot state, string id) =>
        !string.IsNullOrWhiteSpace(id) && state?.verifiedConstellationIds != null && state.verifiedConstellationIds.Contains(id);

    public static CelestialConstellationProgress GetConstellationChartProgress(
        CelestialChartStateSnapshot state, CelestialField field, string id)
    {
        if (field == null || !field.TryResolveConstellation(id, out var constellation)) return default;
        return GetProgress(constellation, CollectChartedObjectIds(state, field));
    }

    public static CelestialConstellationProgress GetProgress(CelestialConstellation constellation, HashSet<string> charted)
    {
        int count = 0;
        foreach (string member in constellation.MemberStarStableIds) if (charted.Contains(member)) count++;
        return new CelestialConstellationProgress(count, constellation.MemberStarStableIds.Count);
    }

    public static bool CanValidateConstellation(CelestialChartStateSnapshot state, CelestialField field, string id) =>
        GetConstellationChartProgress(state, field, id).Eligible;

    public static bool CanAnnotateSubject(CelestialChartStateSnapshot state, CelestialField field, string id, CelestialSubjectKind kind)
    {
        if (field == null || !field.IsValid || string.IsNullOrWhiteSpace(id)) return false;
        if (kind == CelestialSubjectKind.Constellation)
            return field.TryResolveConstellation(id, out _) && IsConstellationKnown(state, id);
        return field.TryResolveObject(id, out var obj) && obj.Kind != CelestialObjectKind.AmbientStar &&
            (int)obj.Kind == (int)kind && IsCelestialObjectCharted(state, field, id);
    }

    public static bool TryGetCrewCelestialAnnotation(CelestialChartStateSnapshot state, CelestialField field,
        string id, CelestialSubjectKind kind, out CelestialSubjectAnnotationSnapshot annotation)
    {
        annotation = null;
        if (!CanAnnotateSubject(state, field, id, kind)) return false;
        var stored = FindAnnotation(state, id, kind);
        if (stored == null || !stored.HasData) return false;
        annotation = stored.Copy(); // Content consumers cannot mutate the shared record.
        return true;
    }

    public static bool TryGetCrewCelestialName(CelestialChartStateSnapshot state, CelestialField field,
        string id, CelestialSubjectKind kind, out string playerName)
    {
        playerName = null;
        if (!TryGetCrewCelestialAnnotation(state, field, id, kind, out var annotation)) return false;
        playerName = annotation.playerName;
        return !string.IsNullOrWhiteSpace(playerName);
    }

    public static int GetAnnotationRevision(CelestialChartStateSnapshot state, string id, CelestialSubjectKind kind) =>
        FindAnnotation(state, id, kind)?.revision ?? 0;

    internal static CelestialSubjectAnnotationSnapshot FindAnnotation(CelestialChartStateSnapshot state, string id, CelestialSubjectKind kind)
    {
        if (state?.annotations == null) return null;
        foreach (var annotation in state.annotations)
            if (annotation != null && annotation.subjectStableId == id && annotation.subjectKind == kind) return annotation;
        return null;
    }
}

/// <summary>Shared crew mutation boundary. Requesters must later be supplied by authenticated transport.</summary>
public static class CelestialKnowledgeAuthority
{
    public static bool TrySetAnnotation(GameObject requester, CelestialField field, string id, CelestialSubjectKind kind,
        int expectedRevision, string playerName, string note, out string reason)
    {
        if (!ResolveMutation(requester, field, out var state, out string key, out reason)) return false;
        if (!CelestialKnowledgeQueries.CanAnnotateSubject(state, field, id, kind))
        { reason = "This subject requires chart evidence; constellations must be Known. Ambient stars cannot be annotated."; return false; }
        var annotation = CelestialKnowledgeQueries.FindAnnotation(state, id, kind);
        if ((annotation?.revision ?? 0) != expectedRevision)
        { reason = "The shared annotation changed. Reload it before saving your draft."; return false; }
        playerName = NormalizeText(playerName, 128);
        note = NormalizeText(note, 2048);
        if (annotation != null && annotation.playerName == playerName && annotation.note == note)
        { reason = "Annotation unchanged."; return true; }
        if (annotation == null && playerName.Length == 0 && note.Length == 0)
        { reason = "Annotation empty."; return true; }
        FreezeGeneration(state, field);
        if (annotation == null)
        {
            annotation = new CelestialSubjectAnnotationSnapshot { subjectStableId = id, subjectKind = kind };
            state.annotations.Add(annotation);
        }
        annotation.playerName = playerName;
        annotation.note = note;
        annotation.revision++;
        annotation.lastEditedByPlayerKey = key;
        // Retain empty records as revision tombstones: an old editor must not recreate stale data.
        state.knowledgeRevision++;
        reason = "Crew annotation saved.";
        return true;
    }

    public static bool TrySetSubjectName(GameObject requester, CelestialField field, string id, CelestialSubjectKind kind,
        int expectedRevision, string name, out string reason) => TrySetAnnotation(requester, field, id, kind,
            expectedRevision, name, CelestialKnowledgeQueries.FindAnnotation(GameState.I?.celestialCharts, id, kind)?.note, out reason);

    public static bool TrySetSubjectNote(GameObject requester, CelestialField field, string id, CelestialSubjectKind kind,
        int expectedRevision, string note, out string reason) => TrySetAnnotation(requester, field, id, kind,
            expectedRevision, CelestialKnowledgeQueries.FindAnnotation(GameState.I?.celestialCharts, id, kind)?.playerName, note, out reason);

    public static bool TryClearSubjectName(GameObject requester, CelestialField field, string id, CelestialSubjectKind kind,
        int revision, out string reason) => TrySetSubjectName(requester, field, id, kind, revision, "", out reason);
    public static bool TryClearSubjectNote(GameObject requester, CelestialField field, string id, CelestialSubjectKind kind,
        int revision, out string reason) => TrySetSubjectNote(requester, field, id, kind, revision, "", out reason);

    public static bool TryValidateConstellation(GameObject requester, CelestialField field, string id, out string reason)
    {
        if (!ResolveMutation(requester, field, out var state, out string key, out reason)) return false;
        var progress = CelestialKnowledgeQueries.GetConstellationChartProgress(state, field, id);
        if (!progress.Eligible) { reason = "Validation rejected: " + progress; return false; }
        if (CelestialKnowledgeQueries.IsConstellationKnown(state, id))
        { reason = "Constellation already Known."; return true; }
        FreezeGeneration(state, field);
        state.verifiedConstellationIds.Add(id);
        state.knowledgeRevision++;
        state.lastKnowledgeEditorPlayerKey = key;
        reason = "Constellation validated: " + progress;
        return true;
    }

    public static bool TryResetConstellationForDebug(GameObject requester, CelestialField field, string id, out string reason)
    {
        if (!ResolveMutation(requester, field, out var state, out string key, out reason)) return false;
        if (!field.TryResolveConstellation(id, out _)) { reason = "Constellation not found."; return false; }
        if (state.verifiedConstellationIds.Remove(id))
        { state.knowledgeRevision++; state.lastKnowledgeEditorPlayerKey = key; }
        reason = "Constellation Hidden. Evidence and annotations retained.";
        return true;
    }

    public static int ValidateAllEligibleForDebug(GameObject requester, CelestialField field, out string reason)
    {
        if (!ResolveMutation(requester, field, out var state, out _, out reason)) return 0;
        var charted = CelestialKnowledgeQueries.CollectChartedObjectIds(state, field);
        int count = 0;
        foreach (var constellation in field.Constellations.All)
            if (!CelestialKnowledgeQueries.IsConstellationKnown(state, constellation.StableId) &&
                CelestialKnowledgeQueries.GetProgress(constellation, charted).Eligible &&
                TryValidateConstellation(requester, field, constellation.StableId, out _)) count++;
        reason = $"Validated {count} eligible constellations.";
        return count;
    }

    internal static void FreezeGeneration(CelestialChartStateSnapshot state, CelestialField field)
    {
        state.constellationWorldSeed = field.WorldSeed;
        state.constellationFieldHash = field.Identity.configHash;
        state.constellationGeneration = field.ConstellationConfig.Clone();
    }

    private static bool ResolveMutation(GameObject requester, CelestialField field,
        out CelestialChartStateSnapshot state, out string key, out string reason)
    {
        state = null; key = null; reason = null;
        if (!GameplayAuthority.IsAuthoritative) { reason = "Celestial changes require gameplay authority."; return false; }
        if (requester == null) { reason = "An exact requester is required."; return false; }
        if (GameState.I == null || field == null || !field.IsValid) { reason = "Celestial state is unavailable."; return false; }
        GameState.I.EnsureCelestialChartDefaults();
        state = GameState.I.celestialCharts;
        var persistence = requester.GetComponent<PlayerLoadoutPersistence>() ??
            requester.GetComponentInParent<PlayerLoadoutPersistence>(true) ?? requester.GetComponentInChildren<PlayerLoadoutPersistence>(true);
        key = persistence != null ? persistence.PersistenceKey : GameState.I.LocalPlayerPersistenceKey;
        if (string.IsNullOrWhiteSpace(key)) { reason = "Requester has no persistence identity."; return false; }
        if (state.constellationGeneration != null &&
            (state.constellationWorldSeed != field.WorldSeed || state.constellationFieldHash != field.Identity.configHash ||
             state.constellationGeneration.Fingerprint != field.ConstellationConfig.Fingerprint))
        { reason = "The celestial field changed. Reopen the view before editing."; return false; }
        return true;
    }

    private static string NormalizeText(string text, int maxLength)
    {
        text = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        return text.Length > maxLength ? text.Substring(0, maxLength) : text;
    }
}
