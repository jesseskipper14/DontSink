using System;
using System.Collections.Generic;

/// <summary>
/// Player/crew knowledge gate for constellations. Nothing currently auto-verifies constellations.
/// A future NPC chart-verification service should call MarkVerified, and save persistence will be
/// wired when the physical chart/knowledge state lands.
/// </summary>
[Serializable]
public sealed class CelestialConstellationKnowledgeState
{
    public List<string> verifiedConstellationIds = new();

    public bool IsVerified(string constellationStableId)
    {
        if (string.IsNullOrWhiteSpace(constellationStableId) || verifiedConstellationIds == null)
            return false;

        return verifiedConstellationIds.Contains(constellationStableId);
    }

    public bool MarkVerified(string constellationStableId)
    {
        if (string.IsNullOrWhiteSpace(constellationStableId))
            return false;

        verifiedConstellationIds ??= new List<string>();
        if (verifiedConstellationIds.Contains(constellationStableId))
            return false;

        verifiedConstellationIds.Add(constellationStableId);
        return true;
    }

    public bool TryGetVerifiedDisplayName(
        CelestialConstellationCatalog catalog,
        string constellationStableId,
        out string displayName)
    {
        displayName = null;

        if (!IsVerified(constellationStableId) || catalog == null)
            return false;

        if (!catalog.TryGetById(constellationStableId, out CelestialConstellation constellation) || constellation == null)
            return false;

        displayName = constellation.TruthName;
        return !string.IsNullOrWhiteSpace(displayName);
    }
}
