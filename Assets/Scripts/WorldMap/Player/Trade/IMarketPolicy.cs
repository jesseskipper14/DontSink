using System.Collections.Generic;

namespace WorldMap.Player.Trade
{
    /// <summary>
    /// Node-owned market policy: generates offers based on node state + world conditions.
    /// This is authoritative, not player-specific storage.
    /// </summary>
    public interface IMarketPolicy
    {
        void GenerateOffers(
            string nodeId,
            int timeBucket,
            List<NodeMarketOffer> outOffers
        );
    }
}
