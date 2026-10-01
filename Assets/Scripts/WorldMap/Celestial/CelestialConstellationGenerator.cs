using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Derives stable constellations from the deterministic landmark-star field.
/// This does not alter CelestialField generation or its config hash; constellations are deterministic
/// derivative truth with their own versioned IDs.
/// </summary>
public static class CelestialConstellationGenerator
{
    public const int CurrentConstellationVersion = 1;

    private const int MinimumMembers = 4;
    private const int MinimumTargetMembers = 5;
    private const int MaximumTargetMembers = 7;

    public static CelestialConstellationCatalog Build(CelestialField field)
    {
        var result = new List<CelestialConstellation>();
        if (field == null || !field.IsValid)
            return new CelestialConstellationCatalog(result);

        var allObjects = new List<CelestialObject>();
        field.QueryAll(allObjects, clearResults: true);

        var landmarks = new List<CelestialObject>();
        for (int i = 0; i < allObjects.Count; i++)
        {
            CelestialObject obj = allObjects[i];
            if (obj != null && obj.Kind == CelestialObjectKind.LandmarkStar)
                landmarks.Add(obj);
        }

        landmarks.Sort((a, b) =>
        {
            ulong ah = PriorityHash(field, a.StableId);
            ulong bh = PriorityHash(field, b.StableId);
            int cmp = ah.CompareTo(bh);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.StableId, b.StableId);
        });

        var assigned = new HashSet<string>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        float nominalSpacing = EstimateLandmarkSpacing(field);
        float maxLinkDistance = Mathf.Max(18f, nominalSpacing * 2.65f);

        int ordinal = 0;
        for (int seedIndex = 0; seedIndex < landmarks.Count; seedIndex++)
        {
            CelestialObject seed = landmarks[seedIndex];
            if (seed == null || assigned.Contains(seed.StableId))
                continue;

            int targetCount = MinimumTargetMembers + (int)(PriorityHash(field, seed.StableId + ":size") %
                (ulong)(MaximumTargetMembers - MinimumTargetMembers + 1));

            var members = GrowLocalGroup(seed, landmarks, assigned, targetCount, maxLinkDistance, field);
            if (members.Count < MinimumMembers)
                continue;

            for (int i = 0; i < members.Count; i++)
                assigned.Add(members[i].StableId);

            OrderAsReadablePath(members);

            string stableId = BuildStableId(field, ordinal, members[0].StableId);
            string truthName = CelestialConstellationNameGenerator.GenerateUniqueName(stableId, usedNames);

            var memberIds = new List<string>(members.Count);
            var edges = new List<CelestialConstellationEdge>(Mathf.Max(0, members.Count - 1));
            Vector2 centroid = Vector2.zero;

            for (int i = 0; i < members.Count; i++)
            {
                memberIds.Add(members[i].StableId);
                centroid += members[i].WorldPosition;

                if (i > 0)
                    edges.Add(new CelestialConstellationEdge(members[i - 1].StableId, members[i].StableId));
            }

            centroid /= Mathf.Max(1, members.Count);
            result.Add(new CelestialConstellation(stableId, truthName, centroid, memberIds, edges));
            ordinal++;
        }

        result.Sort((a, b) => string.CompareOrdinal(a.StableId, b.StableId));
        return new CelestialConstellationCatalog(result);
    }

    private static List<CelestialObject> GrowLocalGroup(
        CelestialObject seed,
        List<CelestialObject> allLandmarks,
        HashSet<string> assigned,
        int targetCount,
        float maxLinkDistance,
        CelestialField field)
    {
        var selected = new List<CelestialObject> { seed };

        while (selected.Count < targetCount)
        {
            CelestialObject best = null;
            float bestDistance = float.PositiveInfinity;
            ulong bestTie = ulong.MaxValue;

            for (int i = 0; i < allLandmarks.Count; i++)
            {
                CelestialObject candidate = allLandmarks[i];
                if (candidate == null || assigned.Contains(candidate.StableId) || selected.Contains(candidate))
                    continue;

                float nearest = float.PositiveInfinity;
                for (int j = 0; j < selected.Count; j++)
                    nearest = Mathf.Min(nearest, Vector2.Distance(candidate.WorldPosition, selected[j].WorldPosition));

                if (nearest > maxLinkDistance)
                    continue;

                ulong tie = PriorityHash(field, seed.StableId + ":" + candidate.StableId);
                if (nearest < bestDistance - 0.0001f ||
                    (Mathf.Abs(nearest - bestDistance) <= 0.0001f && tie < bestTie))
                {
                    best = candidate;
                    bestDistance = nearest;
                    bestTie = tie;
                }
            }

            if (best == null)
                break;

            selected.Add(best);
        }

        return selected;
    }

    private static void OrderAsReadablePath(List<CelestialObject> members)
    {
        if (members == null || members.Count <= 2)
            return;

        var remaining = new List<CelestialObject>(members);
        members.Clear();

        int startIndex = 0;
        for (int i = 1; i < remaining.Count; i++)
        {
            Vector2 p = remaining[i].WorldPosition;
            Vector2 current = remaining[startIndex].WorldPosition;
            if (p.x < current.x || (Mathf.Approximately(p.x, current.x) && p.y < current.y))
                startIndex = i;
        }

        CelestialObject currentStar = remaining[startIndex];
        members.Add(currentStar);
        remaining.RemoveAt(startIndex);

        while (remaining.Count > 0)
        {
            int nearestIndex = 0;
            float nearestDistance = Vector2.Distance(currentStar.WorldPosition, remaining[0].WorldPosition);

            for (int i = 1; i < remaining.Count; i++)
            {
                float d = Vector2.Distance(currentStar.WorldPosition, remaining[i].WorldPosition);
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearestIndex = i;
                }
            }

            currentStar = remaining[nearestIndex];
            members.Add(currentStar);
            remaining.RemoveAt(nearestIndex);
        }
    }

    private static float EstimateLandmarkSpacing(CelestialField field)
    {
        float density = field.Config != null ? Mathf.Max(0.001f, field.Config.landmarkStarsPer1000WorldArea) : 2f;
        return Mathf.Sqrt(1000f / density);
    }

    private static string BuildStableId(CelestialField field, int ordinal, string firstMemberId)
    {
        uint seedBits = unchecked((uint)field.WorldSeed);
        ulong memberHash = CelestialConstellationNameGenerator.Hash64(firstMemberId ?? string.Empty);
        return $"const:{CurrentConstellationVersion}:{seedBits:X8}:{field.Identity.generatorVersion}:{ordinal}:{memberHash & 0xFFFFFFFFUL:X8}";
    }

    private static ulong PriorityHash(CelestialField field, string value)
    {
        string salted = $"{field.WorldSeed}:{field.Identity.generatorVersion}:{field.Identity.configHash}:{value}";
        return CelestialConstellationNameGenerator.Hash64(salted);
    }
}
