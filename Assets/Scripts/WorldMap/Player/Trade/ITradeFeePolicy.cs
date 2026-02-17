using UnityEngine;
using WorldMap.Player.Trade;

public interface ITradeFeePolicy
{
    /// <summary>
    /// Returns a fee amount (credits) for this trade line.
    /// Must be >= 0. Caller will apply rounding rules if needed.
    /// </summary>
    int ComputeFeeCredits(
        MapNodeState node,
        TradeLine line,
        int lineTotalCredits,
        int timeBucket
    );
}
