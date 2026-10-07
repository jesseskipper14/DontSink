using MiniGames;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SurveyorAgentServiceHandler : AgentServiceHandler
{
    public override bool CanHandle(AgentServiceDefinition service) => service is SurveyorServiceDefinition;

    public override bool TryHandle(AgentServiceContext context, out AgentServiceResult result)
    {
        result = AgentServiceResult.Unhandled();
        // Dispatcher searches all handlers. Only this NPC's own handler may serve it.
        if (context.Agent == null || context.Agent.gameObject != gameObject || !CanHandle(context.Service)) return false;
        var overlay = Object.FindFirstObjectByType<MiniGameOverlayHost>();
        var knowledge = Object.FindFirstObjectByType<WorldMapKnowledgeSource>();
        if (overlay == null || context.Actor == null)
        { result = AgentServiceResult.Unhandled("Surveyor needs an overlay and requester."); return false; }
        string nodeId = context.Agent.NodeId;
        if (gameObject.scene.name != "NodeScene")
        { result = AgentServiceResult.Unhandled("Surveyor services are only available in NodeScene."); return false; }
        // Scene-authored NPCs can leave NodeId blank. Resolve once at interaction.
        if (string.IsNullOrWhiteSpace(nodeId)) nodeId = knowledge != null ? knowledge.CurrentNodeId : null;
        if (!HarborTravelService.TryGetNode(nodeId, out _))
        { result = AgentServiceResult.Unhandled("The Surveyor's node is not ready."); return false; }
        var cartridge = new SurveyorCartridge(context, knowledge, nodeId);
        if (!cartridge.IsInRange())
        { result = AgentServiceResult.Unhandled("Approach the Surveyor on foot first."); return false; }
        overlay.Open(cartridge, new MiniGameContext { targetId = "surveyor:" + nodeId });
        result = AgentServiceResult.Handled("Opened Surveyor services.");
        return true;
    }
}
