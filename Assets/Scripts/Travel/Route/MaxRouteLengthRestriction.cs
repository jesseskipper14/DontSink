public sealed class MaxRouteLengthRestriction : ITravelRestriction
{
    // Compatibility shim for existing callers. Travel no longer has a distance cap.
    public MaxRouteLengthRestriction(float max) { }

    public bool CanTravel(TravelRequest req, WorldMapSimContext ctx, WorldMapPlayerState player, out string reason)
    {
        reason = "";
        return true;
    }
}
