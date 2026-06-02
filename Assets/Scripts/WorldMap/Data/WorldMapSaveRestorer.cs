using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldMapSaveRestorer
{
    public static bool HasUsableSnapshot(GameState gs = null)
    {
        gs ??= GameState.I;

        WorldMapSaveSnapshot snapshot = gs != null ? gs.worldMapSnapshot : null;
        return snapshot != null && snapshot.HasPersistedWorld;
    }

    public static bool TryGetRuntimeIdentity(
        string stableId,
        out string clusterAffinityId,
        out string nodeArchetypeId)
    {
        clusterAffinityId = null;
        nodeArchetypeId = null;

        GameState gs = GameState.I;
        WorldMapSaveSnapshot snapshot = gs != null ? gs.worldMapSnapshot : null;

        if (snapshot == null || snapshot.nodeRuntime == null || snapshot.nodeRuntime.nodes == null)
            return false;

        for (int i = 0; i < snapshot.nodeRuntime.nodes.Count; i++)
        {
            WorldMapNodeRuntimeStateSaveSnapshot node = snapshot.nodeRuntime.nodes[i];
            if (node == null || node.stableId != stableId)
                continue;

            clusterAffinityId = node.clusterAffinityId;
            nodeArchetypeId = node.nodeArchetypeId;
            return true;
        }

        return false;
    }

    public static void RestoreNodeRuntimeStateToGameState(GameState gs = null)
    {
        gs ??= GameState.I;
        if (gs == null || gs.worldMapSnapshot == null || gs.worldMapSnapshot.nodeRuntime == null)
            return;

        if (gs.worldMap == null)
            gs.worldMap = new WorldMapSimState();

        gs.worldMap.byNodeStableId ??= new Dictionary<string, MapNodeState>();
        gs.worldMap.byNodeStableId.Clear();

        List<WorldMapNodeRuntimeStateSaveSnapshot> nodes = gs.worldMapSnapshot.nodeRuntime.nodes;
        if (nodes == null)
            return;

        for (int i = 0; i < nodes.Count; i++)
        {
            WorldMapNodeRuntimeStateSaveSnapshot snap = nodes[i];
            if (snap == null || string.IsNullOrWhiteSpace(snap.stableId))
                continue;

            MapNodeState state = RestoreNodeState(snap);
            gs.worldMap.byNodeStableId[snap.stableId] = state;
        }

        Debug.Log(
            $"[WorldMapSaveRestorer] Restored node runtime state store. Nodes={gs.worldMap.byNodeStableId.Count}.",
            gs
        );
    }

    public static bool TryRestoreGraphToGenerator(WorldMapGraphGenerator generator, GameState gs = null)
    {
        gs ??= GameState.I;
        WorldMapSaveSnapshot snapshot = gs != null ? gs.worldMapSnapshot : null;

        if (generator == null || snapshot == null || snapshot.graph == null)
            return false;

        if (snapshot.graph.nodes == null || snapshot.graph.nodes.Count == 0)
            return false;

        MapGraph graph = RestoreGraph(snapshot.graph);
        if (graph == null)
            return false;

        generator.UseRestoredGraph(graph, "WorldMapSaveRestorer");

        Debug.Log(
            $"[WorldMapSaveRestorer] Restored graph. Nodes={graph.nodes?.Count ?? 0}, Edges={graph.edges?.Count ?? 0}.",
            generator
        );

        return true;
    }

    public static bool TryRestoreKnowledgeToSource(WorldMapKnowledgeSource source, GameState gs = null)
    {
        gs ??= GameState.I;
        WorldMapSaveSnapshot snapshot = gs != null ? gs.worldMapSnapshot : null;

        if (source == null || snapshot == null || snapshot.knowledge == null)
            return false;

        if (!snapshot.knowledge.HasKnowledgeGrid)
            return false;

        bool ok = source.TryRestoreFromSnapshot(snapshot.knowledge);

        Debug.Log(
            $"[WorldMapSaveRestorer] Restore knowledge {(ok ? "OK" : "FAILED")}. " +
            $"Surface={snapshot.knowledge.surfaceRevealedCount}, Underwater={snapshot.knowledge.underwaterSurveyedCount}.",
            source
        );

        return ok;
    }

    public static bool TryRestorePOIsToSource(WorldMapPOISource source, GameState gs = null)
    {
        gs ??= GameState.I;
        WorldMapSaveSnapshot snapshot = gs != null ? gs.worldMapSnapshot : null;

        if (source == null || snapshot == null || snapshot.pois == null)
            return false;

        if (snapshot.pois.pois == null || snapshot.pois.pois.Count == 0)
            return false;

        bool ok = source.TryRestoreFromSnapshot(snapshot.pois);

        Debug.Log(
            $"[WorldMapSaveRestorer] Restore POIs {(ok ? "OK" : "FAILED")}. Count={snapshot.pois.pois?.Count ?? 0}.",
            source
        );

        return ok;
    }

    public static bool TryRestoreTopographyToSource(WorldMapTopographyDebugSource source, GameState gs = null)
    {
        gs ??= GameState.I;
        WorldMapSaveSnapshot snapshot = gs != null ? gs.worldMapSnapshot : null;

        if (source == null || snapshot == null || snapshot.topography == null)
            return false;

        bool ok = source.TryRestoreFromSnapshot(snapshot.topography);

        Debug.Log(
            $"[WorldMapSaveRestorer] Restore topography {(ok ? "OK" : "FAILED")}. " +
            $"Res={snapshot.topography.width}x{snapshot.topography.height}.",
            source
        );

        return ok;
    }

    public static bool TryRestoreEffectsToEventManager(
        WorldMapEventManager eventManager,
        WorldMapRuntimeBinder runtimeBinder,
        WorldMapGraphGenerator generator,
        GameState gs = null)
    {
        gs ??= GameState.I;
        WorldMapSaveSnapshot snapshot = gs != null ? gs.worldMapSnapshot : null;

        if (eventManager == null || runtimeBinder == null || !runtimeBinder.IsBuilt)
            return false;

        if (snapshot == null || snapshot.effects == null)
            return false;

        bool ok = eventManager.TryRestoreFromSnapshot(snapshot.effects, runtimeBinder, generator);

        Debug.Log(
            $"[WorldMapSaveRestorer] Restore effects {(ok ? "OK" : "FAILED")}. " +
            $"Events={snapshot.effects.events?.Count ?? 0}, Buffs={snapshot.effects.buffs?.Count ?? 0}.",
            eventManager
        );

        return ok;
    }

    public static MapGraph RestoreGraph(WorldMapGraphSaveSnapshot snapshot)
    {
        if (snapshot == null || snapshot.nodes == null || snapshot.nodes.Count == 0)
            return null;

        var graph = new MapGraph(snapshot.seed);

        for (int i = 0; i < snapshot.nodes.Count; i++)
        {
            WorldMapGraphNodeSaveSnapshot ns = snapshot.nodes[i];
            if (ns == null)
                continue;

            MapNode node = new MapNode
            {
                id = ns.nodeIndex,
                localStableId = ns.localStableId,
                clusterId = ns.clusterId,
                position = ns.Position,
                kind = ParseEnum(ns.kind, NodeKind.Island),
                isPrimary = ns.isPrimary,
                displayName = ns.displayName,
                biome = ParseEnum(ns.biomeId, BiomeId.None),
                primaryResource = ParseEnum(ns.primaryResourceId, ResourceId.None),
                secondaryResource = ParseEnum(ns.secondaryResourceId, ResourceId.None),
                primaryFaction = ParseEnum(ns.primaryFactionId, FactionId.None),
                secondaryFaction = ParseEnum(ns.secondaryFactionId, FactionId.None),
                dock = new BuildingRating(ns.dockRating),
                tradeHub = new BuildingRating(ns.tradeRating),
                population = ns.population,
                minPopulation = ns.minPopulation <= 0f ? 10f : ns.minPopulation,
                maxPopulation = ns.maxPopulation <= 0f ? 500f : ns.maxPopulation,
                notes = ns.notes,
                stats = new List<NodeStat>(),
                optionalBuildings = new List<OptionalBuilding>(),
                flags = ns.flags != null ? new List<string>(ns.flags) : new List<string>()
            };

            if (ns.initialStats != null)
            {
                for (int s = 0; s < ns.initialStats.Count; s++)
                {
                    WorldMapNodeStatSaveSnapshot ss = ns.initialStats[s];
                    if (ss == null)
                        continue;

                    node.stats.Add(new NodeStat
                    {
                        id = ParseEnum(ss.statId, NodeStatId.Prosperity),
                        stat = RestoreSimStat(ss)
                    });
                }
            }

            if (ns.optionalBuildings != null)
            {
                for (int b = 0; b < ns.optionalBuildings.Count; b++)
                {
                    WorldMapNodeOptionalBuildingSaveSnapshot os = ns.optionalBuildings[b];
                    if (os == null)
                        continue;

                    node.optionalBuildings.Add(new OptionalBuilding
                    {
                        id = ParseEnum(os.buildingId, SettlementBuildingId.Dock),
                        present = os.present,
                        rating = new BuildingRating(os.rating)
                    });
                }
            }

            graph.nodes.Add(node);
        }

        if (snapshot.edges != null)
        {
            for (int i = 0; i < snapshot.edges.Count; i++)
            {
                WorldMapGraphEdgeSaveSnapshot es = snapshot.edges[i];
                if (es == null)
                    continue;

                int a = ResolveNodeIndex(graph, es.aStableId, es.aIndex);
                int b = ResolveNodeIndex(graph, es.bStableId, es.bIndex);

                if (a < 0 || b < 0 || a == b)
                    continue;

                graph.AddEdge(a, b, ParseEnum(es.routeType, EdgeKind.Route));
            }
        }

        graph.RebuildEdgeSet();
        return graph;
    }

    private static MapNodeState RestoreNodeState(WorldMapNodeRuntimeStateSaveSnapshot snap)
    {
        var state = new MapNodeState(snap.stableId)
        {
            population = snap.population,
            minPopulation = snap.minPopulation <= 0f ? 10f : snap.minPopulation,
            maxPopulation = snap.maxPopulation <= 0f ? 500f : snap.maxPopulation
        };

        if (snap.stats != null)
        {
            for (int i = 0; i < snap.stats.Count; i++)
            {
                WorldMapNodeStatSaveSnapshot ss = snap.stats[i];
                if (ss == null)
                    continue;

                NodeStatId id = ParseEnum(ss.statId, NodeStatId.Prosperity);
                state.SetStatPreserveVelocity(id, RestoreSimStat(ss));
            }
        }

        if (snap.factionInfluence != null)
        {
            for (int i = 0; i < snap.factionInfluence.Count; i++)
            {
                WorldMapNodeFactionInfluenceSaveSnapshot fs = snap.factionInfluence[i];
                if (fs == null)
                    continue;

                state.SetFactionInfluence(ParseEnum(fs.factionId, FactionId.None), fs.value01);
            }
        }

        if (snap.flags != null)
        {
            for (int i = 0; i < snap.flags.Count; i++)
                state.AddFlag(snap.flags[i]);
        }

        if (snap.resourcePressures != null)
        {
            for (int i = 0; i < snap.resourcePressures.Count; i++)
            {
                WorldMapNodeResourcePressureSaveSnapshot rp = snap.resourcePressures[i];
                if (rp == null || string.IsNullOrWhiteSpace(rp.itemId))
                    continue;

                state.SetResourceBaseline(rp.itemId, rp.baseline, rp.driftRate);
                state.AddPressureImpulse(rp.itemId, rp.value - rp.baseline, rp.driftRate);
            }
        }

        return state;
    }

    private static SimStat RestoreSimStat(WorldMapNodeStatSaveSnapshot snap)
    {
        if (snap == null)
            return new SimStat(1f, 1.2f, 0.15f);

        return new SimStat(
            snap.value,
            snap.equilibrium,
            snap.restoreStrength,
            snap.minValue,
            snap.maxValue)
        {
            velocity = snap.velocity
        };
    }

    private static int ResolveNodeIndex(MapGraph graph, string stableId, int fallbackIndex)
    {
        if (graph == null || graph.nodes == null)
            return -1;

        if (!string.IsNullOrWhiteSpace(stableId))
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                string id = WorldMapStableIdUtility.BuildNodeStableId(graph.seed, graph.nodes[i]);
                if (id == stableId)
                    return i;
            }
        }

        return fallbackIndex >= 0 && fallbackIndex < graph.nodes.Count
            ? fallbackIndex
            : -1;
    }

    public static int ResolveRuntimeNodeIndex(WorldMapRuntimeBinder binder, string stableId)
    {
        if (binder == null || !binder.IsBuilt || binder.Registry == null)
            return -1;

        if (string.IsNullOrWhiteSpace(stableId))
            return -1;

        return binder.Registry.TryGetByStableId(stableId, out MapNodeRuntime rt) && rt != null
            ? rt.NodeIndex
            : -1;
    }

    private static T ParseEnum<T>(string value, T fallback)
        where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return Enum.TryParse(value, out T parsed)
            ? parsed
            : fallback;
    }
}
