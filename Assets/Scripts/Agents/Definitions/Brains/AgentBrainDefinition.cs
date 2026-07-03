using UnityEngine;

public interface IAgentBrainRuntime
{
    void Initialize(AgentController agent);
    void Tick(AgentController agent, float deltaTime);
}

public abstract class AgentBrainDefinition : ScriptableObject
{
    public abstract IAgentBrainRuntime CreateRuntime();
}