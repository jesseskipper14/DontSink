using UnityEngine;
using MiniGames;
using WorldMap.Player.Trade;

public sealed class TradeWorldMapRunner : MonoBehaviour
{
    [Header("Refs")]
    public MiniGameOverlayHost overlay;
    public WorldMapPlayer player;
    public TimeOfDayManager timeOfDay;

    private MarketService _market;
    private IMarketPolicy _policy;
    private bool _subscribed;

    private void Reset()
    {
        overlay = FindAnyObjectByType<MiniGameOverlayHost>();
        player = FindAnyObjectByType<WorldMapPlayer>();
        timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
    }

    private void Awake()
    {
        if (overlay == null) overlay = FindAnyObjectByType<MiniGameOverlayHost>();
        if (player == null) player = FindAnyObjectByType<WorldMapPlayer>();
        if (timeOfDay == null) timeOfDay = FindAnyObjectByType<TimeOfDayManager>();

        _policy = new StubMarketPolicy(); // swap later
    }

    private void OnDisable()
    {
        Unsubscribe();
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
        if (player?.State == null) return;

        if (e.kind != MiniGameEffectKind.Transaction) return;
        if (e.system != "Trade") return;

        if (TradeEffectApplier.TryApply(e, player.State, out var receipt, out var failNote))
        {
            Debug.Log($"[Trade] OK node={receipt.nodeId} ?credits={receipt.creditsDelta}");
        }
        else
        {
            Debug.LogWarning($"[Trade] Failed: {failNote}");
        }
    }

    [ContextMenu("Trade/Open at Current Node")]
    public void OpenTrade()
    {
        if (overlay == null)
        {
            Debug.LogError("[TradeWorldMapRunner] Missing MiniGameOverlayHost.");
            return;
        }
        if (player == null || player.State == null)
        {
            Debug.LogError("[TradeWorldMapRunner] Missing WorldMapPlayer/State.");
            return;
        }
        if (timeOfDay == null)
        {
            Debug.LogError("[TradeWorldMapRunner] Missing TimeOfDayManager.");
            return;
        }

        string nodeId = player.State.currentNodeId;
        int bucket = timeOfDay.DayIndex;

        player.State.marketCacheByNodeId ??= new System.Collections.Generic.Dictionary<string, MarketCacheState>();

        _market = new MarketService(player.State, _policy);

        var offers = _market.GetOffers(nodeId, bucket);

        Subscribe();

        var ctx = new MiniGameContext
        {
            targetId = nodeId, // for trade, target is the node market
            difficulty = 1f,
            pressure = 0f,
            seed = bucket
        };

        var cart = new TradeCartridge(nodeId, bucket, offers, player.State);

        overlay.Open(cart, ctx);

        Debug.Log($"[Trade] Opened at node={nodeId} day={bucket} offers={offers.Count}");
    }
}
