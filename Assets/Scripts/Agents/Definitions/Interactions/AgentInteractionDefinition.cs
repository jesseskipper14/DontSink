using UnityEngine;

public interface IAgentInteractionRuntime
{
    bool CanInteract(AgentController agent, in InteractContext context);
    string GetPromptVerb(AgentController agent, in InteractContext context);
    bool Interact(AgentController agent, in InteractContext context);
}

public abstract class AgentInteractionDefinition : ScriptableObject
{
    public abstract IAgentInteractionRuntime CreateRuntime();
}