using UnityEngine;
using System.Collections;

public sealed partial class WorldMapKnowledgeSource
{
    [Header("Local Island Charts")]
    [SerializeField] private LocalIslandChartLimits localIslandChartLimits = new();
    public string CurrentNodeId => playerRef?.State?.currentNodeId ?? GameState.I?.player?.currentNodeId;

    private IEnumerator WaitForStarterChartWorld()
    {
        while (isActiveAndEnabled && !_starterCoverageSettled && revealCurrentNodeOnAwake && GameplayAuthority.IsAuthoritative)
        {
            var field = topographySource?.Field ?? WorldMapRuntimeCache.I?.Field;
            var graph = HarborTravelService.CurrentGraph;
            if (field != null && field.IsValid && graph?.nodes != null && graph.seed == field.Seed)
            {
                TryGrantStartingCoverage();
                if (!_starterCoverageSettled && verboseLogging)
                    Debug.LogWarning("[Cartography] Starting-island chart could not be built. Check node/land association and local-chart limits.", this);
                yield break; // Invalid terrain/config is not retried every frame.
            }
            yield return new WaitForSecondsRealtime(.5f);
        }
    }

    public bool TryBuildLocalIslandChart(string nodeId, out CartographicChartState chart,
        out LocalIslandChartResult result, out string reason)
    {
        chart = null; result = null; reason = "Node or world topography is unavailable.";
        AutoWire();
        if (!HarborTravelService.TryGetNode(nodeId, out var node)) return false;
        var field = topographySource?.Field;
        float sea = topographySource != null ? topographySource.EffectiveSeaLevel01 : 0f;
        if (field == null || !field.IsValid)
        {
            var cache = WorldMapRuntimeCache.I;
            if (cache == null || !cache.HasTopography) return false;
            field = cache.Field; sea = cache.EffectiveSeaLevel01;
        }
        var graph = HarborTravelService.CurrentGraph;
        if (graph == null || graph.seed != field.Seed) { reason = "Node graph and topography belong to different worlds."; return false; }
        EnsureInitialized();
        if (State.WorldBounds != field.WorldBounds)
        {
            // Awake may precede topography and provision an empty fallback grid. Correct only that
            // fresh, unacquired state; never resize/re-register a restored or populated crew map.
            var snapshot = CaptureSnapshot();
            bool empty = snapshot.surfaceRevealedCount == 0 && snapshot.underwaterSurveyedCount == 0 &&
                snapshot.knownNodeStableIds.Count == 0 && snapshot.integratedSurfacePoiIds.Count == 0 &&
                snapshot.integratedUnderwaterPoiIds.Count == 0 && snapshot.integratedCartographicSourceIds.Count == 0;
            if (!_starterCoverageSettled && empty && GameplayAuthority.IsAuthoritative)
                State.Initialize(gridWidth, gridHeight, field.WorldBounds);
            else { reason = "Shared map registration does not match current topography."; return false; }
        }
        localIslandChartLimits ??= new LocalIslandChartLimits();
        if (!LocalIslandChartBuilder.TryBuild(field, sea, node.position, localIslandChartLimits, out result, out reason)) return false;
        chart = new CartographicChartState {
            kind = CartographicChartKind.Georeferenced,
            title = $"{node.displayName} — local island chart",
            referenceText = result.UsedFallback ? "Local coastline and offshore waters. Extent limited by the local chart's area/distance budget." :
                "The connected island and its offshore waters. Includes this settlement only; no soundings or unrelated POIs.",
            worldSeed = field.Seed, topographyVersion = field.GenerationVersion, worldBounds = field.WorldBounds,
            payload = new WorldMapCartographicPayload {
                sourceId = $"local-island:v1:{field.Seed}:{field.GenerationVersion}:{nodeId}",
                surface = result.Coverage, nodeIds = new[] { nodeId }
            }
        };
        return true;
    }
}
