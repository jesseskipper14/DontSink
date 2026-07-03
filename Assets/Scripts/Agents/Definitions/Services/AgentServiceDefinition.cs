using UnityEngine;

public abstract class AgentServiceDefinition : ScriptableObject
{
    [Header("Service Identity")]
    [SerializeField] private string serviceId = "service_new";
    [SerializeField] private string promptLabel = "Talk";

    public string ServiceId => serviceId;
    public string PromptLabel => promptLabel;

    public abstract AgentServiceKind Kind { get; }

    public virtual AgentServiceResult Run(AgentServiceContext context)
    {
        if (AgentServiceDispatcher.TryDispatch(context, out AgentServiceResult result))
            return result;

        string message = $"No AgentServiceHandler handled service '{ServiceId}' ({Kind}) for agent '{context.Agent.DisplayName}'.";
        Debug.LogWarning(message, context.Agent);

        return AgentServiceResult.Unhandled(message);
    }
}