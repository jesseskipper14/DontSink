using UnityEngine;

[DisallowMultipleComponent]
public class AgentController : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private AgentDefinition definition;

    [Header("Runtime Identity")]
    [SerializeField] private string stableId;
    [SerializeField] private string nodeId;

    [Header("Optional Runtime Bounds")]
    [SerializeField] private AgentHomeBounds homeBounds;

    private IAgentBrainRuntime brainRuntime;
    private IAgentMovementRuntime movementRuntime;
    private IAgentInteractionRuntime interactionRuntime;

    private bool initialized;

    public AgentDefinition Definition => definition;
    public string StableId => stableId;
    public string NodeId => nodeId;
    public AgentKind Kind => definition != null ? definition.Kind : AgentKind.Other;
    public AgentHomeBounds HomeBounds => homeBounds;

    public string DisplayName
    {
        get
        {
            if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
                return definition.DisplayName;

            return name;
        }
    }

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();
        AgentRegistry.Register(this);
    }

    private void OnDisable()
    {
        AgentRegistry.Unregister(this);
    }

    private void Update()
    {
        if (!initialized)
            InitializeIfNeeded();

        float dt = Time.deltaTime;

        brainRuntime?.Tick(this, dt);
        movementRuntime?.Tick(this, dt);
    }

    public void Initialize(AgentDefinition newDefinition, string newStableId, string newNodeId, AgentHomeBounds newHomeBounds)
    {
        definition = newDefinition;
        stableId = newStableId;
        nodeId = newNodeId;
        homeBounds = newHomeBounds;

        initialized = false;
        InitializeIfNeeded();
    }

    public void InitializeIfNeeded()
    {
        if (initialized)
            return;

        EnsureStableId();

        if (definition == null)
        {
            Debug.LogWarning($"AgentController '{name}' has no AgentDefinition.", this);
            initialized = true;
            return;
        }

        AgentBehaviorSetDefinition behaviorSet = definition.BehaviorSet;

        if (behaviorSet != null)
        {
            brainRuntime = behaviorSet.Brain != null
                ? behaviorSet.Brain.CreateRuntime()
                : null;

            movementRuntime = behaviorSet.Movement != null
                ? behaviorSet.Movement.CreateRuntime()
                : null;

            interactionRuntime = behaviorSet.Interaction != null
                ? behaviorSet.Interaction.CreateRuntime()
                : null;
        }

        brainRuntime?.Initialize(this);
        movementRuntime?.Initialize(this);

        initialized = true;
    }

    public bool HasService(int index)
    {
        return GetService(index) != null;
    }

    public AgentServiceDefinition GetService(int index)
    {
        if (definition == null || definition.Services == null)
            return null;

        if (index < 0 || index >= definition.Services.Length)
            return null;

        return definition.Services[index];
    }

    public bool CanInteract(in InteractContext context)
    {
        InitializeIfNeeded();
        return interactionRuntime != null && interactionRuntime.CanInteract(this, context);
    }

    public string GetInteractionPromptVerb(in InteractContext context)
    {
        InitializeIfNeeded();

        if (interactionRuntime == null)
            return string.Empty;

        return interactionRuntime.GetPromptVerb(this, context);
    }

    public bool TryInteract(in InteractContext context)
    {
        InitializeIfNeeded();

        if (interactionRuntime == null)
            return false;

        return interactionRuntime.Interact(this, context);
    }

    public bool TryRunService(int serviceIndex, in InteractContext context)
    {
        AgentServiceDefinition service = GetService(serviceIndex);
        return TryRunService(service, context);
    }

    public bool TryRunService(AgentServiceDefinition service, in InteractContext context)
    {
        if (service == null)
            return false;

        AgentServiceContext serviceContext = new AgentServiceContext(this, service, context);
        AgentServiceResult result = service.Run(serviceContext);

        if (!string.IsNullOrWhiteSpace(result.message))
            Debug.Log($"[Agent Service] {result.message}", this);

        return result.handled;
    }

    public AgentRuntimeSnapshot BuildSnapshot()
    {
        return new AgentRuntimeSnapshot
        {
            stableId = stableId,
            definitionId = definition != null ? definition.Id : string.Empty,
            nodeId = nodeId,
            kind = Kind,
            removed = false
        };
    }

    private void EnsureStableId()
    {
        if (!string.IsNullOrWhiteSpace(stableId))
            return;

        stableId = $"{gameObject.scene.name}:{name}:{GetInstanceID()}";
        Debug.LogWarning($"Generated temporary Agent stableId for '{name}': {stableId}. Use AgentSpawnPoint stable IDs for save-safe agents.", this);
    }
}