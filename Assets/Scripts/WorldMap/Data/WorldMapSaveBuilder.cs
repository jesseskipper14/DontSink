using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldMapSaveBuilder
{
    public static bool CaptureCurrentWorldMapIntoGameState(string reason = "")
    {
        GameState gs = GameState.I;
        if (gs == null)
        {
            Debug.LogWarning("[WorldMapSaveBuilder] Cannot capture world map: GameState.I is null.");
            return false;
        }

        WorldMapTopographyDebugSource topography =
            UnityEngine.Object.FindAnyObjectByType<WorldMapTopographyDebugSource>(FindObjectsInactive.Include);

        WorldMapGraphGenerator generator =
            UnityEngine.Object.FindAnyObjectByType<WorldMapGraphGenerator>(FindObjectsInactive.Include);

        WorldMapRuntimeBinder runtimeBinder =
            UnityEngine.Object.FindAnyObjectByType<WorldMapRuntimeBinder>(FindObjectsInactive.Include);

        WorldMapEventManager eventManager =
            UnityEngine.Object.FindAnyObjectByType<WorldMapEventManager>(FindObjectsInactive.Include);

        WorldMapPOISource poiSource =
            UnityEngine.Object.FindAnyObjectByType<WorldMapPOISource>(FindObjectsInactive.Include);

        WorldMapPlayerRef playerRef =
            UnityEngine.Object.FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);

        WorldMapKnowledgeSource knowledgeSource =
            UnityEngine.Object.FindAnyObjectByType<WorldMapKnowledgeSource>(FindObjectsInactive.Include);

        WorldMapSaveSnapshot snapshot = Capture(
            topography,
            generator,
            runtimeBinder,
            eventManager,
            poiSource,
            playerRef,
            gs,
            knowledgeSource
        );

        gs.SetWorldMapSnapshot(snapshot, string.IsNullOrWhiteSpace(reason)
            ? "WorldMapSaveBuilder.CaptureCurrentWorldMapIntoGameState"
            : reason);

        WorldMapSaveDebugUtility.LogSummary(snapshot, "WorldMap Save Capture", gs);
        WorldMapSaveDebugUtility.LogValidation(snapshot, "WorldMap Save Capture Validation", gs);

        return true;
    }

    public static WorldMapSaveSnapshot Capture(
        WorldMapTopographyDebugSource topography,
        WorldMapGraphGenerator generator,
        WorldMapRuntimeBinder runtimeBinder,
        WorldMapEventManager eventManager,
        WorldMapPOISource poiSource,
        WorldMapPlayerRef playerRef,
        GameState gs,
        WorldMapKnowledgeSource knowledgeSource = null)
    {
        var snapshot = new WorldMapSaveSnapshot
        {
            version = 3,
            worldSeed = generator != null ? generator.seed : 0,
            lastSavedWithGameVersion = Application.version
        };

        snapshot.EnsureDefaults();

        snapshot.topography = CaptureTopography(topography);
        snapshot.graph = CaptureGraph(generator);
        snapshot.nodeRuntime = CaptureRuntimeNodes(runtimeBinder);
        snapshot.pois = CapturePOIs(poiSource);
        snapshot.effects = CaptureEffects(eventManager, runtimeBinder);
        snapshot.player = CapturePlayer(playerRef, gs);
        snapshot.activeTravel = CaptureActiveTravel(gs);
        snapshot.simulation = CaptureSimulation();
        snapshot.knowledge = CaptureKnowledge(knowledgeSource);
        snapshot.diagnostics = BuildDiagnostics(snapshot);

        return snapshot;
    }

    private static WorldMapTopographySaveSnapshot CaptureTopography(WorldMapTopographyDebugSource source)
    {
        var snap = new WorldMapTopographySaveSnapshot
        {
            version = 2,
            mode = WorldMapTopographySaveMode.None,
            height01 = new List<float>()
        };

        if (source == null || source.Field == null || !source.Field.IsValid)
            return snap;

        WorldMapTopographyField field = source.Field;
        float[] heights = field.CopyHeight01();

        snap.mode = WorldMapTopographySaveMode.RawHeightData;
        snap.seed = field.Seed;
        snap.width = field.Width;
        snap.height = field.Height;
        snap.StoreWorldBounds(field.WorldBounds);
        snap.minRaw = field.MinRaw;
        snap.maxRaw = field.MaxRaw;
        snap.effectiveSeaLevel01 = source.EffectiveSeaLevel01;
        snap.stats = CaptureStats(source.Stats);
        snap.StorePackedHeights(heights);

        WorldMapTopographyBakeAsset baked = source.BakedAsset;
        if (baked != null)
        {
            snap.settingsHash = baked.settingsFingerprint;
            snap.settingsId = baked.settingsSource != null ? baked.settingsSource.name : null;
        }
        else if (source.Settings != null)
        {
            snap.settingsId = source.Settings.name;
        }

        snap.generatorVersion = "topography_v2_ushort_base64";

        return snap;
    }

    private static WorldMapTopographyStatsSaveSnapshot CaptureStats(WorldMapTopographyStats stats)
    {
        return new WorldMapTopographyStatsSaveSnapshot
        {
            water01 = stats.Water01,
            land01 = stats.Land01,
            deepOcean01 = stats.DeepOcean01,
            openOcean01 = stats.OpenOcean01,
            shelfWater01 = stats.ShelfWater01,
            shallowWater01 = stats.ShallowWater01,
            beach01 = stats.Beach01,
            lowland01 = stats.Lowland01,
            highland01 = stats.Highland01,
            mountain01 = stats.Mountain01
        };
    }

    private static WorldMapGraphSaveSnapshot CaptureGraph(WorldMapGraphGenerator generator)
    {
        var snap = new WorldMapGraphSaveSnapshot();
        snap.EnsureDefaults();

        if (generator == null || generator.graph == null)
            return snap;

        MapGraph graph = generator.graph;
        snap.seed = graph.seed;

        if (graph.nodes != null)
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                MapNode n = graph.nodes[i];
                if (n == null)
                    continue;

                string stableId = WorldMapStableIdUtility.BuildNodeStableId(graph.seed, n);

                var ns = new WorldMapGraphNodeSaveSnapshot
                {
                    stableId = stableId,
                    localStableId = n.localStableId,
                    nodeIndex = i,
                    clusterId = n.clusterId,
                    displayName = n.displayName,
                    kind = n.kind.ToString(),
                    isPrimary = n.isPrimary,
                    biomeId = n.biome.ToString(),
                    primaryResourceId = n.primaryResource.ToString(),
                    secondaryResourceId = n.secondaryResource.ToString(),
                    primaryFactionId = n.primaryFaction.ToString(),
                    secondaryFactionId = n.secondaryFaction.ToString(),
                    dockRating = n.dock.rating,
                    tradeRating = n.tradeHub.rating,
                    population = n.population,
                    minPopulation = n.minPopulation,
                    maxPopulation = n.maxPopulation,
                    notes = n.notes
                };

                ns.StorePosition(n.position);

                if (n.stats != null)
                {
                    for (int s = 0; s < n.stats.Count; s++)
                    {
                        NodeStat stat = n.stats[s];
                        ns.initialStats.Add(CaptureStat(stat.id, stat.stat));
                    }
                }

                if (n.optionalBuildings != null)
                {
                    for (int b = 0; b < n.optionalBuildings.Count; b++)
                    {
                        OptionalBuilding ob = n.optionalBuildings[b];
                        ns.optionalBuildings.Add(new WorldMapNodeOptionalBuildingSaveSnapshot
                        {
                            buildingId = ob.id.ToString(),
                            present = ob.present,
                            rating = ob.rating.rating
                        });
                    }
                }

                if (n.flags != null)
                    ns.flags.AddRange(n.flags);

                snap.nodes.Add(ns);
            }
        }

        if (graph.edges != null)
        {
            for (int i = 0; i < graph.edges.Count; i++)
            {
                MapEdge e = graph.edges[i];

                string aStable = TryGetNodeStableId(graph, e.a);
                string bStable = TryGetNodeStableId(graph, e.b);

                snap.edges.Add(new WorldMapGraphEdgeSaveSnapshot
                {
                    stableId = BuildEdgeStableId(aStable, bStable),
                    aIndex = e.a,
                    bIndex = e.b,
                    aStableId = aStable,
                    bStableId = bStable,
                    routeType = e.kind.ToString(),
                    routeLength = TryGetRouteLength(graph, e.a, e.b)
                });
            }
        }

        return snap;
    }

    private static WorldMapNodeRuntimeStateSetSnapshot CaptureRuntimeNodes(WorldMapRuntimeBinder binder)
    {
        var snap = new WorldMapNodeRuntimeStateSetSnapshot();
        snap.EnsureDefaults();

        if (binder == null || !binder.IsBuilt || binder.Registry == null)
            return snap;

        foreach (MapNodeRuntime rt in binder.Registry.AllRuntimes)
        {
            if (rt == null || rt.State == null)
                continue;

            MapNodeState state = rt.State;

            var ns = new WorldMapNodeRuntimeStateSaveSnapshot
            {
                stableId = rt.StableId,
                nodeIndex = rt.NodeIndex,
                population = state.population,
                minPopulation = state.minPopulation,
                maxPopulation = state.maxPopulation,
                clusterAffinityId = rt.ClusterAffinityId,
                nodeArchetypeId = rt.NodeArchetypeId
            };

            if (state.Stats != null)
            {
                foreach (KeyValuePair<NodeStatId, SimStat> kv in state.Stats)
                    ns.stats.Add(CaptureStat(kv.Key, kv.Value));
            }

            if (state.FactionInfluence != null)
            {
                foreach (KeyValuePair<FactionId, float> kv in state.FactionInfluence)
                {
                    ns.factionInfluence.Add(new WorldMapNodeFactionInfluenceSaveSnapshot
                    {
                        factionId = kv.Key.ToString(),
                        value01 = kv.Value
                    });
                }
            }

            if (state.Flags != null)
                ns.flags.AddRange(state.Flags);

            if (state.ResourcePressures != null)
            {
                foreach (KeyValuePair<string, ResourcePressureState> kv in state.ResourcePressures)
                {
                    ns.resourcePressures.Add(new WorldMapNodeResourcePressureSaveSnapshot
                    {
                        itemId = kv.Key,
                        baseline = kv.Value.baseline,
                        value = kv.Value.value,
                        driftRate = kv.Value.driftRate
                    });
                }
            }

            snap.nodes.Add(ns);
        }

        return snap;
    }

    private static WorldMapNodeStatSaveSnapshot CaptureStat(NodeStatId id, SimStat stat)
    {
        return new WorldMapNodeStatSaveSnapshot
        {
            statId = id.ToString(),
            value = stat.value,
            velocity = stat.velocity,
            equilibrium = stat.equilibrium,
            restoreStrength = stat.restoreStrength,
            minValue = stat.minValue,
            maxValue = stat.maxValue
        };
    }

    private static WorldMapPOISetSaveSnapshot CapturePOIs(WorldMapPOISource source)
    {
        var snap = new WorldMapPOISetSaveSnapshot();
        snap.EnsureDefaults();

        if (source == null || !source.HasLayer || source.Layer == null || source.Layer.pois == null)
            return snap;

        for (int i = 0; i < source.Layer.pois.Count; i++)
        {
            WorldMapPOIInstance poi = source.Layer.pois[i];
            if (poi == null)
                continue;

            var ps = new WorldMapPOISaveSnapshot
            {
                stableId = poi.stableId,
                poiDefId = poi.poiDefId,
                displayName = poi.displayName,
                height01 = poi.height01,
                depth01 = poi.depth01,
                score = poi.score,
                discovered = poi.discovered,
                surveyed = poi.surveyed,
                // In current POI v1, explored is not in the runtime instance yet.
                explored = false,
                depleted = poi.depleted
            };

            ps.StorePosition(poi.position);
            snap.pois.Add(ps);
        }

        return snap;
    }

    private static WorldMapEffectStateSaveSnapshot CaptureEffects(
        WorldMapEventManager eventManager,
        WorldMapRuntimeBinder binder)
    {
        var snap = new WorldMapEffectStateSaveSnapshot();
        snap.EnsureDefaults();

        if (eventManager != null && eventManager.active != null)
        {
            for (int i = 0; i < eventManager.active.Count; i++)
            {
                WorldMapEventInstance ev = eventManager.active[i];
                string sourceStableId = TryGetNodeStableId(binder, ev.sourceNodeId);
                string eventId = ev.def != null ? ev.def.eventId : null;

                snap.events.Add(new WorldMapEventSaveSnapshot
                {
                    instanceId = $"event_{eventId}_{sourceStableId}_{ev.seed}",
                    eventId = eventId,
                    sourceNodeStableId = sourceStableId,
                    targetNodeStableId = null,
                    elapsedHours = ev.elapsedHours,
                    durationHours = ev.durationHours,
                    remainingHours = ev.RemainingHours,
                    seed = ev.seed,
                    isResolved = ev.isResolved,
                    isVisibleToPlayer = ev.def == null || ev.def.isVisibleToPlayer,
                    discovered = ev.def == null || ev.def.isVisibleToPlayer,
                    selectedOutcomeId = null,
                    stateJson = ev.stateJson
                });
            }
        }

        if (binder != null && binder.IsBuilt && binder.Registry != null)
        {
            foreach (MapNodeRuntime rt in binder.Registry.AllRuntimes)
            {
                if (rt == null || rt.State == null || rt.State.ActiveBuffs == null)
                    continue;

                for (int i = 0; i < rt.State.ActiveBuffs.Count; i++)
                {
                    TimedBuffInstance buff = rt.State.ActiveBuffs[i];
                    string buffId = buff.buff != null ? buff.buff.buffId : null;

                    snap.buffs.Add(new WorldMapBuffSaveSnapshot
                    {
                        instanceId = $"buff_{buffId}_{rt.StableId}_{i}",
                        buffId = buffId,
                        nodeStableId = rt.StableId,
                        elapsedHours = buff.elapsedHours,
                        durationHours = buff.durationHours,
                        remainingHours = buff.RemainingHours,
                        stacks = buff.stacks
                    });
                }
            }
        }

        return snap;
    }

    private static WorldMapPlayerSaveSnapshot CapturePlayer(WorldMapPlayerRef playerRef, GameState gs)
    {
        WorldMapPlayerState state = playerRef != null ? playerRef.State : gs != null ? gs.player : null;

        if (state == null)
            return new WorldMapPlayerSaveSnapshot();

        return new WorldMapPlayerSaveSnapshot
        {
            currentNodeStableId = state.currentNodeId,
            lockedSourceNodeStableId = state.lockedSourceNodeId,
            lockedDestinationNodeStableId = state.lockedDestinationNodeId,
            lastVisitedNodeStableId = null
        };
    }

    private static WorldMapActiveTravelSaveSnapshot CaptureActiveTravel(GameState gs)
    {
        TravelPayload travel = gs != null ? gs.activeTravel : null;

        var snap = new WorldMapActiveTravelSaveSnapshot
        {
            isTraveling = travel != null
        };

        if (travel == null)
            return snap;

        snap.fromNodeStableId = travel.fromNodeStableId;
        snap.toNodeStableId = travel.toNodeStableId;
        snap.seed = travel.seed;
        snap.routeLength = travel.routeLength;
        snap.boatInstanceId = travel.boatInstanceId;
        snap.boatPrefabGuid = travel.boatPrefabGuid;

        return snap;
    }

    private static WorldMapSimulationSaveSnapshot CaptureSimulation()
    {
        return new WorldMapSimulationSaveSnapshot();
    }

    private static WorldMapKnowledgeSaveSnapshot CaptureKnowledge(WorldMapKnowledgeSource knowledgeSource)
    {
        if (knowledgeSource != null)
            return knowledgeSource.CaptureSnapshot();

        return new WorldMapKnowledgeSaveSnapshot();
    }

    private static WorldMapSaveDiagnosticsSnapshot BuildDiagnostics(WorldMapSaveSnapshot snapshot)
    {
        snapshot.EnsureDefaults();

        int topoSamples =
            snapshot.topography != null
                ? Mathf.Max(0, snapshot.topography.width * snapshot.topography.height)
                : 0;

        return new WorldMapSaveDiagnosticsSnapshot
        {
            mapGenerationVersion = "world_map_save_v2_capture_restore_v1",
            topographySettingsHash = snapshot.topography != null ? snapshot.topography.settingsHash : null,
            graphNodeCount = snapshot.graph.nodes.Count,
            graphEdgeCount = snapshot.graph.edges.Count,
            runtimeNodeCount = snapshot.nodeRuntime.nodes.Count,
            poiCount = snapshot.pois.pois.Count,
            activeEventCount = snapshot.effects.events.Count,
            activeBuffCount = snapshot.effects.buffs.Count,
            topographySampleCount = topoSamples
        };
    }

    private static string TryGetNodeStableId(MapGraph graph, int nodeIndex)
    {
        if (graph == null || graph.nodes == null || nodeIndex < 0 || nodeIndex >= graph.nodes.Count)
            return null;

        return WorldMapStableIdUtility.BuildNodeStableId(graph.seed, graph.nodes[nodeIndex]);
    }

    private static string TryGetNodeStableId(WorldMapRuntimeBinder binder, int nodeIndex)
    {
        if (binder == null || !binder.IsBuilt || binder.Registry == null)
            return null;

        return binder.Registry.TryGetByIndex(nodeIndex, out MapNodeRuntime rt) && rt != null
            ? rt.StableId
            : null;
    }

    private static float TryGetRouteLength(MapGraph graph, int a, int b)
    {
        if (graph == null || graph.nodes == null)
            return 0f;

        if (a < 0 || a >= graph.nodes.Count || b < 0 || b >= graph.nodes.Count)
            return 0f;

        return Vector2.Distance(graph.nodes[a].position, graph.nodes[b].position);
    }

    private static string BuildEdgeStableId(string aStableId, string bStableId)
    {
        if (string.IsNullOrWhiteSpace(aStableId) || string.IsNullOrWhiteSpace(bStableId))
            return null;

        return string.CompareOrdinal(aStableId, bStableId) <= 0
            ? $"{aStableId}|{bStableId}"
            : $"{bStableId}|{aStableId}";
    }
}
