using UnityEngine;

public interface IAgentMovementRuntime
{
    void Initialize(AgentController agent);
    void Tick(AgentController agent, float deltaTime);
}

public abstract class AgentMovementDefinition : ScriptableObject
{
    public abstract IAgentMovementRuntime CreateRuntime();
}