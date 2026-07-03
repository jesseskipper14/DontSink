using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Movement/Stationary Within Home Bounds")]
public class StationaryWithinHomeBoundsMovementDefinition : AgentMovementDefinition
{
    [SerializeField] private bool clampToHomeBounds = true;
    [SerializeField] private bool zeroRigidbodyVelocity = true;

    public override IAgentMovementRuntime CreateRuntime()
    {
        return new StationaryWithinHomeBoundsMovementRuntime(clampToHomeBounds, zeroRigidbodyVelocity);
    }

    private sealed class StationaryWithinHomeBoundsMovementRuntime : IAgentMovementRuntime
    {
        private readonly bool clampToHomeBounds;
        private readonly bool zeroRigidbodyVelocity;
        private Rigidbody2D rb;

        public StationaryWithinHomeBoundsMovementRuntime(bool clampToHomeBounds, bool zeroRigidbodyVelocity)
        {
            this.clampToHomeBounds = clampToHomeBounds;
            this.zeroRigidbodyVelocity = zeroRigidbodyVelocity;
        }

        public void Initialize(AgentController agent)
        {
            rb = agent.GetComponent<Rigidbody2D>();

            if (clampToHomeBounds && agent.HomeBounds != null)
                agent.transform.position = agent.HomeBounds.ClampToBounds(agent.transform.position);
        }

        public void Tick(AgentController agent, float deltaTime)
        {
            if (zeroRigidbodyVelocity && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (!clampToHomeBounds || agent.HomeBounds == null)
                return;

            Vector3 clamped = agent.HomeBounds.ClampToBounds(agent.transform.position);

            if (rb != null)
                rb.position = clamped;
            else
                agent.transform.position = clamped;
        }
    }
}