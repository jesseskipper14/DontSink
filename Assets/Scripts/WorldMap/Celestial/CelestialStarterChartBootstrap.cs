using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prototype/tutorial bootstrap for giving a fresh crew one known-good celestial patch at the
/// starting dock. The finished game can replace this seam with the tutorial cartographer/NPC.
///
/// The starter patch is ordinary persistent chart evidence. It is pre-placed and pinned at its
/// correct coordinate so the player begins with one trustworthy registration anchor between the
/// Star Chart and World Map.
/// </summary>
public static class CelestialStarterChartBootstrap
{
    private const int DesiredLandmarkCount = 4;
    private const int MinimumLandmarkCount = 3;
    private const float ContextMarginWorld = 5f;

    private static readonly float[] SearchRadiiWorld = { 14f, 20f, 28f, 38f, 50f };

    public static bool EnsureStarterPatch(
        GameObject requester,
        WorldMapGraphGenerator generator,
        CelestialFieldSource fieldSource,
        out string message)
    {
        message = null;

        if (!GameplayAuthority.IsAuthoritative)
        {
            message = "Starter chart bootstrap waits for gameplay authority.";
            return false;
        }

        if (generator == null || generator.graph == null || generator.graph.nodes == null)
        {
            message = "Starter chart bootstrap has no world-map graph.";
            return false;
        }

        if (fieldSource == null || !fieldSource.EnsureField() || fieldSource.Field == null)
        {
            message = "Starter chart bootstrap has no celestial field.";
            return false;
        }

        if (!TryFindStartNode(generator.graph, out MapNode startNode) || startNode == null)
        {
            message = "Starter chart bootstrap could not find the StartDock node.";
            return false;
        }

        GameState gameState = GameState.I;
        if (gameState == null)
        {
            message = "Starter chart bootstrap has no GameState.";
            return false;
        }

        gameState.EnsureCelestialChartDefaults();
        CelestialChartStateSnapshot state = gameState.celestialCharts;
        CelestialField field = fieldSource.Field;

        string fragmentId = BuildStarterFragmentId(field);

        CelestialChartFragmentSnapshot fragment = FindFragment(state, fragmentId);
        if (fragment == null)
        {
            fragment = BuildStarterFragment(
                field,
                startNode.position,
                ResolvePlayerPersistenceKey(requester, gameState),
                fragmentId);

            if (fragment == null)
            {
                message = "Starter chart bootstrap could not find enough nearby landmark stars.";
                return false;
            }

            if (!state.TryAddFragment(fragment))
            {
                message = "Starter chart bootstrap could not add the starter fragment.";
                return false;
            }

            CelestialKnowledgeAuthority.FreezeGeneration(state, field);
        }

        if (!state.TryGetPlacement(fragment.fragmentId, out CelestialChartBoardPlacementSnapshot placement) || placement == null)
        {
            Vector2 canonicalCenter = ComputeCanonicalFragmentCenter(fragment);
            placement = new CelestialChartBoardPlacementSnapshot
            {
                fragmentId = fragment.fragmentId,
                boardCenterWorld = canonicalCenter,
                rotationDegrees = 0f,
                pinned = true,
                layerOrder = state.GetNextLayerOrder(),
                revision = 1,
                lastEditedByPlayerKey = ResolvePlayerPersistenceKey(requester, gameState)
            };

            if (!state.TryAddPlacement(placement))
            {
                message = "Starter chart bootstrap created evidence but could not place it on the board.";
                return false;
            }

            state.boardRevision++;
        }

        gameState.EnsureCelestialChartDefaults();
        message = "Starter celestial patch is pinned at the starting dock.";
        return true;
    }

    public static bool RemoveStarterPatchForDebug(
        WorldMapGraphGenerator generator,
        CelestialFieldSource fieldSource)
    {
        if (!GameplayAuthority.IsAuthoritative ||
            GameState.I == null ||
            generator == null ||
            fieldSource == null ||
            !fieldSource.EnsureField() ||
            fieldSource.Field == null)
        {
            return false;
        }

        GameState.I.EnsureCelestialChartDefaults();
        CelestialChartStateSnapshot state = GameState.I.celestialCharts;
        string fragmentId = BuildStarterFragmentId(fieldSource.Field);
        bool removed = state.RemoveFragment(fragmentId);
        if (removed)
            state.boardRevision++;
        return removed;
    }

