using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CelestialSurveyRegionProgressSnapshot
{
    public string regionKey;
    public int nextSurveySequence;
}

[Serializable]
public sealed class CelestialChartFragmentMark
{
    public string celestialObjectStableId;
    public CelestialObjectKind kind;
    public CelestialColorClass colorClass;
    public Vector2 celestialWorldPosition;
    public Vector2 observedInstrumentPosition01;
    public float brightness01;
    public float prominence01;
    public bool isPatternAnchor;
}

[Serializable]
public sealed class CelestialChartFragmentSnapshot
{
    public int version = 1;
    public string fragmentId;
    public string observationId;
    public string createdByPlayerKey;

    public int worldSeed;
    public int celestialGeneratorVersion;
    public string celestialConfigHash;

    public string surveyRegionKey;
    public int surveySequence;

    public Vector2 observationDatumWorldPosition;
    public Vector2 observationDatumInstrumentPosition01;

    public float capturedHour;
    public int capturedYear;
    public int capturedMonth;
    public int capturedDay;
    public float starVisibility01;
    public float quality01;

    public int visualSeed;

    // Phase 5B additive visual provenance. Older Phase 5A fragments deserialize
    // these to zero, which remains a valid fallback orientation/zoom.
    public int visualGenerationVersion = 1;
    public float recordedInstrumentRotationDegrees;
    public float recordedInstrumentZoom;
    public Vector2 recordedInstrumentCenter01;

    public List<string> patternObjectStableIds = new();
    public List<CelestialChartFragmentMark> marks = new();

    public void EnsureDefaults()
    {
        if (patternObjectStableIds == null)
            patternObjectStableIds = new List<string>();

        if (marks == null)
            marks = new List<CelestialChartFragmentMark>();
    }
}


[Serializable]
public sealed class CelestialChartBoardPlacementSnapshot
{
    public int version = 1;
    public string fragmentId;
    public Vector2 boardCenterWorld;
    public float rotationDegrees;
    public bool pinned;
    public int layerOrder;
    public int revision = 1;
    public string lastEditedByPlayerKey;

    public void EnsureDefaults()
    {
        revision = Mathf.Max(1, revision);
        layerOrder = Mathf.Max(0, layerOrder);

        if (float.IsNaN(rotationDegrees) || float.IsInfinity(rotationDegrees))
            rotationDegrees = 0f;
        else
            rotationDegrees = Mathf.DeltaAngle(0f, rotationDegrees);

        lastEditedByPlayerKey = GameState.NormalizePlayerPersistenceKey(lastEditedByPlayerKey);
    }
}

[Serializable]
public sealed class CelestialChartStateSnapshot
{
    public int version = 2;
    public List<CelestialChartFragmentSnapshot> fragments = new();
    public List<CelestialSurveyRegionProgressSnapshot> surveyRegions = new();
    public List<string> verifiedConstellationIds = new();

    // Phase 6: mutable shared crew belief about how evidence is arranged on the map table.
    // A fragment's immutable observation/evidence remains in fragments; only placement lives here.
    public int boardRevision;
    public List<CelestialChartBoardPlacementSnapshot> boardPlacements = new();

