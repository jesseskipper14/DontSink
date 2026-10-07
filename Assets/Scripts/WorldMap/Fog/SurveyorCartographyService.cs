using UnityEngine;

/// <summary>Host-side service seam. Station interaction must validate requester/range and supply its own node ID.
/// This is not a client-facing grant API; no physical Surveyor/station content yet.</summary>
public static class SurveyorCartographyService
{
    private static bool issuing;
    public static bool TryIssueLocalChart(GameObject requester, WorldMapKnowledgeSource knowledge,
        string stationNodeId, out string reason)
    {
        reason = null;
        if (!GameplayAuthority.IsAuthoritative) { reason = "Surveyor services require the host."; return false; }
        if (issuing) { reason = "Chart issuance is already in progress."; return false; }
        if (requester == null || knowledge == null) { reason = "Requester/shared map unavailable."; return false; }
        if (!knowledge.TryBuildLocalIslandChart(stationNodeId, out var chart, out _, out reason)) return false;
        if (knowledge.State.HasIntegratedSource(chart.payload.sourceId)) { reason = "This local chart is already integrated."; return false; }
        foreach (var carried in CartographicChartIntegration.Collect(requester))
            if (carried.Item.CartographicChart?.payload?.sourceId == chart.payload.sourceId)
            { reason = "You already carry this local chart."; return false; }
        var inventory = CelestialChartPaperConsumption.ResolveInventory(requester);
        var definition = Resources.Load<ItemDefinition>("Cartography/item_cartographic_chart");
        if (inventory == null || definition == null) { reason = "Chart carrier or inventory unavailable."; return false; }
        if (definition.CanBeBoughtByItemVendors) { reason = "Free local charts require a carrier vendors cannot buy."; return false; }
        var item = ItemInstance.Create(definition); item.SetCartographicChart(chart);
        issuing = true;
        try
        {
            bool issued = inventory.TryAddInstance(item);
            reason = issued ? "Local chart issued. Integrate it at the Mapping Table." : "Make room for one chart first.";
            return issued;
        }
        finally { issuing = false; }
    }

    public static bool TryFixPosition(GameObject requester, string stationNodeId, out string reason)
    {
        reason = null;
        if (!GameplayAuthority.IsAuthoritative) { reason = "Position fixes require the host."; return false; }
        if (requester == null || !HarborTravelService.TryGetNode(stationNodeId, out var node))
        { reason = "Surveyor node/requester unavailable."; return false; }
        // Canonical node coordinates are authoritative, never scene-space NPC/harbor transforms.
        return MapTablePhysicalPieceAuthority.TryApplyPositionFix(requester, node.position, out reason);
    }
}
