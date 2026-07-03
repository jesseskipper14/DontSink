using UnityEngine;

[DisallowMultipleComponent]
public sealed class AgentInteractable :
    MonoBehaviour,
    IInteractable,
    IInteractPromptProvider,
    IInteractionLabelProvider,
    IInteractionRangeProvider
{
    [Header("Refs")]
    [SerializeField] private AgentController agent;
    [SerializeField] private Transform promptAnchor;

    [Header("Interaction")]
    [SerializeField] private int priority = 50;
    [SerializeField, Min(0f)] private float maxUseDistance = 1.8f;

    [Header("Hover / Prompt Ranges")]
    [SerializeField, Min(0f)] private float hoverNameRange = 4.0f;
    [SerializeField, Min(0f)] private float actionRange = 1.8f;

    [Header("Prompt")]
    [SerializeField] private string fallbackVerb = "Talk";
    [SerializeField] private bool useAgentDisplayNameAsLabel = true;

    public int InteractionPriority => priority;

    private void Reset()
    {
        agent = GetComponentInParent<AgentController>();
        promptAnchor = transform;
    }

    private void Awake()
    {
        if (agent == null)
            agent = GetComponentInParent<AgentController>();

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    public bool CanInteract(in InteractContext context)
    {
        if (agent == null)
            return false;

        if (maxUseDistance > 0f)
        {
            float dist = Vector2.Distance(context.Origin, transform.position);
            if (dist > maxUseDistance)
                return false;
        }

        return agent.CanInteract(context);
    }

    public void Interact(in InteractContext context)
    {
        if (agent == null)
            return;

        agent.TryInteract(context);
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (agent == null)
            return fallbackVerb;

        string verb = agent.GetInteractionPromptVerb(context);
        return string.IsNullOrWhiteSpace(verb) ? fallbackVerb : verb;
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    public string GetInteractionLabel(in InteractContext context)
    {
        if (!useAgentDisplayNameAsLabel || agent == null)
            return string.Empty;

        return agent.DisplayName;
    }

    public bool TryGetHoverNameRange(out float range)
    {
        range = hoverNameRange;
        return hoverNameRange > 0f;
    }

    public bool TryGetActionRange(out float range)
    {
        range = actionRange;
        return actionRange > 0f;
    }
}