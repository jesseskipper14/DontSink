using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Interaction/Service Interaction")]
public sealed class ServiceAgentInteractionDefinition : AgentInteractionDefinition
{
    [SerializeField] private string fallbackPromptVerb = "Talk";
    [SerializeField] private int defaultServiceIndex = 0;

    public override IAgentInteractionRuntime CreateRuntime()
    {
        return new ServiceInteractionRuntime(fallbackPromptVerb, defaultServiceIndex);
    }

    private sealed class ServiceInteractionRuntime : IAgentInteractionRuntime
    {
        private readonly string fallbackPromptVerb;
        private readonly int defaultServiceIndex;

        public ServiceInteractionRuntime(string fallbackPromptVerb, int defaultServiceIndex)
        {
            this.fallbackPromptVerb = fallbackPromptVerb;
            this.defaultServiceIndex = defaultServiceIndex;
        }

        public bool CanInteract(AgentController agent, in InteractContext context)
        {
            return agent != null && agent.HasService(defaultServiceIndex);
        }

        public string GetPromptVerb(AgentController agent, in InteractContext context)
        {
            AgentServiceDefinition service = agent != null
                ? agent.GetService(defaultServiceIndex)
                : null;

            if (service != null && !string.IsNullOrWhiteSpace(service.PromptLabel))
                return service.PromptLabel;

            return fallbackPromptVerb;
        }

        public bool Interact(AgentController agent, in InteractContext context)
        {
            return agent != null && agent.TryRunService(defaultServiceIndex, context);
        }
    }
}