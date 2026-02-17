using System;
using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

public sealed class PressureMarketPolicy : IMarketPolicy
{
    private readonly INodeStateLookup _nodes;
    private readonly ResourceCatalog _resources;
    private readonly PressureMarketPolicyTuning _t;

    public PressureMarketPolicy(INodeStateLookup nodes, ResourceCatalog resources, PressureMarketPolicyTuning tuning = null)
    {
        _nodes = nodes;
        _resources = resources;
        _t = tuning; // can be null; we’ll fallback to sane defaults
    }

    public void GenerateOffers(string nodeId, int timeBucket, List<NodeMarketOffer> outOffers)
    {
        outOffers.Clear();

        var node = _nodes?.GetNodeState(nodeId);
        if (node == null) return;

        if (_resources == null || _resources.Resources == null || _resources.Resources.Count == 0)
            return;

        var rng = new System.Random(StableHash(nodeId) ^ (timeBucket * 7919));

        float trade01 = GetStat01(node, NodeStatId.TradeRating);
        float pros01 = GetStat01(node, NodeStatId.Prosperity);

        // Additive budgets (your request)
        int nodeSlotsSell = ComputeNodeSlots(MarketOfferKind.SellToPlayer, trade01, pros01, rng);
        int nodeSlotsBuy = ComputeNodeSlots(MarketOfferKind.BuyFromPlayer, trade01, pros01, rng);

        int uniqueSlotsSell = ComputeUniqueSlots(MarketOfferKind.SellToPlayer, trade01, pros01);
        int uniqueSlotsBuy = ComputeUniqueSlots(MarketOfferKind.BuyFromPlayer, trade01, pros01);

        // Candidate pools:
        // IMPORTANT: neutrals are allowed. Pressures affect WEIGHT, not inclusion.
        var sellCandidates = new List<Candidate>(_resources.Resources.Count);
        var buyCandidates = new List<Candidate>(_resources.Resources.Count);

        float pMin = T().pressureMin;
        float pMax = T().pressureMax;

        for (int i = 0; i < _resources.Resources.Count; i++)
        {
            var def = _resources.Resources[i];
            if (def == null || string.IsNullOrWhiteSpace(def.itemId)) continue;

            float pRaw = node.GetPressure(def.itemId);
            float pClamped = Mathf.Clamp(pRaw, pMin, pMax);

            float pn = Mathf.InverseLerp(pMin, pMax, pClamped);  // 0..1
            float signed = Mathf.Lerp(-1f, 1f, pn);              // -1..+1

            // SellToPlayer: surplus (positive) makes it MORE likely, but neutral still possible.
            float sellW = CandidateWeight(def, signed, trade01, pros01, wantExotic: pros01 > 0.6f, forSellSide: true);
            if (sellW > 0.0001f) sellCandidates.Add(new Candidate(def, signed, sellW));

            // BuyFromPlayer: shortage (negative) makes it MORE likely, but neutral still possible.
            float buyW = CandidateWeight(def, signed, trade01, pros01, wantExotic: pros01 > 0.6f, forSellSide: false);
            if (buyW > 0.0001f) buyCandidates.Add(new Candidate(def, signed, buyW));
        }

        // Cross-side overlap tracking (we’ll only apply overlap penalty in Phase B, buy side)
        var sellSideItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // =========================
        // PHASE A: Guaranteed uniques
        // No empty offers, no cooldown penalty, no overlap penalties, no repeat penalties.
        // This is the “never gated” rule.
        // =========================
        EmitGuaranteedUnique(
            rng, nodeId, timeBucket,
            MarketOfferKind.SellToPlayer,
            uniqueSlotsSell,
            trade01, pros01,
            node,
            sellCandidates,
            sellSideItems,
            outOffers);

        EmitGuaranteedUnique(
            rng, nodeId, timeBucket,
            MarketOfferKind.BuyFromPlayer,
            uniqueSlotsBuy,
            trade01, pros01,
            node,
            buyCandidates,
            sellSideItems, // track overlap for phase B
            outOffers);

        // =========================
        // PHASE B: Fill remaining node slots
        // Here: empty, repeats, cooldown, overlap penalty can fight it out.
        // =========================
        EmitFillWithWeights(
            rng, nodeId, timeBucket,
            MarketOfferKind.SellToPlayer,
            nodeSlotsSell,
            trade01, pros01,
            node,
            sellCandidates,
            usedCrossSide: sellSideItems,
            enforceCrossSidePenalty: false,
            outOffers);

        EmitFillWithWeights(
            rng, nodeId, timeBucket,
            MarketOfferKind.BuyFromPlayer,
            nodeSlotsBuy,
            trade01, pros01,
            node,
            buyCandidates,
            usedCrossSide: sellSideItems,
            enforceCrossSidePenalty: true,
            outOffers);
    }

