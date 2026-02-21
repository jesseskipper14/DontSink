using System.Collections.Generic;
using UnityEngine;
using MiniGames;
using WorldMap.Player.Trade;

public sealed class TradeWorldMapRunner : MonoBehaviour
{
    [Header("Refs")]
    public MiniGameOverlayHost overlay;
    public WorldMapPlayerRef playerRef;
    public TimeOfDayManager timeOfDay;

    [Tooltip("Runtime binder (node authoritative runtime registry).")]
    public WorldMapRuntimeBinder binder;

    [Tooltip("Resource catalog used by PressureMarketPolicy.")]
    public ResourceCatalog resourceCatalog;

    private MarketService _market;
    private IMarketPolicy _policy;
    private ITradeFeePolicy _feePolicy;
    public PressureMarketPolicyTuning marketTuning;

    private bool _subscribed;

    [Header("Item Memory (bucket units)")]
    [SerializeField] private int cooldownSellToPlayerBuckets = 2; // after player sells to node
    [SerializeField] private int cooldownBuyFromPlayerBuckets = 1; // after player buys from node


    // Offers snapshot for the currently-open trade session
    private IReadOnlyList<NodeMarketOffer> _activeOffers = System.Array.Empty<NodeMarketOffer>();

    [Header("Trade → Pressure Feedback")]
    [SerializeField] private float pressureImpulsePerUnitSold = 0.08f;      // tune
    [SerializeField] private float pressureImpulsePerOrder = 0.15f;         // tune
    [SerializeField] private float minImpulsePerLine = 0.05f;               // avoids “sell 1 does nothing”
    [SerializeField] private float pressureFeedbackClampPerTrade = 2.0f;    // safety cap per trade action

    [Header("Trade Fees")]
    [Range(0f, 0.25f)] public float baseFeeRate = 0.03f;
    [Range(0f, 0.25f)] public float tradeRatingFeeBoost = 0.02f;
    [Min(0)] public int minFeePerLine = 1;

    [Header("World Health (placeholder)")]
    [Range(0f, 1f)] public float worldUnhealth01 = 0f;
    [Range(0f, 0.25f)] public float worldUnhealthFeeBoost = 0.03f;

    private void Reset()
    {
        overlay = FindAnyObjectByType<MiniGameOverlayHost>();
        playerRef = FindAnyObjectByType<WorldMapPlayerRef>();
        timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        binder = FindAnyObjectByType<WorldMapRuntimeBinder>();

        // ResourceCatalog is a ScriptableObject; FindAnyObjectByType usually fails if it's not instantiated in-scene.
        // Prefer inspector assignment. We still try anyway.
        resourceCatalog = FindAnyObjectByType<ResourceCatalog>();
    }

    private void Awake()
    {
        if (overlay == null) overlay = FindAnyObjectByType<MiniGameOverlayHost>();
        if (playerRef == null) playerRef = FindAnyObjectByType<WorldMapPlayerRef>();
        if (timeOfDay == null) timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        if (binder == null) binder = FindAnyObjectByType<WorldMapRuntimeBinder>();

        BuildPoliciesIfPossible();
        BuildMarketIfPossible();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void BuildPoliciesIfPossible()
    {
        // Market policy
        if (binder != null && binder.Registry != null && resourceCatalog != null)
        {
            if (marketTuning == null)
            {
                Debug.LogWarning("[TradeWorldMapRunner] Missing marketTuning (PressureMarketPolicyTuning). Using defaults.");
                _policy = new PressureMarketPolicy(binder.Registry, resourceCatalog, null);
            }
            else
            {
                _policy = new PressureMarketPolicy(binder.Registry, resourceCatalog, marketTuning);
            }
        }
        else
        {
            Debug.LogWarning("[TradeWorldMapRunner] Missing binder/registry/resourceCatalog; falling back to stub market policy.");
            _policy = new StubMarketPolicy(binder != null ? binder.Registry : null);
        }

        // Fee policy (always available even if market policy is stubbed)
        _feePolicy = new NodeScaledTradeFeePolicy(
            baseFeeRate: baseFeeRate,
            tradeRatingFeeBoost: tradeRatingFeeBoost,
            minFeePerLine: minFeePerLine,
            worldUnhealth01: worldUnhealth01,
            worldUnhealthFeeBoost: worldUnhealthFeeBoost
        );
    }

    private void BuildMarketIfPossible()
    {
        if (playerRef != null && playerRef.State != null && binder != null && binder.Registry != null && _policy != null)
        {
            _market = new MarketService(playerRef.State, _policy, binder.Registry);
        }
    }

    private void Subscribe()
    {
        if (_subscribed || overlay == null) return;
        overlay.EffectEmitted += OnMiniGameEffect;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || overlay == null) return;
        overlay.EffectEmitted -= OnMiniGameEffect;
        _subscribed = false;
    }

