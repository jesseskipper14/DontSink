using System.Linq;

public sealed class ClusterUnlockRestriction : ITravelRestriction
{
    public bool CanTravel(TravelRequest req, WorldMapSimContext ctx, WorldMapPlayerState player, out string reason)
    {
        var from = ctx.GetNode(req.fromNodeId);
        var to = ctx.GetNode(req.toNodeId);

        if (from.ClusterId == to.ClusterId)
        {
            reason = "";
            return true;
        }

        // simplest: require route unlock OR cluster unlock
        if (player.unlockedClusters.Contains(to.ClusterId))
        {
            reason = "";
            return true;
        }

        reason = "New cluster locked. Need star map route unlock.";
        return false;
    }
}
