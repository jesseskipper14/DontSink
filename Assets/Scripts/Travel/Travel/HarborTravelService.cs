using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Reconstructible harbor geometry over the current world. No discovery mutations.</summary>
public static class HarborTravelService
{
    private static WorldMapTopographyField _field;
    private static float _sea;
    private static HarborGeometryQuery _query;
    private static readonly Dictionary<MapNode, HarborDefinition> Definitions = new();
    private static WorldMapGraphSaveSnapshot _savedGraph;
    private static MapGraph _restoredGraph;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    { _field = null; _query = null; Definitions.Clear(); _savedGraph = null; _restoredGraph = null; }

    public static MapGraph CurrentGraph
    {
        get
        {
            var generator = UnityEngine.Object.FindAnyObjectByType<WorldMapGraphGenerator>();
            if (generator != null && generator.graph != null) return generator.graph;
            var saved = GameState.I != null && GameState.I.worldMapSnapshot != null ? GameState.I.worldMapSnapshot.graph : null;
            if (!ReferenceEquals(saved, _savedGraph))
            { _savedGraph = saved; _restoredGraph = saved != null ? WorldMapSaveRestorer.RestoreGraph(saved) : null; }
            return _restoredGraph;
        }
    }
    public static bool TryGetNode(string stableId, out MapNode node)
    {
        node = null; var graph = CurrentGraph;
        if (graph == null || string.IsNullOrEmpty(stableId)) return false;
        foreach (var candidate in graph.nodes)
            if (candidate != null && WorldMapStableIdUtility.BuildNodeStableId(graph.seed, candidate) == stableId)
            { node = candidate; return true; }
        return false;
    }
    public static bool TryDefinition(MapNode node, out HarborDefinition harbor, out string reason)
    {
        harbor = default; reason = "Harbor topography is unavailable.";
        var cache = WorldMapRuntimeCache.I;
        if (node == null || cache == null || !cache.HasTopography) return false;
        if (!ReferenceEquals(cache.Field, _field) || _sea != cache.EffectiveSeaLevel01)
        {
            _field = cache.Field; _sea = cache.EffectiveSeaLevel01; _query = null; Definitions.Clear();
            try { _query = new HarborGeometryQuery(_field, _sea); }
            catch (ArgumentException error) { reason = error.Message; return false; }
        }
        if (_query == null) return false;
        if (!Definitions.TryGetValue(node, out harbor) || harbor.Position != new WorldTopology(_field.WorldBounds).Normalize(node.position))
        {
            if (!_query.TrySolve(node.position, out harbor))
            { reason = $"No continuous waterward corridor at harbor '{node.displayName}'."; return false; }
            Definitions[node] = harbor;
        }
        node.harbor = harbor;
        reason = null; return true;
    }
    public static bool TryGeometry(MapNode node, Boat boat, float scale, HarborTravelSettings settings,
        out HarborBerth berth, out string reason)
    {
        berth = default;
        if (settings == null || settings.terrainProfile == null)
        { reason = "Assign Harbor Settings / Terrain Profile on SceneTransitionController."; return false; }
        if (boat == null || boat.rb == null)
        { reason = "Current boat bounds are unavailable."; return false; }
        if (!TryDefinition(node, out var harbor, out reason)) return false;
        BoatSize(boat, settings.terrainProfile.waterLevelY, out float length, out float draft);
        return _query.TryScale(harbor, length, draft, scale, settings, out berth, out reason);
    }
    public static void BoatSize(Boat boat, float waterY, out float length, out float draft)
    {
        length = Mathf.Max(1, boat.Width); draft = Mathf.Max(1, boat.Height * .5f);
        bool found = false; Bounds bounds = default;
        int hull = LayerMask.NameToLayer("Hull");
        foreach (var collider in boat.GetComponentsInChildren<Collider2D>())
            if (collider.enabled && !collider.isTrigger && collider.attachedRigidbody == boat.rb && collider.gameObject.layer == hull)
            { if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds); }
        if (found) { length = Mathf.Max(length, bounds.size.x); draft = Mathf.Max(draft, waterY - bounds.min.y); }
    }
}
