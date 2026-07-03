using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Movement/Swim Wander Within Home Bounds")]
public class SwimWanderWithinHomeBoundsMovementDefinition : AgentMovementDefinition
{
    [SerializeField] private float speed = 0.7f;
    [SerializeField] private float verticalWobble = 0.25f;
    [SerializeField] private float turnIntervalMin = 1.5f;
    [SerializeField] private float turnIntervalMax = 4f;

    public override IAgentMovementRuntime CreateRuntime()
    {
        return new SwimWanderRuntime(speed, verticalWobble, turnIntervalMin, turnIntervalMax);
    }

    private sealed class SwimWanderRuntime : IAgentMovementRuntime
    {
        private readonly float speed;
        private readonly float verticalWobble;
        private readonly float turnIntervalMin;
        private readonly float turnIntervalMax;

        private Rigidbody2D rb;
        private Vector2 direction;
        private float nextTurnTime;

        public SwimWanderRuntime(float speed, float verticalWobble, float turnIntervalMin, float turnIntervalMax)
        {
            this.speed = Mathf.Max(0f, speed);
            this.verticalWobble = Mathf.Max(0f, verticalWobble);
            this.turnIntervalMin = Mathf.Max(0.1f, turnIntervalMin);
            this.turnIntervalMax = Mathf.Max(this.turnIntervalMin, turnIntervalMax);
        }

        public void Initialize(AgentController agent)
        {
            rb = agent.GetComponent<Rigidbody2D>();
            PickNewDirection();
        }

        public void Tick(AgentController agent, float deltaTime)
        {
            if (Time.time >= nextTurnTime)
                PickNewDirection();

            Vector3 next = agent.transform.position + (Vector3)(direction * speed * deltaTime);

            if (agent.HomeBounds != null && agent.HomeBounds.HasBounds)
            {
                Bounds b = agent.HomeBounds.WorldBounds;

                if (next.x < b.min.x || next.x > b.max.x)
                {
                    direction.x *= -1f;
                    next.x = Mathf.Clamp(next.x, b.min.x, b.max.x);
                }

                if (next.y < b.min.y || next.y > b.max.y)
                {
                    direction.y *= -1f;
                    next.y = Mathf.Clamp(next.y, b.min.y, b.max.y);
                }
            }

            if (rb != null)
                rb.MovePosition(next);
            else
                agent.transform.position = next;

            FaceMoveDirection(agent);
        }

        private void PickNewDirection()
        {
            float x = Random.value < 0.5f ? -1f : 1f;
            float y = Random.Range(-verticalWobble, verticalWobble);

            direction = new Vector2(x, y).normalized;
            nextTurnTime = Time.time + Random.Range(turnIntervalMin, turnIntervalMax);
        }

        private void FaceMoveDirection(AgentController agent)
        {
            if (Mathf.Abs(direction.x) < 0.01f)
                return;

            Vector3 scale = agent.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction.x);
            agent.transform.localScale = scale;
        }
    }
}