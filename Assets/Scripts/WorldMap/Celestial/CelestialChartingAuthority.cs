using UnityEngine;

public readonly struct CelestialChartCommitResult
{
    public readonly bool success;
    public readonly string message;
    public readonly CelestialChartFragmentSnapshot fragment;

    public CelestialChartCommitResult(bool success, string message, CelestialChartFragmentSnapshot fragment = null)
    {
        this.success = success;
        this.message = message;
        this.fragment = fragment;
    }
}

/// <summary>
/// Authoritative transaction boundary for converting a completed celestial observation
/// into persistent chart evidence. The caller supplies the exact requester GameObject;
/// no client-authored player id is trusted here.
/// </summary>
public static class CelestialChartingAuthority
{
    public static CelestialChartCommitResult TryCommitObservation(
        GameObject requester,
        CelestialField field,
        CelestialObservationSettings observationSettings,
        ItemDefinition chartingPaperDefinition,
        CelestialObservation observation)
    {
        if (!GameplayAuthority.IsAuthoritative)
            return Fail("Chart recording requires gameplay authority.");

        if (requester == null)
            return Fail("No charting requester is available.");

        if (field == null || !field.IsValid || field.Identity == null)
            return Fail("Celestial field truth is unavailable.");

        if (observationSettings == null)
            return Fail("Celestial observation settings are unavailable.");

        if (observation == null || string.IsNullOrWhiteSpace(observation.observationId))
            return Fail("The completed observation is invalid.");

        if (observation.source != CelestialObservationSource.PlayerCharted)
            return Fail("Only player-charted observations can consume charting paper here.");

        if (observation.worldSeed != field.WorldSeed ||
            observation.celestialGeneratorVersion != field.Identity.generatorVersion ||
            observation.celestialConfigHash != field.Identity.configHash)
        {
            return Fail("The observation does not match the current celestial field.");
        }

        if (!ValidatePatternAgainstField(field, observation, out string validationError))
            return Fail(validationError);

        GameState gameState = GameState.I;
        if (gameState == null)
            return Fail("GameState is unavailable.");

        gameState.EnsureCelestialChartDefaults();
        CelestialChartStateSnapshot chartState = gameState.celestialCharts;

        if (chartState.ContainsObservation(observation.observationId))
            return Fail("This observation has already been recorded.");

        string regionKey = CelestialSurveySequenceTracker.BuildRegionKey(
            field,
            observation.observerTrueWorldPosition,
            observationSettings.surveyRegionSizeWorld);

        int expectedSequence = chartState.GetSurveySequence(regionKey);
        if (observation.surveySequence != expectedSequence)
        {
            return Fail(
                $"This survey is stale. Expected sequence {expectedSequence}, received {observation.surveySequence}.");
        }

        if (!CelestialChartPaperConsumption.TryConsumeOne(
                requester,
                chartingPaperDefinition,
                out CelestialChartPaperConsumption.Receipt receipt,
                out string paperError))
        {
            return Fail(paperError);
        }

        string playerKey = ResolvePlayerPersistenceKey(requester, gameState);
        CelestialChartFragmentSnapshot fragment = CelestialChartFragmentBuilder.Build(
            observation,
            playerKey,
            regionKey);

        if (fragment == null)
        {
            receipt.Rollback();
            return Fail("Failed to build the chart fragment.");
        }

        if (!chartState.TryAddFragment(fragment))
        {
            receipt.Rollback();
            return Fail("The chart fragment could not be added to persistent chart state.");
        }

        if (!chartState.TryAdvanceSurveySequence(regionKey, observation.surveySequence))
        {
            chartState.RemoveFragment(fragment.fragmentId);
            receipt.Rollback();
            return Fail("The survey sequence changed before the chart could be committed.");
        }

        receipt.Commit();
        CelestialKnowledgeAuthority.FreezeGeneration(chartState, field);

        return new CelestialChartCommitResult(
            true,
            $"Recorded chart fragment {fragment.fragmentId}.",
            fragment);
    }

    private static bool ValidatePatternAgainstField(
        CelestialField field,
        CelestialObservation observation,
        out string error)
    {
        error = null;

        if (observation.patternObjectStableIds == null || observation.patternObjectStableIds.Count < 3)
        {
            error = "The observation does not contain a valid landmark pattern.";
            return false;
        }

        if (observation.objects == null || observation.objects.Count == 0)
        {
            error = "The observation contains no celestial evidence.";
            return false;
        }

        for (int i = 0; i < observation.patternObjectStableIds.Count; i++)
        {
            string stableId = observation.patternObjectStableIds[i];
            if (!field.TryResolveObject(stableId, out CelestialObject truth) || truth == null)
            {
                error = $"Pattern star '{stableId}' does not exist in celestial truth.";
                return false;
            }

            if (truth.Kind != CelestialObjectKind.LandmarkStar)
            {
                error = $"Pattern object '{stableId}' is not a landmark star.";
                return false;
            }

            bool foundAnchor = false;
            for (int j = 0; j < observation.objects.Count; j++)
            {
                CelestialObservationObject observed = observation.objects[j];
                if (observed == null || observed.stableId != stableId)
                    continue;

                if (!observed.isAnchor)
                    continue;

                if (Vector2.SqrMagnitude(observed.worldPosition - truth.WorldPosition) > 0.0001f)
                {
                    error = $"Pattern star '{stableId}' has a mismatched recorded truth position.";
                    return false;
                }

                foundAnchor = true;
                break;
            }

            if (!foundAnchor)
            {
                error = $"Pattern star '{stableId}' is missing from the recorded anchor evidence.";
                return false;
            }
        }

        return true;
    }

    private static string ResolvePlayerPersistenceKey(GameObject requester, GameState gameState)
    {
        PlayerLoadoutPersistence persistence =
            requester.GetComponent<PlayerLoadoutPersistence>() ??
            requester.GetComponentInParent<PlayerLoadoutPersistence>(true) ??
            requester.GetComponentInChildren<PlayerLoadoutPersistence>(true);

        return persistence != null
            ? persistence.PersistenceKey
            : gameState.LocalPlayerPersistenceKey;
    }

    private static CelestialChartCommitResult Fail(string message)
    {
        return new CelestialChartCommitResult(false, message);
    }
}
