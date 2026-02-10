public sealed class RestrictionGateResolver : ITravelResolver
{
    private readonly ITravelRestriction[] _restrictions;

    public RestrictionGateResolver(params ITravelRestriction[] restrictions)
        => _restrictions = restrictions;

    public TravelResult Resolve(TravelRequest req, WorldMapSimContext ctx, WorldMapPlayerState player)
    {
        foreach (var r in _restrictions)
        {
            if (!r.CanTravel(req, ctx, player, out var reason))
                return new TravelResult(false, reason, roll: 0);
        }

        return default; // no decision, allow next resolver (eg. chance roll)
    }
}
