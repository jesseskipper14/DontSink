using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Trade/Pressure Market Policy Tuning", fileName = "PressureMarketPolicyTuning")]
public sealed class PressureMarketPolicyTuning : ScriptableObject
{
    [Header("Pressure Range")]
    public float pressureMin = -4f;
    public float pressureMax = 4f;

    [Header("Slots (node capacity, per side)")]
    public int baseNodeSlots = 3;
    public int maxNodeSlots = 18;
    public int slotJitterMaxInclusive = 2;
    public float tradeSlotsScale = 6f;
    public float prosperitySlotsScale = 4f;

    [Header("Unique Slots (additive, per side)")]
    public int uniqueBase = 3;
    public int uniqueMax = 8;
    public float uniqueTradeScale = 5f;
    public float uniqueProsperityScale = 1.5f;
    public int buySideUniquePenalty = 1;

    [Header("Candidate Filtering")]
    [Tooltip("How close to neutral a pressure can be before treated as neutral.")]
    public float neutralDeadzoneSigned = 0.05f;

    [Header("Neutral + Signal Weighting")]
    [Tooltip("All items get at least this baseline weight so neutrals can appear.")]
    public float neutralBaselineWeight = 0.12f;

    [Tooltip("Pressure-derived signal exponent. >1 favors strong pressures.")]
    public float signalExponent = 1.35f;

    [Tooltip("Multiplier for pressure-derived signal weight.")]
    public float signalBoost = 1.35f;

    [Header("Repeat Penalty (phase B only)")]
    [Range(0.05f, 1f)]
    [Tooltip("Each repeat multiplies weight by this factor (0.5 => 2nd 50%, 3rd 25%).")]
    public float repeatPenalty = 0.50f;

    [Header("Cross-side Overlap (buy side phase B)")]
    [Range(0.0f, 1f)]
    public float crossSideOverlapWeightMult = 0.20f; // not 0.02 anymore: too brutal

    [Header("Empty Offers (phase B only)")]
    public float emptyBaseWeightSellToPlayer = 0.01f;
    public float emptyBaseWeightBuyFromPlayer = 0.10f;
    public float emptyWeightFromWeakPool = 0.20f;
    [Range(0f, 1f)] public float emptyCapSellToPlayer = 0.15f;
    [Range(0f, 1f)] public float emptyCapBuyFromPlayer = 0.45f;

    [Header("Cooldown (phase B only)")]
    [Range(0.0f, 1f)]
    public float cooldownMinPenalty = 0.05f;

    [Header("Noise")]
    public float noiseMin = 0.90f;
    public float noiseMax = 1.10f;

    [Header("Price")]
    [Range(0f, 1f)] public float priceSwing = 0.55f;
    [Range(0f, 0.5f)] public float prosperityPriceInfluence = 0.10f;
    public float exoticPriceMult = 1.10f;

    [Header("Quantity")]
    [Range(0f, 1f)] public float tradeQuantityInfluence = 0.35f;
    public float prosperityQuantityInfluence = 0.25f;
    public float baseQtySell = 2f;
    public float magQtySell = 10f;
    public float baseQtyBuy = 1f;
    public float magQtyBuy = 8f;
    public int qtyJitterMaxInclusive = 2;
    public int qtyClampMin = 1;
    public int qtyClampMax = 50;
    [Range(0f, 1f)] public float exoticQtyPenalty = 0.55f;
}
