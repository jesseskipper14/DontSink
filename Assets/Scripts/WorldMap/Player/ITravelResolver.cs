public interface ITravelResolver
{
    TravelResult Resolve(TravelRequest req, WorldMapSimContext ctx, WorldMapPlayerState player);
}
