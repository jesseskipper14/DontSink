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
        report.Merge(ValidateWorldMapSnapshot(gameState.worldMapSnapshot, "GameState.worldMapSnapshot"));

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
        report.Merge(ValidateWorldMapSnapshot(file.payload.worldMapSnapshot, "Save payload world map"));

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

    public static SaveCompatibilityReport ValidateWorldMapSnapshot(
        WorldMapSaveSnapshot snapshot,
        string label)
    {
        SaveCompatibilityReport report = new();

        if (snapshot == null)
        {
            report.Warning($"{label} is null. This save will not restore a persisted world map.");
            return report;
        }

        snapshot.EnsureDefaults();

        if (!snapshot.HasPersistedWorld)
            report.Warning($"{label} does not look like a complete persisted world yet.");

        HashSet<string> nodeIds = ValidateWorldMapGraph(snapshot.graph, label, report);
        ValidateWorldMapTopography(snapshot.topography, label, report);
        ValidateWorldMapRuntime(snapshot.nodeRuntime, nodeIds, label, report);
        ValidateWorldMapPOIs(snapshot.pois, label, report);
        ValidateWorldMapEffects(snapshot.effects, nodeIds, label, report);
        ValidateWorldMapPlayer(snapshot.player, snapshot.activeTravel, nodeIds, label, report);
        ValidateWorldMapKnowledge(snapshot.knowledge, nodeIds, label, report);
        ValidateWorldMapDiagnostics(snapshot, label, report);

        if (!report.HasErrors && !report.HasWarnings)
        {
            int nodeCount = snapshot.graph != null && snapshot.graph.nodes != null ? snapshot.graph.nodes.Count : 0;
            int edgeCount = snapshot.graph != null && snapshot.graph.edges != null ? snapshot.graph.edges.Count : 0;
            int poiCount = snapshot.pois != null && snapshot.pois.pois != null ? snapshot.pois.pois.Count : 0;

            report.AddInfo($"{label} passed world map validation. Nodes={nodeCount}, Edges={edgeCount}, POIs={poiCount}.");
        }

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

    private static void ValidateWorldMapTopography(
        WorldMapTopographySaveSnapshot topography,
        string label,
        SaveCompatibilityReport report)
    {
        string scope = $"{label}.topography";

        if (topography == null)
        {
            report.Error($"{scope} is null.");
            return;
        }

        if (topography.mode == WorldMapTopographySaveMode.None)
        {
            report.Warning($"{scope} mode is None. Topography will need generation/fallback.");
            return;
        }

        if (topography.width <= 0 || topography.height <= 0)
            report.Error($"{scope} has invalid dimensions {topography.width}x{topography.height}.");

        if (topography.worldBoundsWidth <= 0f || topography.worldBoundsHeight <= 0f)
            report.Error($"{scope} has invalid world bounds {topography.worldBoundsWidth:0.###}x{topography.worldBoundsHeight:0.###}.");

        int expectedSamples = Mathf.Max(0, topography.width * topography.height);

        if (topography.mode == WorldMapTopographySaveMode.RawHeightData)
        {
            if (!topography.HasRawHeightData)
            {
                report.Error($"{scope} is RawHeightData but has no valid height payload.");
            }
            else if (topography.HasPackedHeightData)
            {
                if (topography.heightEncoding != WorldMapTopographyHeightCodec.UShortBase64Encoding)
                {
                    report.Warning(
                        $"{scope} packed height encoding is '{topography.heightEncoding}', expected '{WorldMapTopographyHeightCodec.UShortBase64Encoding}'.");
                }

                if (topography.heightQuantizationBits != 16)
                    report.Warning($"{scope} quantization bits is {topography.heightQuantizationBits}, expected 16.");

                if (topography.heightSampleCount != expectedSamples)
                    report.Error($"{scope} packed sample count mismatch. expected={expectedSamples}, actual={topography.heightSampleCount}.");
            }
            else if (topography.HasLegacyFloatHeightData)
            {
                report.Warning($"{scope} uses legacy JSON float height storage. It will work, but it will be huge because apparently numbers wanted to become soup.");
            }
        }

        if (topography.effectiveSeaLevel01 <= 0f || topography.effectiveSeaLevel01 >= 1f)
            report.Warning($"{scope} effectiveSeaLevel01 is suspicious: {topography.effectiveSeaLevel01:0.###}.");

        if (topography.stats != null)
        {
            float total = topography.stats.water01 + topography.stats.land01;
            if (Mathf.Abs(total - 1f) > 0.05f)
                report.Warning($"{scope} stats water+land total is {total:0.###}, expected approximately 1.");
        }
        else
        {
            report.Warning($"{scope} stats are null.");
        }
    }

    private static HashSet<string> ValidateWorldMapGraph(
        WorldMapGraphSaveSnapshot graph,
        string label,
        SaveCompatibilityReport report)
    {
        string scope = $"{label}.graph";
        HashSet<string> nodeIds = new();

        if (graph == null)
        {
            report.Error($"{scope} is null.");
            return nodeIds;
        }

        if (graph.nodes == null || graph.nodes.Count == 0)
        {
            report.Error($"{scope} has no nodes.");
            return nodeIds;
        }

        HashSet<int> nodeIndexes = new();

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            WorldMapGraphNodeSaveSnapshot node = graph.nodes[i];

            if (node == null)
            {
                report.Error($"{scope}.nodes[{i}] is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.stableId))
            {
                report.Error($"{scope}.nodes[{i}] has empty stableId.");
            }
            else if (!nodeIds.Add(node.stableId))
            {
                report.Error($"{scope} has duplicate node stableId '{node.stableId}'.");
            }

            if (node.nodeIndex < 0 || node.nodeIndex >= graph.nodes.Count)
                report.Warning($"{scope}.nodes[{i}] has suspicious nodeIndex {node.nodeIndex} for count {graph.nodes.Count}.");

            if (!nodeIndexes.Add(node.nodeIndex))
                report.Warning($"{scope} has duplicate nodeIndex {node.nodeIndex}.");

            if (string.IsNullOrWhiteSpace(node.displayName))
                report.Warning($"{scope}.nodes[{i}] '{node.stableId}' has empty displayName.");

            if (!IsFinite(node.positionX) || !IsFinite(node.positionY))
                report.Error($"{scope}.nodes[{i}] '{node.stableId}' has non-finite position.");
        }

        if (graph.edges == null)
        {
            report.Warning($"{scope}.edges is null.");
            return nodeIds;
        }

        if (graph.edges.Count == 0)
            report.Warning($"{scope} has no edges.");

        HashSet<string> edgeIds = new();

        for (int i = 0; i < graph.edges.Count; i++)
        {
            WorldMapGraphEdgeSaveSnapshot edge = graph.edges[i];

            if (edge == null)
            {
                report.Warning($"{scope}.edges[{i}] is null.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(edge.stableId) && !edgeIds.Add(edge.stableId))
                report.Warning($"{scope} has duplicate edge stableId '{edge.stableId}'.");

            bool hasStableEndpoints =
                !string.IsNullOrWhiteSpace(edge.aStableId) &&
                !string.IsNullOrWhiteSpace(edge.bStableId);

            if (hasStableEndpoints)
            {
                if (!nodeIds.Contains(edge.aStableId))
                    report.Error($"{scope}.edges[{i}] aStableId '{edge.aStableId}' does not resolve.");
                if (!nodeIds.Contains(edge.bStableId))
                    report.Error($"{scope}.edges[{i}] bStableId '{edge.bStableId}' does not resolve.");
            }
            else
            {
                if (edge.aIndex < 0 || edge.aIndex >= graph.nodes.Count)
                    report.Error($"{scope}.edges[{i}] aIndex {edge.aIndex} is out of range.");

                if (edge.bIndex < 0 || edge.bIndex >= graph.nodes.Count)
                    report.Error($"{scope}.edges[{i}] bIndex {edge.bIndex} is out of range.");
            }

            if (edge.aStableId == edge.bStableId && !string.IsNullOrWhiteSpace(edge.aStableId))
                report.Warning($"{scope}.edges[{i}] connects node '{edge.aStableId}' to itself.");

            if (edge.routeLength < 0f)
                report.Warning($"{scope}.edges[{i}] has negative routeLength {edge.routeLength:0.###}.");
        }

        return nodeIds;
    }

    private static void ValidateWorldMapRuntime(
        WorldMapNodeRuntimeStateSetSnapshot runtime,
        HashSet<string> nodeIds,
        string label,
        SaveCompatibilityReport report)
    {
        string scope = $"{label}.nodeRuntime";

        if (runtime == null)
        {
            report.Warning($"{scope} is null.");
            return;
        }

        if (runtime.nodes == null || runtime.nodes.Count == 0)
        {
            report.Warning($"{scope} has no nodes.");
            return;
        }

        if (nodeIds != null && nodeIds.Count > 0 && runtime.nodes.Count != nodeIds.Count)
        {
            report.Warning(
                $"{scope} node count {runtime.nodes.Count} differs from graph node count {nodeIds.Count}.");
        }

        HashSet<string> runtimeIds = new();

        for (int i = 0; i < runtime.nodes.Count; i++)
        {
            WorldMapNodeRuntimeStateSaveSnapshot node = runtime.nodes[i];

            if (node == null)
            {
                report.Warning($"{scope}.nodes[{i}] is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.stableId))
            {
                report.Error($"{scope}.nodes[{i}] has empty stableId.");
            }
            else
            {
                if (!runtimeIds.Add(node.stableId))
                    report.Error($"{scope} has duplicate runtime stableId '{node.stableId}'.");

                if (nodeIds != null && nodeIds.Count > 0 && !nodeIds.Contains(node.stableId))
                    report.Error($"{scope}.nodes[{i}] stableId '{node.stableId}' is not in graph.");
            }

            if (node.stats == null || node.stats.Count == 0)
            {
                report.Warning($"{scope}.nodes[{i}] '{node.stableId}' has no stats.");
            }
            else
            {
                HashSet<string> statIds = new();

                for (int s = 0; s < node.stats.Count; s++)
                {
                    WorldMapNodeStatSaveSnapshot stat = node.stats[s];

                    if (stat == null)
                    {
                        report.Warning($"{scope}.nodes[{i}].stats[{s}] is null.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(stat.statId))
                        report.Warning($"{scope}.nodes[{i}].stats[{s}] has empty statId.");
                    else if (!statIds.Add(stat.statId))
                        report.Warning($"{scope}.nodes[{i}] has duplicate stat '{stat.statId}'.");

                    if (!IsFinite(stat.value) || !IsFinite(stat.velocity) || !IsFinite(stat.equilibrium))
                        report.Error($"{scope}.nodes[{i}].stats[{s}] '{stat.statId}' has non-finite values.");
                }
            }

            if (node.population < 0f)
                report.Warning($"{scope}.nodes[{i}] '{node.stableId}' has negative population {node.population:0.###}.");
        }
    }

    private static void ValidateWorldMapPOIs(
        WorldMapPOISetSaveSnapshot pois,
        string label,
        SaveCompatibilityReport report)
    {
        string scope = $"{label}.pois";

        if (pois == null)
        {
            report.Warning($"{scope} is null.");
            return;
        }

        if (pois.pois == null)
        {
            report.Warning($"{scope}.pois list is null.");
            return;
        }

        HashSet<string> ids = new();

        for (int i = 0; i < pois.pois.Count; i++)
        {
            WorldMapPOISaveSnapshot poi = pois.pois[i];

            if (poi == null)
            {
                report.Warning($"{scope}.pois[{i}] is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(poi.stableId))
            {
                report.Warning($"{scope}.pois[{i}] has empty stableId.");
            }
            else if (!ids.Add(poi.stableId))
            {
                report.Warning($"{scope} has duplicate POI stableId '{poi.stableId}'.");
            }

            if (string.IsNullOrWhiteSpace(poi.poiDefId))
                report.Warning($"{scope}.pois[{i}] '{poi.stableId}' has empty poiDefId.");

            if (!IsFinite(poi.positionX) || !IsFinite(poi.positionY))
                report.Error($"{scope}.pois[{i}] '{poi.stableId}' has non-finite position.");

            if (poi.depth01 <= 0f)
                report.Warning($"{scope}.pois[{i}] '{poi.stableId}' has depth01 <= 0, but POIs are expected to be underwater.");

            if (!Is01(poi.height01))
                report.Warning($"{scope}.pois[{i}] '{poi.stableId}' height01 is outside 0..1: {poi.height01:0.###}.");

            if (!Is01(poi.depth01))
                report.Warning($"{scope}.pois[{i}] '{poi.stableId}' depth01 is outside 0..1: {poi.depth01:0.###}.");
        }
    }

    private static void ValidateWorldMapEffects(
        WorldMapEffectStateSaveSnapshot effects,
        HashSet<string> nodeIds,
        string label,
        SaveCompatibilityReport report)
    {
        string scope = $"{label}.effects";

        if (effects == null)
        {
            report.Warning($"{scope} is null.");
            return;
        }

        if (effects.events != null)
        {
            HashSet<string> eventInstanceIds = new();

            for (int i = 0; i < effects.events.Count; i++)
            {
                WorldMapEventSaveSnapshot ev = effects.events[i];

                if (ev == null)
                {
                    report.Warning($"{scope}.events[{i}] is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(ev.instanceId))
                    report.Warning($"{scope}.events[{i}] has empty instanceId.");
                else if (!eventInstanceIds.Add(ev.instanceId))
                    report.Warning($"{scope} has duplicate event instanceId '{ev.instanceId}'.");

                if (string.IsNullOrWhiteSpace(ev.eventId))
                    report.Warning($"{scope}.events[{i}] '{ev.instanceId}' has empty eventId.");

                ValidateOptionalNodeReference(ev.sourceNodeStableId, nodeIds, $"{scope}.events[{i}].sourceNodeStableId", report);
                ValidateOptionalNodeReference(ev.targetNodeStableId, nodeIds, $"{scope}.events[{i}].targetNodeStableId", report);

                if (ev.remainingHours < 0f)
                    report.Warning($"{scope}.events[{i}] '{ev.instanceId}' has negative remainingHours {ev.remainingHours:0.###}.");
            }
        }

        if (effects.buffs != null)
        {
            HashSet<string> buffInstanceIds = new();

            for (int i = 0; i < effects.buffs.Count; i++)
            {
                WorldMapBuffSaveSnapshot buff = effects.buffs[i];

                if (buff == null)
                {
                    report.Warning($"{scope}.buffs[{i}] is null.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(buff.instanceId) && !buffInstanceIds.Add(buff.instanceId))
                    report.Warning($"{scope} has duplicate buff instanceId '{buff.instanceId}'.");

                if (string.IsNullOrWhiteSpace(buff.buffId))
                    report.Warning($"{scope}.buffs[{i}] has empty buffId.");

                ValidateRequiredNodeReference(buff.nodeStableId, nodeIds, $"{scope}.buffs[{i}].nodeStableId", report);

                if (buff.remainingHours < 0f)
                    report.Warning($"{scope}.buffs[{i}] '{buff.instanceId}' has negative remainingHours {buff.remainingHours:0.###}.");

                if (buff.stacks <= 0)
                    report.Warning($"{scope}.buffs[{i}] '{buff.instanceId}' has non-positive stacks {buff.stacks}.");
            }
        }

        if (effects.resolvedOutcomes != null)
        {
            for (int i = 0; i < effects.resolvedOutcomes.Count; i++)
            {
                WorldMapResolvedOutcomeSaveSnapshot outcome = effects.resolvedOutcomes[i];

                if (outcome == null)
                {
                    report.Warning($"{scope}.resolvedOutcomes[{i}] is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(outcome.outcomeId))
                    report.Warning($"{scope}.resolvedOutcomes[{i}] has empty outcomeId.");

                ValidateOptionalNodeReference(outcome.nodeStableId, nodeIds, $"{scope}.resolvedOutcomes[{i}].nodeStableId", report);
            }
        }
    }

    private static void ValidateWorldMapPlayer(
        WorldMapPlayerSaveSnapshot player,
        WorldMapActiveTravelSaveSnapshot activeTravel,
        HashSet<string> nodeIds,
        string label,
        SaveCompatibilityReport report)
    {
        string scope = $"{label}.player";

        if (player == null)
        {
            report.Warning($"{scope} is null.");
        }
        else
        {
            ValidateRequiredNodeReference(player.currentNodeStableId, nodeIds, $"{scope}.currentNodeStableId", report);
            ValidateOptionalNodeReference(player.lockedSourceNodeStableId, nodeIds, $"{scope}.lockedSourceNodeStableId", report);
            ValidateOptionalNodeReference(player.lockedDestinationNodeStableId, nodeIds, $"{scope}.lockedDestinationNodeStableId", report);
            ValidateOptionalNodeReference(player.lastVisitedNodeStableId, nodeIds, $"{scope}.lastVisitedNodeStableId", report);
        }

        if (activeTravel != null && activeTravel.isTraveling)
        {
            ValidateRequiredNodeReference(activeTravel.fromNodeStableId, nodeIds, $"{label}.activeTravel.fromNodeStableId", report);
            ValidateRequiredNodeReference(activeTravel.toNodeStableId, nodeIds, $"{label}.activeTravel.toNodeStableId", report);

            if (activeTravel.routeLength < 0f)
                report.Warning($"{label}.activeTravel.routeLength is negative.");

            if (!Is01(activeTravel.progress01))
                report.Warning($"{label}.activeTravel.progress01 outside 0..1: {activeTravel.progress01:0.###}.");
        }
    }

    private static void ValidateWorldMapKnowledge(
        WorldMapKnowledgeSaveSnapshot knowledge,
        HashSet<string> nodeIds,
        string label,
        SaveCompatibilityReport report)
    {
        if (knowledge == null)
            return;

        ValidateNodeList(knowledge.knownNodeStableIds, nodeIds, $"{label}.knowledge.knownNodeStableIds", report);
        ValidateNodeList(knowledge.discoveredNodeStableIds, nodeIds, $"{label}.knowledge.discoveredNodeStableIds", report);

        WarnDuplicateStrings(knowledge.knownRouteStableIds, $"{label}.knowledge.knownRouteStableIds", report);
        WarnDuplicateStrings(knowledge.partialRouteStableIds, $"{label}.knowledge.partialRouteStableIds", report);
        WarnDuplicateStrings(knowledge.rumoredRouteStableIds, $"{label}.knowledge.rumoredRouteStableIds", report);
    }

    private static void ValidateWorldMapDiagnostics(
        WorldMapSaveSnapshot snapshot,
        string label,
        SaveCompatibilityReport report)
    {
        if (snapshot == null || snapshot.diagnostics == null)
            return;

        WorldMapSaveDiagnosticsSnapshot d = snapshot.diagnostics;

        int actualTopoSamples =
            snapshot.topography != null
                ? Mathf.Max(0, snapshot.topography.width * snapshot.topography.height)
                : 0;

        int actualGraphNodes = snapshot.graph != null && snapshot.graph.nodes != null ? snapshot.graph.nodes.Count : 0;
        int actualGraphEdges = snapshot.graph != null && snapshot.graph.edges != null ? snapshot.graph.edges.Count : 0;
        int actualRuntimeNodes = snapshot.nodeRuntime != null && snapshot.nodeRuntime.nodes != null ? snapshot.nodeRuntime.nodes.Count : 0;
        int actualPois = snapshot.pois != null && snapshot.pois.pois != null ? snapshot.pois.pois.Count : 0;
        int actualEvents = snapshot.effects != null && snapshot.effects.events != null ? snapshot.effects.events.Count : 0;
        int actualBuffs = snapshot.effects != null && snapshot.effects.buffs != null ? snapshot.effects.buffs.Count : 0;

        WarnIfCountMismatch(d.topographySampleCount, actualTopoSamples, $"{label}.diagnostics.topographySampleCount", report);
        WarnIfCountMismatch(d.graphNodeCount, actualGraphNodes, $"{label}.diagnostics.graphNodeCount", report);
        WarnIfCountMismatch(d.graphEdgeCount, actualGraphEdges, $"{label}.diagnostics.graphEdgeCount", report);
        WarnIfCountMismatch(d.runtimeNodeCount, actualRuntimeNodes, $"{label}.diagnostics.runtimeNodeCount", report);
        WarnIfCountMismatch(d.poiCount, actualPois, $"{label}.diagnostics.poiCount", report);
        WarnIfCountMismatch(d.activeEventCount, actualEvents, $"{label}.diagnostics.activeEventCount", report);
        WarnIfCountMismatch(d.activeBuffCount, actualBuffs, $"{label}.diagnostics.activeBuffCount", report);
    }

    private static void ValidateNodeList(
        List<string> ids,
        HashSet<string> validIds,
        string label,
        SaveCompatibilityReport report)
    {
        if (ids == null)
            return;

        HashSet<string> seen = new();

        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];

            if (string.IsNullOrWhiteSpace(id))
            {
                report.Warning($"{label}[{i}] is empty.");
                continue;
            }

            if (!seen.Add(id))
                report.Warning($"{label} contains duplicate node id '{id}'.");

            if (validIds != null && validIds.Count > 0 && !validIds.Contains(id))
                report.Warning($"{label}[{i}] '{id}' does not resolve to a graph node.");
        }
    }

    private static void WarnDuplicateStrings(
        List<string> values,
        string label,
        SaveCompatibilityReport report)
    {
        if (values == null)
            return;

        HashSet<string> seen = new();

        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];

            if (string.IsNullOrWhiteSpace(value))
            {
                report.Warning($"{label}[{i}] is empty.");
                continue;
            }

            if (!seen.Add(value))
                report.Warning($"{label} contains duplicate '{value}'.");
        }
    }

    private static void ValidateRequiredNodeReference(
        string stableId,
        HashSet<string> nodeIds,
        string label,
        SaveCompatibilityReport report)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            report.Warning($"{label} is empty.");
            return;
        }

        if (nodeIds != null && nodeIds.Count > 0 && !nodeIds.Contains(stableId))
            report.Error($"{label} '{stableId}' does not resolve to a graph node.");
    }

    private static void ValidateOptionalNodeReference(
        string stableId,
        HashSet<string> nodeIds,
        string label,
        SaveCompatibilityReport report)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return;

        if (nodeIds != null && nodeIds.Count > 0 && !nodeIds.Contains(stableId))
            report.Error($"{label} '{stableId}' does not resolve to a graph node.");
    }

    private static void WarnIfCountMismatch(
        int recorded,
        int actual,
        string label,
        SaveCompatibilityReport report)
    {
        if (recorded != actual)
            report.Warning($"{label} says {recorded}, actual is {actual}.");
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool Is01(float value)
    {
        return IsFinite(value) && value >= 0f && value <= 1f;
    }
}
