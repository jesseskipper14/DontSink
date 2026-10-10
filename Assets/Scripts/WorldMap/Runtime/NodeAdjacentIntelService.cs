using System;
using System.Collections.Generic;
using MiniGames;
using UnityEngine;

/// <summary>Graph contacts determine eligibility. Only explicitly received snapshots determine freshness.</summary>
public static class NodeAdjacentIntelService
{
    public static List<string> GetNeighborIds(string sourceId)
    {
        var result = new List<string>();
        var graph = HarborTravelService.CurrentGraph;
        int source = IndexOf(graph, sourceId);
        if (source < 0 || graph.edges == null) return result;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in graph.edges)
        {
            int neighbor = edge.a == source ? edge.b : edge.b == source ? edge.a : -1;
            if (neighbor < 0 || neighbor == source || neighbor >= graph.nodes.Count || graph.nodes[neighbor] == null) continue;
            string id = WorldMapStableIdUtility.BuildNodeStableId(graph.seed, graph.nodes[neighbor]);
            if (seen.Add(id)) result.Add(id);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    public static AdjacentNodeIntelSnapshot GetLastReport(string sourceId, string observedId)
    {
        if (!GetNeighborIds(sourceId).Contains(observedId)) return null;
        var state = NodeCivicQuestProvider.FindState(sourceId);
        var found = state?.civic?.adjacentIntel?.Find(i => i != null && i.sourceNodeId == sourceId && i.observedNodeId == observedId);
        return found?.Copy();
    }

    public static bool TryReceive(AdjacentNodeIntelSnapshot snapshot, out string reason)
    {
        reason = "Intel updates require the authoritative host.";
        if (!GameplayAuthority.IsAuthoritative) return false;
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.report) ||
            !ValidTime(snapshot.observedAtWorldHours) || !ValidTime(snapshot.receivedAtWorldHours) ||
            snapshot.receivedAtWorldHours < snapshot.observedAtWorldHours ||
            !GetNeighborIds(snapshot.sourceNodeId).Contains(snapshot.observedNodeId))
        { reason = "Invalid report or settlements are not contact neighbors."; return false; }
        var state = NodeCivicQuestProvider.FindState(snapshot.sourceNodeId);
        if (state == null) { reason = "Receiving settlement state is unavailable."; return false; }
        state.civic ??= new NodeCivicState(); state.civic.EnsureDefaults();
        if (!string.IsNullOrWhiteSpace(snapshot.contactId) && state.civic.receivedIntelContactIds.Contains(snapshot.contactId))
        { reason = "This trade contact's report has already been received."; return false; }
        int index = state.civic.adjacentIntel.FindIndex(i => i != null && i.observedNodeId == snapshot.observedNodeId && i.sourceNodeId == snapshot.sourceNodeId);
        if (index >= 0)
        {
            var old = state.civic.adjacentIntel[index];
            if ((!string.IsNullOrWhiteSpace(snapshot.contactId) && snapshot.contactId == old.contactId) ||
                snapshot.receivedAtWorldHours <= old.receivedAtWorldHours || snapshot.observedAtWorldHours < old.observedAtWorldHours)
            { reason = "A newer or identical report is already stored."; return false; }
            state.civic.adjacentIntel[index] = snapshot.Copy();
        }
        else state.civic.adjacentIntel.Add(snapshot.Copy());
        if (!string.IsNullOrWhiteSpace(snapshot.contactId)) state.civic.receivedIntelContactIds.Add(snapshot.contactId);
        reason = "Report received.";
        return true;
    }

    public static bool TryGetWorldHours(out double hours)
    {
        var clock = ServiceRoot.Instance != null ? ServiceRoot.Instance.TimeManager : null;
        if (clock == null) clock = UnityEngine.Object.FindFirstObjectByType<TimeOfDayManager>();
        hours = clock != null ? clock.DayIndex * 24d + clock.CurrentTime : 0;
        return clock != null;
    }

    public static AdjacentNodeIntelSnapshot CaptureManualReport(string sourceId, string observedId, double worldHours)
        => CaptureObservation(sourceId, observedId, worldHours, "Manual test report");

    internal static AdjacentNodeIntelSnapshot CaptureObservation(string sourceId, string observedId, double worldHours, string provenance)
    {
        if (!GameplayAuthority.IsAuthoritative || !ValidTime(worldHours) || !GetNeighborIds(sourceId).Contains(observedId)) return null;
        var state = NodeCivicQuestProvider.FindState(observedId);
        var graph = HarborTravelService.CurrentGraph;
        int index = IndexOf(graph, observedId);
        if (state == null || index < 0) return null;
        var manager = UnityEngine.Object.FindFirstObjectByType<WorldMapEventManager>();
        var events = manager != null ? manager.active : null;
        var snapshot = new AdjacentNodeIntelSnapshot {
            sourceNodeId = sourceId, observedNodeId = observedId, observedName = graph.nodes[index].displayName,
            observedAtWorldHours = worldHours, receivedAtWorldHours = worldHours,
            provenance = provenance, population = state.population,
            report = NodeLocalInformationReport.Build(observedId, graph.nodes[index].displayName, state, index, events)
                .Replace("Current local report", "Last-known report") };
        if (state.Stats != null)
            foreach (var pair in state.Stats) snapshot.stats.Add(new WorldMapNodeStatSaveSnapshot { statId = pair.Key.ToString(), value = pair.Value.value });
        if (state.Flags != null) snapshot.flags.AddRange(state.Flags);
        if (state.ActiveBuffs != null)
            foreach (var effect in state.ActiveBuffs)
                if (effect.buff != null && !effect.IsExpired) snapshot.majorBuffs.Add(effect.buff.displayName);
        if (events != null)
            foreach (var ev in events)
                if (ev.sourceNodeId == index && !ev.isResolved && ev.def != null && ev.def.isVisibleToPlayer) snapshot.majorEvents.Add(ev.def.displayName);
        if (state.ResourcePressures != null)
            foreach (var pair in state.ResourcePressures) snapshot.resourcePressures.Add(new WorldMapNodeResourcePressureSaveSnapshot {
                itemId = pair.Key, value = pair.Value.value, baseline = pair.Value.baseline, driftRate = pair.Value.driftRate });
        snapshot.stats.Sort((a, b) => string.CompareOrdinal(a.statId, b.statId));
        snapshot.resourcePressures.Sort((a, b) => string.CompareOrdinal(a.itemId, b.itemId));
        return snapshot;
    }

    public static string ContactName(string id)
    {
        var graph = HarborTravelService.CurrentGraph;
        int index = IndexOf(graph, id);
        return index >= 0 && !string.IsNullOrWhiteSpace(graph.nodes[index].displayName) ? graph.nodes[index].displayName : "Unnamed settlement";
    }

    private static int IndexOf(MapGraph graph, string id)
    {
        if (graph?.nodes == null || string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < graph.nodes.Count; i++)
            if (graph.nodes[i] != null && WorldMapStableIdUtility.BuildNodeStableId(graph.seed, graph.nodes[i]) == id) return i;
        return -1;
    }
    private static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
}
