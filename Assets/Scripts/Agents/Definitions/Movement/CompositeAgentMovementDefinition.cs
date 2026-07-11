using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Agents/Movement/Composite Movement",
    fileName = "CompositeAgentMovement")]
public sealed class CompositeAgentMovementDefinition : AgentMovementDefinition
{
    [Header("Motor")]
    [SerializeField, Min(0f)] private float maxSpeed = 3.5f;
    [SerializeField, Min(0f)] private float acceleration = 8f;

    [Tooltip("If true, velocity moves toward zero when no module has intent.")]
    [SerializeField] private bool stopWhenNoIntent = true;

    [SerializeField, Min(0f)] private float noIntentDeceleration = 4f;

    [Header("Facing")]
    [SerializeField] private bool flipVisualOnXMovement = true;
    [SerializeField] private Transform explicitVisualRoot;

    [Header("Modules")]
    [SerializeField] private AgentMovementModuleSlot[] modules;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public float MaxSpeed => Mathf.Max(0f, maxSpeed);
    public float Acceleration => Mathf.Max(0f, acceleration);
    public bool StopWhenNoIntent => stopWhenNoIntent;
    public float NoIntentDeceleration => Mathf.Max(0f, noIntentDeceleration);
    public bool FlipVisualOnXMovement => flipVisualOnXMovement;
    public Transform ExplicitVisualRoot => explicitVisualRoot;
    public bool VerboseLogging => verboseLogging;

    public IReadOnlyList<AgentMovementModuleSlot> Modules =>
    modules ?? System.Array.Empty<AgentMovementModuleSlot>();

