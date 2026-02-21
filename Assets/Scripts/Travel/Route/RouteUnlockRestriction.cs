public sealed class RouteUnlockRestriction : ITravelRestriction
{
    public bool CanTravel(TravelRequest req, WorldMapSimContext ctx, WorldMapPlayerState player, out string reason)
    {
        // Single source of truth for "known" routes.
        var from = ctx.GetNode(req.fromNodeId);
        var to = ctx.GetNode(req.toNodeId);

        if (RouteAccessPolicy.IsRouteKnown(player, req.fromNodeId, req.toNodeId, from.ClusterId, to.ClusterId))
        {
            reason = string.Empty;
            return true;
        }

        reason = "Route locked. Requires star map.";
        return false;
    }
}