    private static CelestialChartFragmentSnapshot BuildStarterFragment(
        CelestialField field,
        Vector2 startPosition,
        string createdByPlayerKey,
        string fragmentId)
    {
        var queried = new List<CelestialObject>();
        var landmarks = new List<CelestialObject>();

        for (int r = 0; r < SearchRadiiWorld.Length; r++)
        {
            float radius = SearchRadiiWorld[r];
            Rect area = new Rect(
                startPosition.x - radius,
                startPosition.y - radius,
                radius * 2f,
                radius * 2f);

            field.Query(area, queried);
            landmarks.Clear();

            for (int i = 0; i < queried.Count; i++)
            {
                CelestialObject obj = queried[i];
                if (obj != null && obj.Kind == CelestialObjectKind.LandmarkStar)
                    landmarks.Add(obj);
            }

            if (landmarks.Count >= DesiredLandmarkCount ||
                (r == SearchRadiiWorld.Length - 1 && landmarks.Count >= MinimumLandmarkCount))
            {
                break;
            }
        }

        if (landmarks.Count < MinimumLandmarkCount)
            return null;

        landmarks.Sort((a, b) =>
        {
            float da = new WorldTopology(field.WorldBounds).Delta(startPosition, a.WorldPosition).sqrMagnitude;
            float db = new WorldTopology(field.WorldBounds).Delta(startPosition, b.WorldPosition).sqrMagnitude;
            int distanceOrder = da.CompareTo(db);
            if (distanceOrder != 0)
                return distanceOrder;
            return string.CompareOrdinal(a.StableId, b.StableId);
        });

        int anchorCount = Mathf.Min(DesiredLandmarkCount, landmarks.Count);
        var anchorIds = new HashSet<string>(StringComparer.Ordinal);

        float minX = startPosition.x;
        float maxX = startPosition.x;
        float minY = startPosition.y;
        float maxY = startPosition.y;

        for (int i = 0; i < anchorCount; i++)
        {
            CelestialObject landmark = landmarks[i];
            anchorIds.Add(landmark.StableId);
            Encapsulate(ref minX, ref maxX, ref minY, ref maxY, new WorldTopology(field.WorldBounds).Nearest(landmark.WorldPosition, startPosition));
        }

        Rect patchBounds = Rect.MinMaxRect(
            minX - ContextMarginWorld,
            minY - ContextMarginWorld,
            maxX + ContextMarginWorld,
            maxY + ContextMarginWorld);

        field.Query(patchBounds, queried);

        var fragment = new CelestialChartFragmentSnapshot
        {
            isStarterPatch = true,
            fragmentId = fragmentId,
            observationId = fragmentId + ":observation",
            createdByPlayerKey = GameState.NormalizePlayerPersistenceKey(createdByPlayerKey),
            worldSeed = field.WorldSeed,
            celestialGeneratorVersion = field.Identity.generatorVersion,
            celestialConfigHash = field.Identity.configHash,
            surveyRegionKey = "starter:" + field.WorldSeed,
            surveySequence = 0,
            observationDatumWorldPosition = startPosition,
            observationDatumInstrumentPosition01 = new Vector2(0.5f, 0.5f),
            capturedHour = 0f,
            capturedYear = 0,
            capturedMonth = 0,
            capturedDay = 0,
            starVisibility01 = 1f,
            quality01 = 1f,
            visualSeed = StableSeed(fragmentId),
            visualGenerationVersion = 1,
            recordedInstrumentRotationDegrees = 0f,
            recordedInstrumentZoom = 1f,
            recordedInstrumentCenter01 = new Vector2(0.5f, 0.5f)
        };

        for (int i = 0; i < anchorCount; i++)
            fragment.patternObjectStableIds.Add(landmarks[i].StableId);

        for (int i = 0; i < queried.Count; i++)
        {
            CelestialObject obj = queried[i];
            if (obj == null)
                continue;
            Vector2 nearby = new WorldTopology(field.WorldBounds).Nearest(obj.WorldPosition, startPosition);
            if (!patchBounds.Contains(nearby)) continue;

            bool isAnchor = anchorIds.Contains(obj.StableId);
            bool includeContext =
                obj.Kind == CelestialObjectKind.AmbientStar ||
                obj.Kind == CelestialObjectKind.Nebula ||
                obj.Kind == CelestialObjectKind.DeepSkyObject;

            if (!isAnchor && !includeContext)
                continue;

            fragment.marks.Add(new CelestialChartFragmentMark
            {
                celestialObjectStableId = obj.StableId,
                kind = obj.Kind,
                colorClass = obj.ColorClass,
                celestialWorldPosition = obj.WorldPosition,
                observedInstrumentPosition01 = new Vector2(
                    Mathf.InverseLerp(patchBounds.xMin, patchBounds.xMax, nearby.x),
                    Mathf.InverseLerp(patchBounds.yMin, patchBounds.yMax, nearby.y)),
                brightness01 = obj.Brightness01,
                prominence01 = obj.Prominence01,
                isPatternAnchor = isAnchor
            });
        }

        fragment.EnsureDefaults();
        return fragment;
    }