    public override IAgentMovementRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : IAgentMovementRuntime, IAgentMovementDiagnosticsProvider
    {
        private readonly CompositeAgentMovementDefinition definition;
        private readonly List<ModuleRuntimeEntry> runtimeEntries = new();

        private readonly AgentMovementBlackboard blackboard = new();

        private Rigidbody2D rb;
        private Transform visualRoot;

        private readonly AgentMovementDiagnosticsSnapshot diagnostics = new();
        private AgentController currentAgent;

        private float activeMaxSpeed;
        private float activeAcceleration;

        private struct ModuleRuntimeEntry
        {
            public AgentMovementModuleSlot slot;
            public IAgentMovementModuleRuntime runtime;
        }

        public Runtime(CompositeAgentMovementDefinition definition)
        {
            this.definition = definition;
        }

        public void Initialize(AgentController agent)
        {
            if (agent == null)
                return;

            currentAgent = agent;

            rb = agent.GetComponent<Rigidbody2D>();

            if (rb == null)
                Debug.LogWarning($"[CompositeMovement] Agent '{agent.name}' has no Rigidbody2D.", agent);

            blackboard.agent = agent;
            blackboard.rb = rb;
            blackboard.fishSchoolMember = agent.GetComponent<FishSchoolMember2D>();

            visualRoot = definition.ExplicitVisualRoot != null
                ? definition.ExplicitVisualRoot
                : agent.transform;

            runtimeEntries.Clear();

            activeMaxSpeed = definition.MaxSpeed;
            activeAcceleration = definition.Acceleration;

            if (definition.modules == null)
                return;

            for (int i = 0; i < definition.modules.Length; i++)
            {
                AgentMovementModuleSlot slot = definition.modules[i];

                if (slot == null || !slot.Enabled || slot.Module == null)
                    continue;

                IAgentMovementModuleRuntime runtime = slot.Module.CreateRuntime();
                if (runtime == null)
                    continue;

                runtime.Initialize(agent, slot);

                runtimeEntries.Add(new ModuleRuntimeEntry
                {
                    slot = slot,
                    runtime = runtime
                });
            }

            if (definition.VerboseLogging)
                Debug.Log($"[CompositeMovement] Initialized '{agent.name}' modules={runtimeEntries.Count}", agent);
        }

        public AgentMovementDiagnosticsSnapshot GetMovementDiagnosticsSnapshot()
        {
            return diagnostics;
        }

        public void Tick(AgentController agent, float dt)
        {
            if (agent == null)
                return;

            if (rb == null)
                rb = agent.GetComponent<Rigidbody2D>();

            if (rb == null)
                return;

            blackboard.agent = agent;
            blackboard.rb = rb;

            if (blackboard.fishSchoolMember == null)
                blackboard.fishSchoolMember = agent.GetComponent<FishSchoolMember2D>();

            if (TryBuildDesiredVelocity(dt, out Vector2 desiredVelocity))
            {
                diagnostics.rawDesiredVelocity = desiredVelocity;

                float maxSpeed = Mathf.Max(0f, activeMaxSpeed);
                float acceleration = Mathf.Max(0f, activeAcceleration);

                desiredVelocity = Vector2.ClampMagnitude(
                    desiredVelocity,
                    maxSpeed);

                diagnostics.finalDesiredVelocity = desiredVelocity;
                diagnostics.activeMaxSpeed = maxSpeed;
                diagnostics.activeAcceleration = acceleration;

                rb.linearVelocity = Vector2.MoveTowards(
                    rb.linearVelocity,
                    desiredVelocity,
                    acceleration * dt);

                UpdateFacing(rb.linearVelocity);

                diagnostics.appliedVelocity = rb.linearVelocity;
            }
            else if (definition.StopWhenNoIntent)
            {
                rb.linearVelocity = Vector2.MoveTowards(
                    rb.linearVelocity,
                    Vector2.zero,
                    definition.NoIntentDeceleration * dt);

                UpdateFacing(rb.linearVelocity);
                diagnostics.appliedVelocity = rb.linearVelocity;
            }
        }

        private bool TryBuildDesiredVelocity(float dt, out Vector2 desiredVelocity)
        {
            desiredVelocity = Vector2.zero;

            diagnostics.Clear();

            diagnostics.agentName = currentAgent != null ? currentAgent.name : "<missing agent>";
            diagnostics.movementDefinitionName = definition != null ? definition.name : "<missing movement definition>";
            diagnostics.runtimeTypeName = GetType().Name;
            diagnostics.hasRigidbody = rb != null;
            diagnostics.currentVelocity = rb != null ? rb.linearVelocity : Vector2.zero;

            activeMaxSpeed = definition.MaxSpeed;
            activeAcceleration = definition.Acceleration;

            if (runtimeEntries.Count == 0)
            {
                diagnostics.activeSummary = "No movement module runtimes were created.";
                return false;
            }

            AgentMovementIntent[] intents = new AgentMovementIntent[runtimeEntries.Count];

            int suppressPriority = int.MinValue;
            bool hasAny = false;

            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                ModuleRuntimeEntry entry = runtimeEntries[i];

                AgentMovementIntent intent =
                    entry.runtime.Evaluate(blackboard, entry.slot, dt);

                intents[i] = intent;

                if (!intent.hasIntent)
                    continue;

                hasAny = true;

                if (intent.suppressLowerPriority && intent.priority > suppressPriority)
                    suppressPriority = intent.priority;
            }

            diagnostics.suppressPriority = suppressPriority;

            Vector2 weighted = Vector2.zero;
            float totalWeight = 0f;

            int activeCount = 0;
            string activeSummary = string.Empty;

            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                ModuleRuntimeEntry entry = runtimeEntries[i];
                AgentMovementIntent intent = intents[i];

                bool suppressed =
                    intent.hasIntent &&
                    suppressPriority != int.MinValue &&
                    intent.priority < suppressPriority;

                diagnostics.modules.Add(new AgentMovementModuleDiagnostic
                {
                    slotIndex = i,
                    moduleName = entry.slot != null ? entry.slot.ModuleLabel : "<missing slot>",
                    debugLabel = intent.debugLabel,

                    slotEnabled = entry.slot != null && entry.slot.Enabled,
                    runtimeExists = entry.runtime != null,

                    priority = entry.slot != null ? entry.slot.Priority : intent.priority,
                    strength = entry.slot != null ? entry.slot.Strength : 0f,

                    hasIntent = intent.hasIntent,
                    suppressLowerPriority = intent.suppressLowerPriority,
                    suppressedByHigherPriority = suppressed,

                    intentWeight = intent.weight,
                    desiredVelocity = intent.desiredVelocity
                });

                if (!intent.hasIntent)
                    continue;

                if (suppressed)
                    continue;

                float w = Mathf.Max(0f, intent.weight);
                if (w <= 0f)
                    continue;

                weighted += intent.desiredVelocity * w;
                totalWeight += w;

                activeCount++;

                if (intent.maxSpeedOverride > activeMaxSpeed)
                    activeMaxSpeed = intent.maxSpeedOverride;

                if (intent.accelerationOverride > activeAcceleration)
                    activeAcceleration = intent.accelerationOverride;

                if (!string.IsNullOrWhiteSpace(activeSummary))
                    activeSummary += ", ";

                activeSummary += !string.IsNullOrWhiteSpace(intent.debugLabel)
                    ? intent.debugLabel
                    : $"Module {i}";
            }

            if (!hasAny)
            {
                diagnostics.activeSummary = "No module currently has movement intent.";
                return false;
            }

            if (totalWeight <= 0.0001f)
            {
                diagnostics.activeSummary = "Movement intents exist, but all were suppressed or had zero weight.";
                return false;
            }

            desiredVelocity = weighted / totalWeight;

            diagnostics.hasFinalIntent = true;
            diagnostics.finalDesiredVelocity = desiredVelocity;
            diagnostics.activeSummary = activeCount > 0
                ? activeSummary
                : "No active movement after suppression.";

            return true;
        }

        private void UpdateFacing(Vector2 velocity)
        {
            if (!definition.FlipVisualOnXMovement || visualRoot == null)
                return;

            if (Mathf.Abs(velocity.x) < 0.03f)
                return;

            Vector3 scale = visualRoot.localScale;
            float absX = Mathf.Abs(scale.x);

            scale.x = velocity.x >= 0f ? absX : -absX;
            visualRoot.localScale = scale;
        }
    }
}