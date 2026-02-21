public sealed class TravelSystem
{
    private readonly ITravelResolver[] _resolvers;

    public TravelSystem(params ITravelResolver[] resolvers) => _resolvers = resolvers;

    public TravelResult TryTravel(TravelRequest req, WorldMapSimContext ctx, WorldMapPlayerState player)
    {
        foreach (var r in _resolvers)
        {
            var result = r.Resolve(req, ctx, player);

            if (result.roll != 0 || result.success || !string.IsNullOrEmpty(result.failureReason))
            {
                if (result.success)
                    player.currentNodeId = req.toNodeId;

                return result;
            }
        }
        return new TravelResult(false, "No resolver produced an outcome", 0);
    }
}
