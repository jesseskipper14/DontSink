using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Services/Market Trade Service")]
public sealed class MarketTradeServiceDefinition : AgentServiceDefinition
{
    [Header("Market Trade")]
    [SerializeField] private string marketKey = "node_market";

    public override AgentServiceKind Kind => AgentServiceKind.MarketTrade;
    public string MarketKey => marketKey;
}