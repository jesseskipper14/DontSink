using UnityEngine;
using WorldMap.Player.Trade;

public sealed class NodeScaledTradeFeePolicy : ITradeFeePolicy
{
    // Tunables
    private readonly float _baseFeeRate;            // e.g. 0.03f
    private readonly float _tradeRatingFeeBoost;    // e.g. 0.04f additional at Trade=4
    private readonly float _worldUnhealthFeeBoost;  // placeholder
    private readonly float _worldUnhealth01;        // 0..1 placeholder (0 healthy, 1 bad)
    private readonly int _minFeePerLine;            // e.g. 1

    public NodeScaledTradeFeePolicy(
        float baseFeeRate = 0.03f,
        float tradeRatingFeeBoost = 0.02f,
        int minFeePerLine = 1,
        float worldUnhealth01 = 0f,
        float worldUnhealthFeeBoost = 0.03f)
    {
        _baseFeeRate = Mathf.Max(0f, baseFeeRate);
        _tradeRatingFeeBoost = Mathf.Max(0f, tradeRatingFeeBoost);
        _minFeePerLine = Mathf.Max(0, minFeePerLine);

        _worldUnhealth01 = Mathf.Clamp01(worldUnhealth01);
        _worldUnhealthFeeBoost = Mathf.Max(0f, worldUnhealthFeeBoost);
    }

    public int ComputeFeeCredits(MapNodeState node, TradeLine line, int lineTotalCredits, int timeBucket)
    {
        if (node == null || line == null) return 0;
        if (lineTotalCredits <= 0) return 0;

        float trade01 = 0.5f;
        if (node.TryGetStat(NodeStatId.TradeRating, out var tradeStat))
            trade01 = Mathf.Clamp01(tradeStat.value / 4f);

        // Fee rate rises with TradeRating and with world unhealth (placeholder)
        float feeRate =
            _baseFeeRate +
            trade01 * _tradeRatingFeeBoost +
            _worldUnhealth01 * _worldUnhealthFeeBoost;

        feeRate = Mathf.Clamp(feeRate, 0f, 0.40f); // safety cap: 40% max, you monster

        // Round UP so micro-trades can't dodge
        int fee = Mathf.CeilToInt(lineTotalCredits * feeRate);

        // Minimum fee prevents 0-fee trades
        if (_minFeePerLine > 0)
            fee = Mathf.Max(_minFeePerLine, fee);

        return fee;
    }
}