    // =========================================
    // Phase A: guaranteed unique items
    // =========================================

    private void EmitGuaranteedUnique(
        System.Random rng,
        string nodeId,
        int timeBucket,
        MarketOfferKind kind,
        int uniqueSlots,
        float trade01,
        float pros01,
        MapNodeState nodeState,
        List<Candidate> candidates,
        HashSet<string> crossSideUsed,
        List<NodeMarketOffer> outOffers)
    {
        if (uniqueSlots <= 0) return;
        if (candidates == null || candidates.Count == 0) return;

        var chosen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // We pick unique slots by weight, but we do NOT allow “empty”.
        // Also: we do NOT exclude neutrals.
        for (int s = 0; s < uniqueSlots; s++)
        {
            var picked = PickWeightedUnique_NoEmpty_NoPenalties(rng, candidates, chosen);
            if (!picked.hasValue) break;

            var c = picked.value;
            if (c.def == null) continue;

            string itemId = c.def.itemId;
            chosen.Add(itemId);

            // track sell-side items for overlap purposes (just for informational use later)
            if (kind == MarketOfferKind.SellToPlayer)
                crossSideUsed.Add(itemId);

            int unitPrice = ComputePrice(c.def, c.signed, kind, pros01);
            int qty = ComputeQuantity(c.def, c.signed, kind, trade01, pros01, rng);
            if (qty <= 0) continue;

            string offerId = $"{nodeId}:{timeBucket}:{(kind == MarketOfferKind.SellToPlayer ? "US" : "UB")}:{itemId}:{s}";

            outOffers.Add(new NodeMarketOffer
            {
                offerId = offerId,
                itemId = itemId,
                unitPrice = unitPrice,
                quantityRemaining = qty,
                kind = kind
            });
        }
    }

    private (bool hasValue, Candidate value) PickWeightedUnique_NoEmpty_NoPenalties(
        System.Random rng,
        List<Candidate> candidates,
        HashSet<string> chosen)
    {
        float total = 0f;

        // Build total excluding already chosen
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c.def == null) continue;
            if (chosen.Contains(c.def.itemId)) continue;

