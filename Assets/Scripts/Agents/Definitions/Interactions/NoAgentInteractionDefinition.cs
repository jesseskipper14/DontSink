using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Interaction/None")]
public sealed class NoAgentInteractionDefinition : AgentInteractionDefinition
{
    public override IAgentInteractionRuntime CreateRuntime()
    {
        return new NoInteractionRuntime();
    }

    private sealed class NoInteractionRuntime : IAgentInteractionRuntime
    {
        public bool CanInteract(AgentController agent, in InteractContext context) => false;

        public string GetPromptVerb(AgentController agent, in InteractContext context) => string.Empty;

        public bool Interact(AgentController agent, in InteractContext context) => false;
    }
}