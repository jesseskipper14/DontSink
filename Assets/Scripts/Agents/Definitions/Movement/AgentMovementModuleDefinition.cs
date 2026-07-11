using UnityEngine;

public interface IAgentMovementModuleRuntime
{
    void Initialize(AgentController agent, AgentMovementModuleSlot slot);
    AgentMovementIntent Evaluate(AgentMovementBlackboard blackboard, AgentMovementModuleSlot slot, float dt);
}

public abstract class AgentMovementModuleDefinition : ScriptableObject
{
    public abstract IAgentMovementModuleRuntime CreateRuntime();
}