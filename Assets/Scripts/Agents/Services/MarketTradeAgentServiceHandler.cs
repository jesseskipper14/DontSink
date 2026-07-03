using UnityEngine;

public sealed class MarketTradeAgentServiceHandler : AgentServiceHandler
{
    [Header("Refs")]
    [SerializeField] private TradeWorldMapRunner tradeRunner;

    private void Reset()
    {
        tradeRunner = FindAnyObjectByType<TradeWorldMapRunner>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (tradeRunner == null)
            tradeRunner = FindAnyObjectByType<TradeWorldMapRunner>(FindObjectsInactive.Include);

        if (tradeRunner == null)
            Debug.LogWarning($"{name}: No TradeWorldMapRunner found. Market NPC cannot open trade.", this);
    }

    public override bool CanHandle(AgentServiceDefinition service)
    {
        return service != null && service.Kind == AgentServiceKind.MarketTrade;
    }

    public override bool TryHandle(AgentServiceContext context, out AgentServiceResult result)
    {
        if (tradeRunner == null)
        {
            result = AgentServiceResult.Unhandled("Missing TradeWorldMapRunner.");
            return false;
        }

        tradeRunner.OpenTrade();

        result = AgentServiceResult.Handled($"Opened market trade for {context.Agent.DisplayName}.");
        return true;
    }
}