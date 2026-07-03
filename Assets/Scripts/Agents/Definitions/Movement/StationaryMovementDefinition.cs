using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Movement/Stationary")]
public class StationaryMovementDefinition : AgentMovementDefinition
{
    [SerializeField] private bool zeroRigidbodyVelocity = true;

    public override IAgentMovementRuntime CreateRuntime()
    {
        return new StationaryMovementRuntime(zeroRigidbodyVelocity);
    }

    private sealed class StationaryMovementRuntime : IAgentMovementRuntime
    {
        private readonly bool zeroRigidbodyVelocity;
        private Rigidbody2D rb;

        public StationaryMovementRuntime(bool zeroRigidbodyVelocity)
        {
            this.zeroRigidbodyVelocity = zeroRigidbodyVelocity;
        }

        public void Initialize(AgentController agent)
        {
            rb = agent.GetComponent<Rigidbody2D>();
        }

        public void Tick(AgentController agent, float deltaTime)
        {
            if (!zeroRigidbodyVelocity || rb == null)
                return;

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}