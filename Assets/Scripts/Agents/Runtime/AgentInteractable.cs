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

    [Header("Boarding Context")]
    [Tooltip("If true, world/NPC interaction must match the player's boarding context. Unboarded agents cannot be talked to from inside a boat; agents parented under a Boat require the player to be boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

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

        if (!PassesBoardingContext(context))
            return false;

        return agent.CanInteract(context);
    }

    public void Interact(in InteractContext context)
    {
        if (agent == null)
            return;

        if (!PassesBoardingContext(context))
            return;

        agent.TryInteract(context);
    }

    private bool PassesBoardingContext(
        in InteractContext context)
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        PlayerBoardingState boarding =
            FindBoardingState(context);

        // Preserve non-player/future callers that do not carry player boarding state.
        if (boarding == null)
            return true;

        Boat agentBoat =
            GetComponentInParent<Boat>();

        if (agentBoat == null &&
            agent != null)
        {
            agentBoat =
                agent.GetComponentInParent<Boat>();
        }

        if (agentBoat == null)
        {
            // World/dock NPC. A boarded player is in a different interaction
            // context even if the cursor and distance happen to overlap.
            return !boarding.IsBoarded;
        }

        // Boat-owned NPC. Require the player to be aboard that same boat.
        return boarding.IsBoarded &&
               boarding.CurrentBoatRoot ==
               agentBoat.transform;
    }

    private static PlayerBoardingState FindBoardingState(
        in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerBoardingState boarding =
                context.InteractorGO
                    .GetComponentInParent<PlayerBoardingState>();

            if (boarding != null)
                return boarding;

            boarding =
                context.InteractorGO
                    .GetComponentInChildren<PlayerBoardingState>(
                        true);

            if (boarding != null)
                return boarding;
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState boarding =
                context.InteractorTransform
                    .GetComponentInParent<PlayerBoardingState>();

            if (boarding != null)
                return boarding;
        }

        return null;
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