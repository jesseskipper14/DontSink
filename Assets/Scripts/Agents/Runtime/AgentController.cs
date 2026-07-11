using UnityEngine;

[DisallowMultipleComponent]
public class AgentController : MonoBehaviour, ISimulationLodTarget
{
    [Header("Definition")]
    [SerializeField] private AgentDefinition definition;

    [Header("Runtime Identity")]
    [SerializeField] private string stableId;
    [SerializeField] private string nodeId;

    [Header("Runtime Identity Logging")]
    [SerializeField] private bool warnWhenGeneratingTemporaryStableId = false;
    [SerializeField] private bool logGeneratedTemporaryStableId = false;

    [Header("Simulation LOD")]
    [SerializeField] private bool allowSimulationLod = true;
    [SerializeField] private SimulationLodState currentLodState = SimulationLodState.Full;

    [SerializeField, Min(0.02f)]
    private float reducedTickInterval = 0.25f;

    private float nextReducedTickTime;

    [Header("Optional Runtime Bounds")]
    [SerializeField] private AgentHomeBounds homeBounds;

    private IAgentBrainRuntime brainRuntime;
    private IAgentMovementRuntime movementRuntime;
    private IAgentInteractionRuntime interactionRuntime;

    public IAgentBrainRuntime BrainRuntime => brainRuntime;
    public IAgentMovementRuntime MovementRuntime => movementRuntime;
    public IAgentInteractionRuntime InteractionRuntime => interactionRuntime;

    public bool TryGetMovementDiagnostics(out AgentMovementDiagnosticsSnapshot snapshot)
    {
        snapshot = null;

        if (movementRuntime is IAgentMovementDiagnosticsProvider provider)
        {
            snapshot = provider.GetMovementDiagnosticsSnapshot();
            return snapshot != null;
        }

        return false;
    }

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

    public SimulationLodState CurrentLodState => currentLodState;
    public bool AllowSimulationLod => allowSimulationLod;
    public float ReducedTickInterval => reducedTickInterval;

    public void SetSimulationLodState(
        SimulationLodState state,
        SimulationLodAuthorityMode authorityMode)
    {
        if (!allowSimulationLod)
            state = SimulationLodState.Full;

        if (currentLodState == state)
            return;

        currentLodState = state;

        if (currentLodState == SimulationLodState.Frozen)
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        nextReducedTickTime = 0f;
    }

    private void Awake()
    {
        // Intentionally do not initialize here.
        //
        // Runtime-spawned agents are configured immediately after Instantiate,
        // but Awake runs during Instantiate before the spawner gets control back.
        //
        // Scene-placed agents can safely initialize in Start.
    }

    private void OnEnable()
    {
        // Do not initialize here either.
        // OnEnable also runs before runtime spawners can call Initialize(...).

        AgentRegistry.Register(this);
    }

    private void OnDisable()
    {
        AgentRegistry.Unregister(this);
    }

    private void Start()
    {
        if (!initialized)
            InitializeIfNeeded();
    }

    private void Update()
    {
        // Still allow pre-placed agents or delayed runtime assignment to initialize
        // once a definition becomes available.
        if (!initialized && definition != null)
            InitializeIfNeeded();

        if (!initialized)
            return;

        if (initialized && movementRuntime == null && HasMovementDefinition)
            ReinitializeRuntime();

        if (!ShouldTickThisFrame(out float dt))
            return;

        brainRuntime?.Tick(this, dt);
        movementRuntime?.Tick(this, dt);
    }

    private bool ShouldTickThisFrame(out float dt)
    {
        dt = Time.deltaTime;

        switch (currentLodState)
        {
            case SimulationLodState.Full:
                return true;

            case SimulationLodState.Reduced:
                if (Time.time < nextReducedTickTime)
                    return false;

                dt = Mathf.Max(Time.deltaTime, reducedTickInterval);
                nextReducedTickTime = Time.time + reducedTickInterval;
                return true;

            case SimulationLodState.Frozen:
                return false;

            default:
                return true;
        }
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

        if (definition == null)
        {
            initialized = false;
            return;
        }

        EnsureStableId();

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

        string message =
            $"Generated temporary Agent stableId for '{name}': {stableId}. " +
            "This is acceptable for transient runtime agents like fish. " +
            "Use AgentSpawnPoint stable IDs for save-safe agents.";

        if (warnWhenGeneratingTemporaryStableId)
        {
            Debug.LogWarning(message, this);
        }
        else if (logGeneratedTemporaryStableId)
        {
            Debug.Log($"[AgentController] {message}", this);
        }
    }

    [ContextMenu("Reinitialize Agent Runtime")]
    public void ReinitializeRuntime()
    {
        brainRuntime = null;
        movementRuntime = null;
        interactionRuntime = null;

        initialized = false;
        InitializeIfNeeded();
    }

    public bool HasMovementDefinition
    {
        get
        {
            return definition != null &&
                   definition.BehaviorSet != null &&
                   definition.BehaviorSet.Movement != null;
        }
    }
}