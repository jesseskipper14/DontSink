using System.Collections.Generic;
using UnityEngine;

public sealed partial class WorldMapKnowledgeState
{
    [SerializeField] private List<string> integratedNodes = new();
    [SerializeField] private List<string> integratedSurfacePois = new();
    [SerializeField] private List<string> integratedUnderwaterPois = new();
    [SerializeField] private List<string> integratedSources = new();
    public bool HasNodeMarker(string id) => !string.IsNullOrWhiteSpace(id) && integratedNodes.Contains(id);
    public bool HasSurfacePoiMarker(string id) => !string.IsNullOrWhiteSpace(id) && integratedSurfacePois.Contains(id);
    public bool HasUnderwaterPoiMarker(string id) => !string.IsNullOrWhiteSpace(id) && integratedUnderwaterPois.Contains(id);
    public bool HasIntegratedSource(string id) => !string.IsNullOrWhiteSpace(id) && integratedSources.Contains(id);
    public bool CanDisplayBathymetry(Vector2 p) => IsRevealed(WorldMapKnowledgeLayer.Surface, p) && IsRevealed(WorldMapKnowledgeLayer.UnderwaterSurvey, p);
    public bool CanDisplayPoi(string id, Vector2 p, bool underwater) => underwater
        ? HasUnderwaterPoiMarker(id) && IsRevealed(WorldMapKnowledgeLayer.Surface, p) : HasSurfacePoiMarker(id);

    public bool TryApplyPayload(WorldMapCartographicPayload payload, out string reason, int maximumUnknownComponentCells = 0)
    {
        reason = null;
        if (!IsValid || payload == null || string.IsNullOrWhiteSpace(payload.sourceId)) { reason = "Missing cartographic state or source ID."; return false; }
        if (HasIntegratedSource(payload.sourceId)) { reason = "Source already integrated."; return false; }
        if (!ValidMask(payload.surface) || !ValidMask(payload.bathymetry) || !ValidIds(payload.nodeIds) || !ValidIds(payload.surfacePoiIds) || !ValidIds(payload.underwaterPoiIds))
        { reason = "Invalid payload mask, world registration or marker ID."; return false; }
        if (payload.surface == null && payload.bathymetry == null && Empty(payload.nodeIds) && Empty(payload.surfacePoiIds) && Empty(payload.underwaterPoiIds))
        { reason = "Source contains no cartographic data."; return false; }
        // All validation precedes mutation. These inputs are trusted host-side data, not client grants.
        UnionMask(WorldMapKnowledgeLayer.Surface, payload.surface, out bool surfaceChanged);
        if (surfaceChanged) CleanupTinySurfaceGaps(maximumUnknownComponentCells);
        UnionMask(WorldMapKnowledgeLayer.UnderwaterSurvey, payload.bathymetry);
        AddIds(integratedNodes, payload.nodeIds); AddIds(integratedSurfacePois, payload.surfacePoiIds); AddIds(integratedUnderwaterPois, payload.underwaterPoiIds);
        integratedSources.Add(payload.sourceId);
        Revision++;
        return true;
    }
    public bool UnionMask(WorldMapKnowledgeLayer layer, WorldMapCoverageMask mask)
        => UnionMask(layer, mask, out _);

    private bool UnionMask(WorldMapKnowledgeLayer layer, WorldMapCoverageMask mask, out bool changed)
    {
        changed = false;
        if (!IsValid || mask == null || !ValidMask(mask) ||
            (layer != WorldMapKnowledgeLayer.Surface && layer != WorldMapKnowledgeLayer.UnderwaterSurvey)) return false;
        Revision++;
        var bits = GetBits(layer);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            if (mask.Contains(CellCenterWorld(x, y)) && !bits[Index(x, y)])
            { bits[Index(x, y)] = true; changed = true; }
        return true;
    }
    private bool ValidMask(WorldMapCoverageMask mask) => mask == null || mask.IsValid && mask.worldBounds == worldBounds;
    private static bool Empty(string[] ids) => ids == null || ids.Length == 0;
    private static bool ValidIds(string[] ids) { if (ids != null) foreach (var id in ids) if (string.IsNullOrWhiteSpace(id)) return false; return true; }
    private static void AddIds(List<string> target, IEnumerable<string> ids) { if (ids != null) foreach (var id in ids) if (!string.IsNullOrWhiteSpace(id) && !target.Contains(id)) target.Add(id); }
    private void ResetIntegratedRecords() { integratedNodes = new(); integratedSurfacePois = new(); integratedUnderwaterPois = new(); integratedSources = new(); }
    private void CopyIntegratedRecords(WorldMapKnowledgeSaveSnapshot s)
    {
        s.knownNodeStableIds = new(integratedNodes); s.integratedSurfacePoiIds = new(integratedSurfacePois);
        s.integratedUnderwaterPoiIds = new(integratedUnderwaterPois); s.integratedCartographicSourceIds = new(integratedSources);
    }
    private void RestoreIntegratedRecords(WorldMapKnowledgeSaveSnapshot s)
    {
        AddIds(integratedNodes, s.knownNodeStableIds); AddIds(integratedSurfacePois, s.integratedSurfacePoiIds);
        AddIds(integratedUnderwaterPois, s.integratedUnderwaterPoiIds); AddIds(integratedSources, s.integratedCartographicSourceIds);
    }
}