            total += Mathf.Max(0.0001f, c.weight);
        }

        if (total <= 0.0001f) return (false, default);

        float roll = (float)(rng.NextDouble() * total);

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c.def == null) continue;
            if (chosen.Contains(c.def.itemId)) continue;

            roll -= Mathf.Max(0.0001f, c.weight);
            if (roll <= 0f)
                return (true, c);
        }

        // fallback
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            var c = candidates[i];
            if (c.def == null) continue;
            if (!chosen.Contains(c.def.itemId))
                return (true, c);
        }

        return (false, default);
    }

    // =========================================
    // Phase B: weighted fill with repeats/empty/cooldown/overlap
    // =========================================

    private void EmitFillWithWeights(
        System.Random rng,
        string nodeId,
        int timeBucket,
        MarketOfferKind kind,
        int slots,
        float trade01,
        float pros01,
        MapNodeState nodeState,
        List<Candidate> candidates,
        HashSet<string> usedCrossSide,
        bool enforceCrossSidePenalty,
        List<NodeMarketOffer> outOffers)
    {
        if (slots <= 0) return;
        if (candidates == null || candidates.Count == 0) return;

        // Track repeats on this side (phase B only)
        var repeatCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Pool strength for empty weighting
        float poolStrength = SumWeights(candidates);

        for (int s = 0; s < slots; s++)
        {
            var pick = PickWithEmptyAndPenalties(
                rng,
                kind,
                timeBucket,
                nodeState,
                candidates,
                usedCrossSide,
                enforceCrossSidePenalty,
                repeatCount,
                poolStrength);

            if (pick.isEmpty)
                continue;

            var c = pick.candidate;
            if (c.def == null) continue;

            string itemId = c.def.itemId;

            // increment repeat count
            repeatCount.TryGetValue(itemId, out int rc);
            repeatCount[itemId] = rc + 1;

            // record cross-side usage (sell side sets the list)
            if (!enforceCrossSidePenalty && kind == MarketOfferKind.SellToPlayer)
                usedCrossSide.Add(itemId);

            int unitPrice = ComputePrice(c.def, c.signed, kind, pros01);
            int qty = ComputeQuantity(c.def, c.signed, kind, trade01, pros01, rng);
            if (qty <= 0) continue;

            string offerId = $"{nodeId}:{timeBucket}:{(kind == MarketOfferKind.SellToPlayer ? "S" : "B")}:{itemId}:{s}";

            outOffers.Add(new NodeMarketOffer
            {
                offerId = offerId,
                itemId = itemId,
                unitPrice = unitPrice,
                quantityRemaining = qty,
                kind = kind
            });
        }
    }

    private PickResult PickWithEmptyAndPenalties(
        System.Random rng,
        MarketOfferKind kind,
        int timeBucket,
        MapNodeState nodeState,
        List<Candidate> candidates,
        HashSet<string> usedCrossSide,
        bool enforceCrossSidePenalty,
        Dictionary<string, int> repeatCount,
        float poolStrength)
    {
        var t = T();

        float emptyW = kind == MarketOfferKind.SellToPlayer
            ? t.emptyBaseWeightSellToPlayer
            : t.emptyBaseWeightBuyFromPlayer;

        // pool weakness -> more empties (but capped)
        float weak01 = poolStrength <= 0.001f ? 1f : Mathf.Clamp01(1f - (poolStrength / 6f));
        emptyW += weak01 * t.emptyWeightFromWeakPool;

        float emptyCap = kind == MarketOfferKind.SellToPlayer ? t.emptyCapSellToPlayer : t.emptyCapBuyFromPlayer;
        emptyW = Mathf.Min(emptyW, emptyCap);

        float total = emptyW;

        var tmp = new List<(Candidate c, float w)>(candidates.Count);

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c.def == null) continue;

            string itemId = c.def.itemId;

            float w = Mathf.Max(0.0001f, c.weight);

            // cross-side overlap penalty (buy side)
            if (enforceCrossSidePenalty && usedCrossSide != null && usedCrossSide.Contains(itemId))
                w *= Mathf.Clamp01(t.crossSideOverlapWeightMult);

            // cooldown penalty (weight, not exclusion)
            w *= GetCooldownPenalty01(nodeState, itemId, timeBucket);

            // repeat penalty (phase B only)
            if (repeatCount != null && repeatCount.TryGetValue(itemId, out int repeats) && repeats > 0)
            {
                // repeats=1 => 2nd time (penalty once), repeats=2 => 3rd time (penalty twice), etc.
                float rp = Mathf.Pow(Mathf.Clamp01(t.repeatPenalty), repeats);
                w *= rp;
            }

            // deterministic-ish noise
            float noise = Mathf.Lerp(t.noiseMin, t.noiseMax, (float)rng.NextDouble());
            w *= noise;

            if (w <= 0.0001f) continue;

            tmp.Add((c, w));
            total += w;
        }

        if (total <= 0.0001f)
            return new PickResult(isEmpty: true, candidate: default);

        float roll = (float)(rng.NextDouble() * total);

        roll -= emptyW;
        if (roll <= 0f)
            return new PickResult(isEmpty: true, candidate: default);

        for (int i = 0; i < tmp.Count; i++)
        {
            roll -= tmp[i].w;
            if (roll <= 0f)
                return new PickResult(isEmpty: false, candidate: tmp[i].c);
        }

        return tmp.Count > 0
            ? new PickResult(isEmpty: false, candidate: tmp[tmp.Count - 1].c)
            : new PickResult(isEmpty: true, candidate: default);
    }

    // =========================================
    // Tuned computations
    // =========================================

    private int ComputeNodeSlots(MarketOfferKind kind, float trade01, float prosperity01, System.Random rng)
    {
        var t = T();

        float slotsF = t.baseNodeSlots
                     + trade01 * t.tradeSlotsScale
                     + prosperity01 * t.prosperitySlotsScale;

        int jitter = rng.Next(0, Mathf.Max(0, t.slotJitterMaxInclusive) + 1);
        int slots = Mathf.Clamp(Mathf.FloorToInt(slotsF) + jitter, 0, Mathf.Max(0, t.maxNodeSlots));
        return slots;
    }

    private int ComputeUniqueSlots(MarketOfferKind kind, float trade01, float pros01)
    {
        var t = T();

        float f = t.uniqueBase
                + trade01 * t.uniqueTradeScale
                + pros01 * t.uniqueProsperityScale;

        int u = Mathf.RoundToInt(f);

        if (kind == MarketOfferKind.BuyFromPlayer)
            u -= Mathf.Max(0, t.buySideUniquePenalty);

        u = Mathf.Clamp(u, 0, Mathf.Max(0, t.uniqueMax));
        return u;
    }

    private int ComputePrice(ResourceDef def, float signedPressure, MarketOfferKind kind, float prosperity01)
    {
        var t = T();

        float p = Mathf.Clamp(signedPressure, -1f, 1f);

        float pressureMult = (kind == MarketOfferKind.SellToPlayer)
            ? (1f - (p * t.priceSwing))
            : (1f + (-p * t.priceSwing));

        float prosMult = 1f + (prosperity01 - 0.5f) * 2f * t.prosperityPriceInfluence;
        float exoticMult = def.isExotic ? t.exoticPriceMult : 1.00f;

        float raw = def.basePrice * pressureMult * prosMult * exoticMult;
        return Mathf.Max(1, Mathf.RoundToInt(raw));
    }

    private int ComputeQuantity(ResourceDef def, float signedPressure, MarketOfferKind kind, float trade01, float prosperity01, System.Random rng)
    {
        var t = T();

        float mag = Mathf.Clamp01(Mathf.Abs(signedPressure));
        float activity = 0.5f + trade01 * t.tradeQuantityInfluence + prosperity01 * t.prosperityQuantityInfluence;

        float exoticPenalty = def.isExotic ? t.exoticQtyPenalty : 1f;

        float baseQtyF = kind == MarketOfferKind.SellToPlayer
            ? (t.baseQtySell + mag * t.magQtySell)
            : (t.baseQtyBuy + mag * t.magQtyBuy);

        baseQtyF *= activity * exoticPenalty;

        int jitter = rng.Next(0, Mathf.Max(0, t.qtyJitterMaxInclusive) + 1);
        int qty = Mathf.Clamp(Mathf.FloorToInt(baseQtyF) + jitter, t.qtyClampMin, t.qtyClampMax);

        return qty;
    }

    private float CandidateWeight(ResourceDef def, float signedPressure, float trade01, float prosperity01, bool wantExotic, bool forSellSide)
    {
        var t = T();

        // signedPressure is -1..+1. We convert to “signal strength” for the side.
        // Sell side likes positive. Buy side likes negative.
        float signal = forSellSide ? signedPressure : -signedPressure; // now + means “good for this side”
        signal = Mathf.Clamp(signal, -1f, 1f);

        // deadzone makes mild values behave like “neutral”
        if (Mathf.Abs(signal) < t.neutralDeadzoneSigned)
            signal = 0f;

        // pressure contribution (0..1)
        float signal01 = Mathf.Clamp01((signal + 1f) * 0.5f);
        // We want “good” signal to matter, but neutrals still exist.
        float good01 = signal > 0f ? signal01 : 0f; // only shortages/surpluses boost

        float pressureTerm = Mathf.Pow(good01, Mathf.Max(0.01f, t.signalExponent)) * t.signalBoost;

        // baseline ensures neutrals can spawn
        float w = t.neutralBaselineWeight + pressureTerm;

        // Trade slightly increases activity/variety
        w *= 0.85f + trade01 * 0.30f;

        // Exotics: prosperity bias
        if (def.isExotic)
            w *= wantExotic ? (1.10f + prosperity01 * 0.60f) : 0.75f;

        w *= 1.0f + def.volatility01 * 0.25f;

        return Mathf.Max(0.0001f, w);
    }

    // =========================================
    // Cooldown hook
    // =========================================

    private float GetCooldownPenalty01(MapNodeState node, string itemId, int timeBucket)
    {
        if (node == null || string.IsNullOrWhiteSpace(itemId))
            return 1f;

        if (node is INodeItemTradeMemory mem && mem.TryGetLastTradeBucket(itemId, out int lastBucket))
        {
            int dt = Mathf.Max(0, timeBucket - lastBucket);
            int cooldownBuckets = Mathf.Max(1, mem.GetCooldownBuckets(itemId));

            float t01 = Mathf.Clamp01(dt / (float)cooldownBuckets);
            return Mathf.Lerp(T().cooldownMinPenalty, 1f, t01);
        }

        return 1f;
    }

    public interface INodeItemTradeMemory
    {
        bool TryGetLastTradeBucket(string itemId, out int lastBucket);
        int GetCooldownBuckets(string itemId);
    }

    // =========================================
    // Helpers
    // =========================================

    private static float SumWeights(List<Candidate> items)
    {
        float t = 0f;
        for (int i = 0; i < items.Count; i++)
            t += Mathf.Max(0f, items[i].weight);
        return t;
    }

    private float GetStat01(MapNodeState node, NodeStatId id)
    {
        if (node == null) return 0.5f;
        if (!node.TryGetStat(id, out var s)) return 0.5f;
        return Mathf.Clamp01(s.value / 4f);
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int hash = 23;
            for (int i = 0; i < s.Length; i++)
                hash = hash * 31 + s[i];
            return hash;
        }
    }

    private PressureMarketPolicyTuning T()
    {
        // Fallback defaults if you didn’t assign an asset yet.
        if (_t != null) return _t;

        // Minimal inline defaults matching your old behavior broadly.
        // (Yes, it’s still numbers, but they’re confined here and replaced once asset exists.)
        return _fallback ??= ScriptableObject.CreateInstance<PressureMarketPolicyTuning>();
    }

    private static PressureMarketPolicyTuning _fallback;

    private readonly struct Candidate
    {
        public readonly ResourceDef def;
        public readonly float signed;  // -1..+1 (world)
        public readonly float weight;

        public Candidate(ResourceDef def, float signed, float weight)
        {
            this.def = def;
            this.signed = signed;
            this.weight = weight;
        }
    }

    private readonly struct PickResult
    {
        public readonly bool isEmpty;
        public readonly Candidate candidate;

        public PickResult(bool isEmpty, Candidate candidate)
        {
            this.isEmpty = isEmpty;
            this.candidate = candidate;
        }
    }
}
