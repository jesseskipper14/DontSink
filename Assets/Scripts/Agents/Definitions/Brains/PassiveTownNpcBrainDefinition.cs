using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Brains/Passive Town NPC Brain")]
public sealed class PassiveTownNpcBrainDefinition : AgentBrainDefinition
{
    public override IAgentBrainRuntime CreateRuntime()
    {
        return new PassiveTownNpcBrainRuntime();
    }

    private sealed class PassiveTownNpcBrainRuntime : IAgentBrainRuntime
    {
        public void Initialize(AgentController agent)
        {
        }

        public void Tick(AgentController agent, float deltaTime)
        {
        }
    }
}