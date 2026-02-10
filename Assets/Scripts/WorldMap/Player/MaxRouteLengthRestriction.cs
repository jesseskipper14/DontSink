public sealed class MaxRouteLengthRestriction : ITravelRestriction
{
    private readonly float _max;
    public MaxRouteLengthRestriction(float max) => _max = max;

    public bool CanTravel(TravelRequest req, WorldMapSimContext ctx, WorldMapPlayerState player, out string reason)
    {
        if (req.routeLength <= _max)
        {
            reason = "";
            return true;
        }
        reason = $"Route too long (max {_max}, got {req.routeLength})";
        return false;
    }
}
