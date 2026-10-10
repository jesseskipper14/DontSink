using MiniGames;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NodeLeaderAgentServiceHandler : AgentServiceHandler
{
    public TownCenterBuilding Building { get; private set; }
    public void Bind(TownCenterBuilding building) => Building = building;
    public override bool CanHandle(AgentServiceDefinition service) => service is NodeLeaderServiceDefinition;

    public override bool TryHandle(AgentServiceContext context, out AgentServiceResult result)
    {
        result = AgentServiceResult.Unhandled();
        if (context.Agent == null || context.Agent.gameObject != gameObject || !CanHandle(context.Service)) return false;
        var manager = CameraManager.ForActor(context.Actor);
        if (manager == null || !manager.CanProvideGameplayInput)
        { result = AgentServiceResult.Unhandled("Node Leader requires a local requester."); return false; }
        var overlay = Object.FindFirstObjectByType<MiniGameOverlayHost>();
        var cartridge = new NodeLeaderCartridge(context, Building);
        if (overlay == null || !cartridge.IsInRange())
        { result = AgentServiceResult.Unhandled("Approach this settlement's Node Leader on foot."); return false; }
        overlay.Open(cartridge, new MiniGameContext { targetId = "node_leader:" + Building.NodeStableId });
        result = AgentServiceResult.Handled("Opened Node Leader services.");
        return true;
    }
}
