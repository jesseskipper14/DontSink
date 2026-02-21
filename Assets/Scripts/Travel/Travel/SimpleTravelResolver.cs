using UnityEngine;

public sealed class SimpleTravelResolver : ITravelResolver
{
    public TravelResult Resolve(
        TravelRequest req,
        WorldMapSimContext ctx,
        WorldMapPlayerState player)
    {
        float chance = 0.85f;

        // route length penalty (unchanged)
        chance -= Mathf.Clamp01(req.routeLength / 10f) * 0.25f;

        var from = ctx.GetNode(req.fromNodeId);

        // READ FROM STATE, NOT PROPERTIES
        float prosperity = from.State.GetStat(NodeStatId.Prosperity).value;
        float stability = from.State.GetStat(NodeStatId.Stability).value;

        // assuming ratings are 0..4
        chance += (prosperity / 4f) * 0.10f;
        chance += (stability / 4f) * 0.05f;

        chance = Mathf.Clamp01(chance);

        var rng = new System.Random(req.seed);
        int roll = rng.Next(0, 10000);

        bool success = roll < (int)(chance * 10000f);

        return success
            ? new TravelResult(true, "", roll)
            : new TravelResult(false, "Travel failed", roll);
    }
}