    public void EnsureDefaults()
    {
        version = Mathf.Max(2, version);

        if (fragments == null)
            fragments = new List<CelestialChartFragmentSnapshot>();

        if (surveyRegions == null)
            surveyRegions = new List<CelestialSurveyRegionProgressSnapshot>();

        if (verifiedConstellationIds == null)
            verifiedConstellationIds = new List<string>();

        if (boardPlacements == null)
            boardPlacements = new List<CelestialChartBoardPlacementSnapshot>();

        boardRevision = Mathf.Max(0, boardRevision);

        var seenFragments = new HashSet<string>(StringComparer.Ordinal);
        for (int i = fragments.Count - 1; i >= 0; i--)
        {
            CelestialChartFragmentSnapshot fragment = fragments[i];
            if (fragment == null || string.IsNullOrWhiteSpace(fragment.fragmentId))
            {
                fragments.RemoveAt(i);
                continue;
            }

            fragment.EnsureDefaults();
            if (!seenFragments.Add(fragment.fragmentId))
                fragments.RemoveAt(i);
        }

        var validFragmentIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] != null && !string.IsNullOrWhiteSpace(fragments[i].fragmentId))
                validFragmentIds.Add(fragments[i].fragmentId);
        }

        var seenPlacements = new HashSet<string>(StringComparer.Ordinal);
        for (int i = boardPlacements.Count - 1; i >= 0; i--)
        {
            CelestialChartBoardPlacementSnapshot placement = boardPlacements[i];
            if (placement == null ||
                string.IsNullOrWhiteSpace(placement.fragmentId) ||
                !validFragmentIds.Contains(placement.fragmentId) ||
                !seenPlacements.Add(placement.fragmentId))
            {
                boardPlacements.RemoveAt(i);
                continue;
            }

            placement.EnsureDefaults();
        }

        var seenRegions = new HashSet<string>(StringComparer.Ordinal);
        for (int i = surveyRegions.Count - 1; i >= 0; i--)
        {
            CelestialSurveyRegionProgressSnapshot region = surveyRegions[i];
            if (region == null || string.IsNullOrWhiteSpace(region.regionKey))
            {
                surveyRegions.RemoveAt(i);
                continue;
            }

            region.nextSurveySequence = Mathf.Max(0, region.nextSurveySequence);
            if (!seenRegions.Add(region.regionKey))
                surveyRegions.RemoveAt(i);
        }

        var seenConstellations = new HashSet<string>(StringComparer.Ordinal);
        for (int i = verifiedConstellationIds.Count - 1; i >= 0; i--)
        {
            string id = verifiedConstellationIds[i];
            if (string.IsNullOrWhiteSpace(id) || !seenConstellations.Add(id))
                verifiedConstellationIds.RemoveAt(i);
        }
    }

    public int GetSurveySequence(string regionKey)
    {
        EnsureDefaults();
        if (string.IsNullOrWhiteSpace(regionKey))
            return 0;

        for (int i = 0; i < surveyRegions.Count; i++)
        {
            CelestialSurveyRegionProgressSnapshot region = surveyRegions[i];
            if (region != null && region.regionKey == regionKey)
                return Mathf.Max(0, region.nextSurveySequence);
        }

        return 0;
    }

    public bool TryAdvanceSurveySequence(string regionKey, int completedSequence)
    {
        EnsureDefaults();
        if (string.IsNullOrWhiteSpace(regionKey))
            return false;

        int expected = GetSurveySequence(regionKey);
        if (completedSequence != expected)
            return false;

        for (int i = 0; i < surveyRegions.Count; i++)
        {
            CelestialSurveyRegionProgressSnapshot region = surveyRegions[i];
            if (region != null && region.regionKey == regionKey)
            {
                region.nextSurveySequence = expected + 1;
                return true;
            }
        }

        surveyRegions.Add(new CelestialSurveyRegionProgressSnapshot
        {
            regionKey = regionKey,
            nextSurveySequence = expected + 1
        });
        return true;
    }

    public bool ContainsObservation(string observationId)
    {
        EnsureDefaults();
        if (string.IsNullOrWhiteSpace(observationId))
            return false;

        for (int i = 0; i < fragments.Count; i++)
        {
            CelestialChartFragmentSnapshot fragment = fragments[i];
            if (fragment != null && fragment.observationId == observationId)
                return true;
        }

        return false;
    }

    public bool TryAddFragment(CelestialChartFragmentSnapshot fragment)
    {
        EnsureDefaults();
        if (fragment == null || string.IsNullOrWhiteSpace(fragment.fragmentId) || string.IsNullOrWhiteSpace(fragment.observationId))
            return false;

        if (ContainsObservation(fragment.observationId))
            return false;

        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] != null && fragments[i].fragmentId == fragment.fragmentId)
                return false;
        }

        fragment.EnsureDefaults();
        fragments.Add(fragment);
        return true;
    }

    public bool RemoveFragment(string fragmentId)
    {
        EnsureDefaults();
        if (string.IsNullOrWhiteSpace(fragmentId))
            return false;

        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] != null && fragments[i].fragmentId == fragmentId)
            {
                fragments.RemoveAt(i);
                RemovePlacement(fragmentId);
                return true;
            }
        }

        return false;
    }

    public bool TryGetPlacement(string fragmentId, out CelestialChartBoardPlacementSnapshot placement)
    {
        EnsureDefaults();
        placement = null;

        if (string.IsNullOrWhiteSpace(fragmentId))
            return false;

        for (int i = 0; i < boardPlacements.Count; i++)
        {
            CelestialChartBoardPlacementSnapshot candidate = boardPlacements[i];
            if (candidate != null && candidate.fragmentId == fragmentId)
            {
                placement = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryAddPlacement(CelestialChartBoardPlacementSnapshot placement)
    {
        EnsureDefaults();
        if (placement == null || string.IsNullOrWhiteSpace(placement.fragmentId))
            return false;

        if (!ContainsFragment(placement.fragmentId))
            return false;

        if (TryGetPlacement(placement.fragmentId, out _))
            return false;

        placement.EnsureDefaults();
        boardPlacements.Add(placement);
        return true;
    }

    public bool RemovePlacement(string fragmentId)
    {
        EnsureDefaults();
        if (string.IsNullOrWhiteSpace(fragmentId))
            return false;

        for (int i = 0; i < boardPlacements.Count; i++)
        {
            if (boardPlacements[i] != null && boardPlacements[i].fragmentId == fragmentId)
            {
                boardPlacements.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool ContainsFragment(string fragmentId)
    {
        EnsureDefaults();
        if (string.IsNullOrWhiteSpace(fragmentId))
            return false;

        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] != null && fragments[i].fragmentId == fragmentId)
                return true;
        }

        return false;
    }

    public int GetNextLayerOrder()
    {
        EnsureDefaults();
        int max = -1;
        for (int i = 0; i < boardPlacements.Count; i++)
        {
            CelestialChartBoardPlacementSnapshot placement = boardPlacements[i];
            if (placement != null)
                max = Mathf.Max(max, placement.layerOrder);
        }

        return max + 1;
    }
}

public static class CelestialChartFragmentBuilder
{
    public static CelestialChartFragmentSnapshot Build(
        CelestialObservation observation,
        string createdByPlayerKey,
        string surveyRegionKey)
    {
        if (observation == null)
            return null;

        string fragmentId = Guid.NewGuid().ToString("N");
        var fragment = new CelestialChartFragmentSnapshot
        {
            fragmentId = fragmentId,
            observationId = observation.observationId,
            createdByPlayerKey = GameState.NormalizePlayerPersistenceKey(createdByPlayerKey),
            worldSeed = observation.worldSeed,
            celestialGeneratorVersion = observation.celestialGeneratorVersion,
            celestialConfigHash = observation.celestialConfigHash,
            surveyRegionKey = surveyRegionKey,
            surveySequence = Mathf.Max(0, observation.surveySequence),
            observationDatumWorldPosition = observation.observerTrueWorldPosition,
            observationDatumInstrumentPosition01 = ComputeObservationDatumInstrumentPosition(observation),
            capturedHour = observation.capturedHour,
            capturedYear = observation.capturedYear,
            capturedMonth = observation.capturedMonth,
            capturedDay = observation.capturedDay,
            starVisibility01 = Mathf.Clamp01(observation.starVisibility01),
            quality01 = Mathf.Clamp01(observation.quality01),
            visualSeed = StableSeed(fragmentId + ":" + observation.observationId),
            visualGenerationVersion = 1,
            recordedInstrumentRotationDegrees = observation.instrumentRotationDegrees,
            recordedInstrumentZoom = observation.instrumentZoom,
            recordedInstrumentCenter01 = observation.instrumentCenter01
        };

        if (observation.patternObjectStableIds != null)
        {
            for (int i = 0; i < observation.patternObjectStableIds.Count; i++)
            {
                string id = observation.patternObjectStableIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                    fragment.patternObjectStableIds.Add(id);
            }
        }

        if (observation.objects != null)
        {
            for (int i = 0; i < observation.objects.Count; i++)
            {
                CelestialObservationObject obj = observation.objects[i];
                if (obj == null || string.IsNullOrWhiteSpace(obj.stableId))
                    continue;

                fragment.marks.Add(new CelestialChartFragmentMark
                {
                    celestialObjectStableId = obj.stableId,
                    kind = obj.kind,
                    colorClass = obj.colorClass,
                    celestialWorldPosition = obj.worldPosition,
                    observedInstrumentPosition01 = obj.instrumentPosition01,
                    brightness01 = Mathf.Clamp01(obj.brightness01),
                    prominence01 = Mathf.Clamp01(obj.prominence01),
                    isPatternAnchor = obj.isAnchor
                });
            }
        }

        fragment.EnsureDefaults();
        return fragment;
    }

    private static Vector2 ComputeObservationDatumInstrumentPosition(CelestialObservation observation)
    {
        Vector2 local = (new Vector2(0.5f, 0.5f) - observation.instrumentCenter01) * Mathf.Max(0.0001f, observation.instrumentZoom);
        float radians = observation.instrumentRotationDegrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(radians);
        float s = Mathf.Sin(radians);
        Vector2 rotated = new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
        return new Vector2(0.5f, 0.5f) + rotated;
    }

    private static int StableSeed(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;
            if (text != null)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }
            }
            return (int)hash;
        }
    }
}
