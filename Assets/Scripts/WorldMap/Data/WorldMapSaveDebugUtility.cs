using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class WorldMapSaveDebugUtility
{
    public static string BuildSummary(WorldMapSaveSnapshot snapshot, string title = "World Map Snapshot")
    {
        if (snapshot == null)
            return $"[{title}] NULL";

        snapshot.EnsureDefaults();

        var sb = new StringBuilder(1024);

        sb.AppendLine($"[{title}]");
        sb.AppendLine($"  Version={snapshot.version} Seed={snapshot.worldSeed} HasPersistedWorld={snapshot.HasPersistedWorld}");

        WorldMapTopographySaveSnapshot topo = snapshot.topography;
        if (topo != null)
        {
            int samples = topo.width * topo.height;
            string storage = topo.HasPackedHeightData
                ? $"{topo.heightEncoding}, base64Chars={SafeLen(topo.heightU16Base64)}"
                : topo.HasLegacyFloatHeightData
                    ? $"legacyFloatList, count={topo.height01.Count}"
                    : "none";

            sb.AppendLine(
                $"  Topography: mode={topo.mode} {topo.width}x{topo.height} samples={samples} " +
                $"sea={topo.effectiveSeaLevel01:0.000} storage={storage}"
            );
        }
        else
        {
            sb.AppendLine("  Topography: NULL");
        }

        sb.AppendLine(
            $"  Graph: nodes={Count(snapshot.graph?.nodes)} edges={Count(snapshot.graph?.edges)}"
        );

        sb.AppendLine(
            $"  Runtime: nodes={Count(snapshot.nodeRuntime?.nodes)}"
        );

        sb.AppendLine(
            $"  POIs: {Count(snapshot.pois?.pois)}"
        );

        sb.AppendLine(
            $"  Effects: events={Count(snapshot.effects?.events)} buffs={Count(snapshot.effects?.buffs)} " +
            $"resolved={Count(snapshot.effects?.resolvedOutcomes)}"
        );

        if (snapshot.player != null)
        {
            sb.AppendLine(
                $"  Player: current='{snapshot.player.currentNodeStableId}' " +
                $"locked='{snapshot.player.lockedSourceNodeStableId}' → '{snapshot.player.lockedDestinationNodeStableId}'"
            );
        }

        if (snapshot.activeTravel != null && snapshot.activeTravel.isTraveling)
        {
            sb.AppendLine(
                $"  ActiveTravel: {snapshot.activeTravel.fromNodeStableId} → {snapshot.activeTravel.toNodeStableId} " +
                $"progress={snapshot.activeTravel.progress01:0.00}"
            );
        }
        else
        {
            sb.AppendLine("  ActiveTravel: none");
        }

        if (snapshot.diagnostics != null)
        {
            sb.AppendLine(
                $"  Diagnostics: topoSamples={snapshot.diagnostics.topographySampleCount} " +
                $"graph={snapshot.diagnostics.graphNodeCount}/{snapshot.diagnostics.graphEdgeCount} " +
                $"runtime={snapshot.diagnostics.runtimeNodeCount} poi={snapshot.diagnostics.poiCount} " +
                $"events={snapshot.diagnostics.activeEventCount} buffs={snapshot.diagnostics.activeBuffCount}"
            );
        }

        return sb.ToString();
    }

    public static bool Validate(
        WorldMapSaveSnapshot snapshot,
        out List<string> errors,
        out List<string> warnings)
    {
        errors = new List<string>();
        warnings = new List<string>();

        if (snapshot == null)
        {
            errors.Add("Snapshot is null.");
            return false;
        }

        snapshot.EnsureDefaults();

        ValidateTopography(snapshot.topography, errors, warnings);
        ValidateGraph(snapshot.graph, errors, warnings);
        ValidateRuntime(snapshot, errors, warnings);
        ValidatePOIs(snapshot.pois, warnings);
        ValidatePlayer(snapshot, warnings);

        return errors.Count == 0;
    }

    public static void LogSummary(WorldMapSaveSnapshot snapshot, string title = "World Map Snapshot", Object context = null)
    {
        Debug.Log(BuildSummary(snapshot, title), context);
    }

    public static void LogValidation(WorldMapSaveSnapshot snapshot, string title = "World Map Snapshot Validation", Object context = null)
    {
        bool ok = Validate(snapshot, out var errors, out var warnings);

        var sb = new StringBuilder(1024);
        sb.AppendLine($"[{title}] {(ok ? "OK" : "FAILED")}");

        if (errors.Count > 0)
        {
            sb.AppendLine("Errors:");
            for (int i = 0; i < errors.Count; i++)
                sb.AppendLine($"  - {errors[i]}");
        }

        if (warnings.Count > 0)
        {
            sb.AppendLine("Warnings:");
            for (int i = 0; i < warnings.Count; i++)
                sb.AppendLine($"  - {warnings[i]}");
        }

        if (errors.Count == 0 && warnings.Count == 0)
            sb.AppendLine("No issues found.");

        if (ok)
            Debug.Log(sb.ToString(), context);
        else
            Debug.LogWarning(sb.ToString(), context);
    }

    public static string FormatBytes(long bytes)
    {
        const double kb = 1024.0;
        const double mb = kb * 1024.0;

        if (bytes >= mb)
            return $"{bytes / mb:0.00} MB";

        if (bytes >= kb)
            return $"{bytes / kb:0.0} KB";

        return $"{bytes} B";
    }

    private static void ValidateTopography(
        WorldMapTopographySaveSnapshot topo,
        List<string> errors,
        List<string> warnings)
    {
        if (topo == null)
        {
            errors.Add("Topography snapshot is null.");
            return;
        }

        if (topo.mode == WorldMapTopographySaveMode.None)
        {
            warnings.Add("Topography mode is None.");
            return;
        }

        if (topo.width <= 0 || topo.height <= 0)
            errors.Add($"Topography dimensions are invalid: {topo.width}x{topo.height}.");

        int expected = topo.width * topo.height;

        if (topo.mode == WorldMapTopographySaveMode.RawHeightData)
        {
            if (!topo.HasRawHeightData)
                errors.Add("Topography raw height data is missing or has the wrong length.");

            if (topo.HasLegacyFloatHeightData)
                warnings.Add("Topography is using legacy JSON float list storage. Saves will be huge, because apparently dragons need paperwork.");

            if (topo.HasPackedHeightData && topo.heightSampleCount != expected)
                errors.Add($"Packed topography sample count mismatch. expected={expected}, actual={topo.heightSampleCount}.");
        }

        if (topo.effectiveSeaLevel01 <= 0f || topo.effectiveSeaLevel01 >= 1f)
            warnings.Add($"Effective sea level looks suspicious: {topo.effectiveSeaLevel01:0.000}.");
    }

    private static void ValidateGraph(
        WorldMapGraphSaveSnapshot graph,
        List<string> errors,
        List<string> warnings)
    {
        if (graph == null)
        {
            errors.Add("Graph snapshot is null.");
            return;
        }

        int nodeCount = Count(graph.nodes);
        if (nodeCount <= 0)
        {
            errors.Add("Graph has no nodes.");
            return;
        }

        var ids = new HashSet<string>();

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            var node = graph.nodes[i];
            if (node == null)
            {
                warnings.Add($"Graph node #{i} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.stableId))
            {
                errors.Add($"Graph node #{i} has no stableId.");
                continue;
            }

            if (!ids.Add(node.stableId))
                errors.Add($"Duplicate graph node stableId '{node.stableId}'.");
        }

        if (Count(graph.edges) <= 0)
            warnings.Add("Graph has no edges.");
    }

    private static void ValidateRuntime(
        WorldMapSaveSnapshot snapshot,
        List<string> errors,
        List<string> warnings)
    {
        int graphNodes = Count(snapshot.graph?.nodes);
        int runtimeNodes = Count(snapshot.nodeRuntime?.nodes);

        if (graphNodes > 0 && runtimeNodes <= 0)
            warnings.Add("Graph exists but runtime node snapshot is empty.");

        if (runtimeNodes > 0 && graphNodes > 0 && runtimeNodes != graphNodes)
            warnings.Add($"Runtime node count differs from graph node count. graph={graphNodes}, runtime={runtimeNodes}.");
    }

    private static void ValidatePOIs(WorldMapPOISetSaveSnapshot pois, List<string> warnings)
    {
        if (pois == null || pois.pois == null)
            return;

        var ids = new HashSet<string>();

        for (int i = 0; i < pois.pois.Count; i++)
        {
            var poi = pois.pois[i];
            if (poi == null)
                continue;

            if (string.IsNullOrWhiteSpace(poi.stableId))
            {
                warnings.Add($"POI #{i} has no stableId.");
                continue;
            }

            if (!ids.Add(poi.stableId))
                warnings.Add($"Duplicate POI stableId '{poi.stableId}'.");

            if (string.IsNullOrWhiteSpace(poi.poiDefId))
                warnings.Add($"POI '{poi.stableId}' has no poiDefId.");
        }
    }

    private static void ValidatePlayer(WorldMapSaveSnapshot snapshot, List<string> warnings)
    {
        if (snapshot.player == null)
            return;

        if (string.IsNullOrWhiteSpace(snapshot.player.currentNodeStableId))
            warnings.Add("Player current node stableId is empty.");
    }

    private static int Count<T>(List<T> list)
    {
        return list != null ? list.Count : 0;
    }

    private static int SafeLen(string s)
    {
        return string.IsNullOrEmpty(s) ? 0 : s.Length;
    }
}
