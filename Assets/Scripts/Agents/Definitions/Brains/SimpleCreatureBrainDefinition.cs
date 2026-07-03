using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Brains/Simple Creature Brain")]
public sealed class SimpleCreatureBrainDefinition : AgentBrainDefinition
{
    public override IAgentBrainRuntime CreateRuntime()
    {
        return new SimpleCreatureBrainRuntime();
    }

    private sealed class SimpleCreatureBrainRuntime : IAgentBrainRuntime
    {
        public void Initialize(AgentController agent)
        {
        }

        public void Tick(AgentController agent, float deltaTime)
        {
        }
    }
}