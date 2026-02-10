public interface ITravelRestriction
{
    bool CanTravel(TravelRequest req, WorldMapSimContext ctx, WorldMapPlayerState player, out string reason);
}