    private void OnMiniGameEffect(MiniGameEffect e)
    {
        if (playerRef?.State == null) return;
        if (e.kind != MiniGameEffectKind.Transaction) return;
        if (e.system != "Trade") return;

        if (binder == null || binder.Registry == null)
        {
            Debug.LogError("[Trade] Missing binder/registry.");
            return;
        }

        string nodeId = playerRef.State.currentNodeId;
        var nodeState = binder.Registry.GetNodeState(nodeId);
        int bucket = timeOfDay != null ? timeOfDay.DayIndex : 0;

        // you should have these serialized on the runner
        int cooldownSellToPlayerBuckets = 2;
        int cooldownBuyFromPlayerBuckets = 1;

        if (TradeEffectApplier.TryApply(
            e,
            playerRef.State,
            _activeOffers,
            nodeState,
            _feePolicy,
            bucket,
            cooldownSellToPlayerBuckets,
            cooldownBuyFromPlayerBuckets,
            out var receipt,
            out var failNote))
        {
            Debug.Log($"[Trade] OK node={receipt.nodeId} Δcredits={receipt.creditsDelta} fees={receipt.totalFeesPaid}");
            ApplySellToNodePressureFeedback(receipt);
        }
        else
        {
            Debug.LogWarning($"[Trade] Failed: {failNote}");
        }
    }


    [ContextMenu("Trade/Open at Current Node")]
    public void OpenTrade()
    {
        if (overlay == null) overlay = FindAnyObjectByType<MiniGameOverlayHost>();
        if (playerRef == null) playerRef = FindAnyObjectByType<WorldMapPlayerRef>();
        if (timeOfDay == null) timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        if (binder == null) binder = FindAnyObjectByType<WorldMapRuntimeBinder>();

        if (overlay == null) { Debug.LogError("[TradeWorldMapRunner] Missing MiniGameOverlayHost."); return; }
        if (playerRef == null || playerRef.State == null) { Debug.LogError("[TradeWorldMapRunner] Missing WorldMapPlayerRef/State."); return; }
        if (timeOfDay == null) { Debug.LogError("[TradeWorldMapRunner] Missing TimeOfDayManager."); return; }
        if (binder == null || binder.Registry == null || !binder.IsBuilt) { Debug.LogError("[TradeWorldMapRunner] Missing/broken WorldMapRuntimeBinder or runtime not built yet."); return; }
        if (resourceCatalog == null) { Debug.LogError("[TradeWorldMapRunner] Missing ResourceCatalog reference (assign in inspector)."); return; }

        // Rebuild policies/market in case you fixed inspector refs mid-session
        BuildPoliciesIfPossible();
        BuildMarketIfPossible();

        if (_market == null)
        {
            Debug.LogError("[TradeWorldMapRunner] MarketService not initialized.");
            return;
        }

        string nodeId = playerRef.State.currentNodeId;
        int bucket = timeOfDay.DayIndex;

        var offers = _market.GetOffers(nodeId, bucket);
        _activeOffers = offers;

        Subscribe();

        var ctx = new MiniGameContext
        {
            targetId = nodeId,
            difficulty = 1f,
            pressure = 0f,
            seed = bucket
        };

        var nodeState = binder.Registry.GetNodeState(nodeId);

        var feePreview = new TradeFeePreview(
            baseFeeRate,
            tradeRatingFeeBoost,
            minFeePerLine,
            worldUnhealth01,
            worldUnhealthFeeBoost
        );

        var cart = new TradeCartridge(nodeId, bucket, offers, playerRef.State, nodeState, feePreview, resourceCatalog);
        overlay.Open(cart, ctx);


        Debug.Log($"[Trade] Opened at node={nodeId} day={bucket} offers={offers.Count}");
    }

    private void ApplySellToNodePressureFeedback(TradeService.TradeReceipt receipt)
    {
        if (receipt == null) return;
        if (binder == null || binder.Registry == null) return;

        var node = binder.Registry.GetNodeState(receipt.nodeId);
        if (node == null) return;

        float totalAppliedThisTrade = 0f;

        for (int i = 0; i < receipt.appliedLines.Count; i++)
        {
            var line = receipt.appliedLines[i];

            // Only when player sells to node (node buys from player)
            if (line.direction != TradeDirection.SellToNode) continue;
            if (line.quantity <= 0) continue;
            if (string.IsNullOrWhiteSpace(line.itemId)) continue;

            // Linear partial impulse based on quantity sold
            float impulse = pressureImpulsePerUnitSold * line.quantity + pressureImpulsePerOrder;
            impulse = Mathf.Max(minImpulsePerLine, impulse);

            // Satisfying demand => shortage eases => pressure moves UP toward 0/positive
            node.AddPressureImpulse(line.itemId, +impulse);

            totalAppliedThisTrade += impulse;
            if (totalAppliedThisTrade >= pressureFeedbackClampPerTrade)
                break;
        }
    }
}
