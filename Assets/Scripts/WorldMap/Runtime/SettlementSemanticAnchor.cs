using UnityEngine;

/// <summary>Stable service/quest identity; actual interaction systems bind to this anchor.</summary>
public sealed class SettlementSemanticAnchor : MonoBehaviour
{
    public string NodeStableId { get; private set; }
    public string PlotId { get; private set; }
    public SettlementRole Role { get; private set; }
    public bool ServiceActive { get; private set; }
    public string SemanticNpcId => $"{NodeStableId}/{Role}/{PlotId}/semantic";
    public void Initialize(string nodeId, string plotId, SettlementRole role, bool active)
    { NodeStableId = nodeId; PlotId = plotId; Role = role; ServiceActive = active; }
}
