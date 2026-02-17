using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

public sealed class TradeFeePreview : ITradeFeePreview
{
    private readonly float _baseFeeRate;
    private readonly float _tradeRatingFeeBoost;
    private readonly int _minFeePerLine;
    private readonly float _worldUnhealth01;
    private readonly float _worldUnhealthFeeBoost;

    public TradeFeePreview(
        float baseFeeRate,
        float tradeRatingFeeBoost,
        int minFeePerLine,
        float worldUnhealth01,
        float worldUnhealthFeeBoost)
    {
        _baseFeeRate = baseFeeRate;
        _tradeRatingFeeBoost = tradeRatingFeeBoost;
        _minFeePerLine = minFeePerLine;
        _worldUnhealth01 = worldUnhealthAvoidNaN(worldUnhealth01);
        _worldUnhealthFeeBoost = worldUnhealthFeeBoost;
    }

    public int ComputeFeeTotal(MapNodeState nodeState, IReadOnlyList<TradeLine> lines, int timeBucket)
    {
        if (nodeState == null || lines == null || lines.Count == 0) return 0;

        float trade01 = 0f;
        if (nodeState.TryGetStat(NodeStatId.TradeRating, out var trade))
            trade01 = Mathf.Clamp01(trade.value / 4f);

        float rate = _baseFeeRate
                     + trade01 * _tradeRatingFeeBoost
                     + Mathf.Clamp01(_worldUnhealth01) * _worldUnhealthFeeBoost;

        rate = Mathf.Clamp(rate, 0f, 0.50f);

        int totalFee = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.quantity <= 0 || line.unitPrice <= 0) continue;

            int lineTotal = line.unitPrice * line.quantity;

            // Round UP so 1-at-a-time doesn’t dodge fees.
            int fee = Mathf.CeilToInt(lineTotal * rate);

            // Min fee per line when a line is present.
            fee = Mathf.Max(_minFeePerLine, fee);

            totalFee += fee;
        }

        return Mathf.Max(0, totalFee);
    }

    public string DebugDescribe(MapNodeState nodeState)
    {
        float trade01 = 0f;
        if (nodeState != null && nodeState.TryGetStat(NodeStatId.TradeRating, out var trade))
            trade01 = Mathf.Clamp01(trade.value / 4f);

        float rate = _baseFeeRate
                     + trade01 * _tradeRatingFeeBoost
                     + Mathf.Clamp01(_worldUnhealth01) * _worldUnhealthFeeBoost;

        rate = Mathf.Clamp(rate, 0f, 0.50f);

        return $"feeRate={rate:0.###} minPerLine={_minFeePerLine}";
    }

    private static float worldUnhealthAvoidNaN(float v) => float.IsNaN(v) ? 0f : v;
}