    private static bool TryFindStartNode(MapGraph graph, out MapNode startNode)
    {
        startNode = null;
        if (graph == null || graph.nodes == null)
            return false;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            MapNode node = graph.nodes[i];
            if (node != null && node.kind == NodeKind.StartDock)
            {
                startNode = node;
                return true;
            }
        }

        return false;
    }

    private static CelestialChartFragmentSnapshot FindFragment(
        CelestialChartStateSnapshot state,
        string fragmentId)
    {
        if (state == null || state.fragments == null)
            return null;

        for (int i = 0; i < state.fragments.Count; i++)
        {
            CelestialChartFragmentSnapshot fragment = state.fragments[i];
            if (fragment != null && fragment.fragmentId == fragmentId)
                return fragment;
        }

        return null;
    }

    private static Vector2 ComputeCanonicalFragmentCenter(CelestialChartFragmentSnapshot fragment)
    {
        bool hasPoint = false;
        float minX = 0f;
        float maxX = 0f;
        float minY = 0f;
        float maxY = 0f;

        if (fragment != null && fragment.marks != null)
        {
            for (int i = 0; i < fragment.marks.Count; i++)
            {
                CelestialChartFragmentMark mark = fragment.marks[i];
                if (mark == null)
                    continue;

                Vector2 nearby = WorldTopologyService.Nearest(mark.celestialWorldPosition, fragment.observationDatumWorldPosition);

                if (!hasPoint)
                {
                    hasPoint = true;
                    minX = maxX = nearby.x;
                    minY = maxY = nearby.y;
                }
                else
                {
                    Encapsulate(
                        ref minX,
                        ref maxX,
                        ref minY,
                        ref maxY,
                        nearby);
                }
            }
        }

        Vector2 datum = fragment != null
            ? fragment.observationDatumWorldPosition
            : Vector2.zero;

        if (!hasPoint)
            return datum;

        Encapsulate(ref minX, ref maxX, ref minY, ref maxY, datum);
        return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }

    private static void Encapsulate(
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY,
        Vector2 point)
    {
        minX = Mathf.Min(minX, point.x);
        maxX = Mathf.Max(maxX, point.x);
        minY = Mathf.Min(minY, point.y);
        maxY = Mathf.Max(maxY, point.y);
    }

    private static string ResolvePlayerPersistenceKey(GameObject requester, GameState gameState)
    {
        if (requester != null)
        {
            PlayerLoadoutPersistence persistence =
                requester.GetComponent<PlayerLoadoutPersistence>() ??
                requester.GetComponentInParent<PlayerLoadoutPersistence>(true) ??
                requester.GetComponentInChildren<PlayerLoadoutPersistence>(true);

            if (persistence != null)
                return persistence.PersistenceKey;
        }

        return gameState != null
            ? gameState.LocalPlayerPersistenceKey
            : null;
    }

    private static string BuildStarterFragmentId(CelestialField field)
    {
        string hash = field != null && field.Identity != null
            ? field.Identity.configHash
            : "nohash";

        return $"starter-chart:{field?.WorldSeed ?? 0}:{field?.Identity?.generatorVersion ?? 0}:{hash}";
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
