using System.Collections.Generic;
using UnityEngine;
using MiniGames;
using WorldMap.Player.Trade;

/// <summary>
/// Opens the TradeCartridge in a non-worldmap scene, using a PhysicalCrateItemStore.
/// Requires access to offers + node state if you want fees/embargo. If those are missing, it still works (fee=0).
/// </summary>
public sealed class TradeBoatSceneRunner : MonoBehaviour
{
    [Header("Refs")]
    public MiniGameOverlayHost overlay;
    public WorldMapPlayerRef playerRef;
    public TimeOfDayManager timeOfDay;

    [Tooltip("Optional: runtime binder if your world map runtime persists across scenes.")]
    public WorldMapRuntimeBinder binder;

    [Tooltip("Resource catalog used by PressureMarketPolicy.")]
    public ResourceCatalog resourceCatalog;

    [Header("Physical Store")]
    public PhysicalCrateItemStore physicalStore;

    [Header("Fallback Offers (if market service unavailable)")]
    public List<NodeMarketOffer> debugOffers = new List<NodeMarketOffer>();

    private MarketService _market;
    private IMarketPolicy _policy;
    private ITradeFeePolicy _feePolicy;

    private bool _subscribed;

    [Header("Trade Fees")]
    [Range(0f, 0.25f)] public float baseFeeRate = 0.03f;
    [Range(0f, 0.25f)] public float tradeRatingFeeBoost = 0.02f;
    [Min(0)] public int minFeePerLine = 1;

    [Header("World Health (placeholder)")]
    [Range(0f, 1f)] public float worldUnhealth01 = 0f;
    [Range(0f, 0.25f)] public float worldUnhealthFeeBoost = 0.03f;

    private IReadOnlyList<NodeMarketOffer> _activeOffers = System.Array.Empty<NodeMarketOffer>();

    private void Reset()
    {
        overlay = FindAnyObjectByType<MiniGameOverlayHost>();
        playerRef = FindAnyObjectByType<WorldMapPlayerRef>();
        timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        binder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        physicalStore = FindAnyObjectByType<PhysicalCrateItemStore>();
        resourceCatalog = FindAnyObjectByType<ResourceCatalog>();
    }

    private void Awake()
    {
        if (overlay == null) overlay = FindAnyObjectByType<MiniGameOverlayHost>();
        if (playerRef == null) playerRef = FindAnyObjectByType<WorldMapPlayerRef>();
        if (timeOfDay == null) timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        if (binder == null) binder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        if (physicalStore == null) physicalStore = FindAnyObjectByType<PhysicalCrateItemStore>();

        BuildPoliciesIfPossible();
        BuildMarketIfPossible();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void BuildPoliciesIfPossible()
    {
        if (binder != null && binder.Registry != null && resourceCatalog != null)
        {
            _policy = new PressureMarketPolicy(binder.Registry, resourceCatalog, null);
        }
        else
        {
            _policy = new StubMarketPolicy(binder != null ? binder.Registry : null);
        }

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
        else
        {
            _market = null;
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
        if (physicalStore == null) return;

        if (e.kind != MiniGameEffectKind.Transaction) return;
        if (e.system != "Trade") return;

        string nodeId = playerRef.State.currentNodeId;
        int bucket = timeOfDay != null ? timeOfDay.DayIndex : 0;

        MapNodeState nodeState = null;
        if (binder != null && binder.Registry != null && !string.IsNullOrWhiteSpace(nodeId))
        {
            // Safe: some scenes may not have a valid node.
            try
            {
                nodeState = binder.Registry.GetNodeState(nodeId);
            }
            catch
            {
                nodeState = null;
            }
        }

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
            physicalStore,
            out var receipt,
            out var failNote))
        {
            Debug.Log($"[TradeBoatScene] OK node={receipt.nodeId} Δcredits={receipt.creditsDelta} fees={receipt.totalFeesPaid}");
        }
        else
        {
            Debug.LogWarning($"[TradeBoatScene] Failed: {failNote}");
        }
    }

    [ContextMenu("Trade/Open (Boat Scene)")]
    public void OpenTrade()
    {
        if (overlay == null) overlay = FindAnyObjectByType<MiniGameOverlayHost>();
        if (playerRef == null) playerRef = FindAnyObjectByType<WorldMapPlayerRef>();
        if (timeOfDay == null) timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        if (physicalStore == null) physicalStore = FindAnyObjectByType<PhysicalCrateItemStore>();

        if (overlay == null) { Debug.LogError("[TradeBoatSceneRunner] Missing MiniGameOverlayHost."); return; }
        if (playerRef == null || playerRef.State == null) { Debug.LogError("[TradeBoatSceneRunner] Missing WorldMapPlayerRef/State."); return; }
        if (resourceCatalog == null) { Debug.LogError("[TradeBoatSceneRunner] Missing ResourceCatalog (assign in inspector)."); return; }
        if (physicalStore == null) { Debug.LogError("[TradeBoatSceneRunner] Missing PhysicalCrateItemStore."); return; }

        BuildPoliciesIfPossible();
        BuildMarketIfPossible();

        string nodeId = playerRef.State.currentNodeId;
        int bucket = timeOfDay != null ? timeOfDay.DayIndex : 0;

        IReadOnlyList<NodeMarketOffer> offers = null;

        if (_market != null && !string.IsNullOrWhiteSpace(nodeId))
            offers = _market.GetOffers(nodeId, bucket);
        else
            offers = debugOffers;

        _activeOffers = offers ?? System.Array.Empty<NodeMarketOffer>();

        Subscribe();

        var ctx = new MiniGameContext
        {
            targetId = nodeId,
            difficulty = 1f,
            pressure = 0f,
            seed = bucket
        };

        MapNodeState nodeState = null;
        if (binder != null && binder.Registry != null && !string.IsNullOrWhiteSpace(nodeId))
            try
            {
                nodeState = binder.Registry.GetNodeState(nodeId);
            }
            catch
            {
                nodeState = null;
            }

        var feePreview = new TradeFeePreview(
            baseFeeRate,
            tradeRatingFeeBoost,
            minFeePerLine,
            worldUnhealth01,
            worldUnhealthFeeBoost
        );

        var cart = new TradeCartridge(nodeId, bucket, _activeOffers, playerRef.State, physicalStore, nodeState, feePreview, resourceCatalog);
        overlay.Open(cart, ctx);

        Debug.Log($"[TradeBoatScene] Opened node={nodeId} day={bucket} offers={_activeOffers.Count}");
    }
}
