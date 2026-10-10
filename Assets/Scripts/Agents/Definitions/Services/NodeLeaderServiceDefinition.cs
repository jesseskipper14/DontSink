using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Services/Node Leader Service")]
public sealed class NodeLeaderServiceDefinition : AgentServiceDefinition
{
    public override AgentServiceKind Kind => AgentServiceKind.NodeLeader;
}
