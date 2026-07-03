using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Movement/Ground Wander Within Home Bounds")]
public sealed class GroundWanderWithinHomeBoundsMovementDefinition : AgentMovementDefinition
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float speed = 0.65f;
    [SerializeField, Min(0f)] private float pauseChance = 0.25f;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float decisionIntervalMin = 1.0f;
    [SerializeField, Min(0.1f)] private float decisionIntervalMax = 3.5f;

    [Header("Facing")]
    [SerializeField] private bool faceMoveDirection = true;

    public override IAgentMovementRuntime CreateRuntime()
    {
        return new GroundWanderRuntime(
            speed,
            pauseChance,
            decisionIntervalMin,
            decisionIntervalMax,
            faceMoveDirection);
    }

    private sealed class GroundWanderRuntime : IAgentMovementRuntime
    {
        private readonly float speed;
        private readonly float pauseChance;
        private readonly float decisionIntervalMin;
        private readonly float decisionIntervalMax;
        private readonly bool faceMoveDirection;

        private Rigidbody2D rb;
        private float direction;
        private float nextDecisionTime;
        private bool paused;

        public GroundWanderRuntime(
            float speed,
            float pauseChance,
            float decisionIntervalMin,
            float decisionIntervalMax,
            bool faceMoveDirection)
        {
            this.speed = Mathf.Max(0f, speed);
            this.pauseChance = Mathf.Clamp01(pauseChance);
            this.decisionIntervalMin = Mathf.Max(0.1f, decisionIntervalMin);
            this.decisionIntervalMax = Mathf.Max(this.decisionIntervalMin, decisionIntervalMax);
            this.faceMoveDirection = faceMoveDirection;
        }

        public void Initialize(AgentController agent)
        {
            rb = agent.GetComponent<Rigidbody2D>();
            PickDecision();
        }

        public void Tick(AgentController agent, float deltaTime)
        {
            if (Time.time >= nextDecisionTime)
                PickDecision();

            if (paused || speed <= 0f)
            {
                StopHorizontalVelocity();
                return;
            }

            Vector3 current = agent.transform.position;
            Vector3 next = current + Vector3.right * (direction * speed * deltaTime);

            AgentHomeBounds bounds = agent.HomeBounds;

            if (bounds != null && bounds.HasBounds)
            {
                Bounds b = bounds.WorldBounds;

                if (next.x < b.min.x)
                {
                    next.x = b.min.x;
                    direction = 1f;
                    ScheduleNextDecision();
                }
                else if (next.x > b.max.x)
                {
                    next.x = b.max.x;
                    direction = -1f;
                    ScheduleNextDecision();
                }
            }

            Move(agent, next);

            if (faceMoveDirection)
                Face(agent, direction);
        }

        private void PickDecision()
        {
            paused = Random.value < pauseChance;

            if (!paused)
                direction = Random.value < 0.5f ? -1f : 1f;

            ScheduleNextDecision();
        }

        private void ScheduleNextDecision()
        {
            nextDecisionTime = Time.time + Random.Range(decisionIntervalMin, decisionIntervalMax);
        }

        private void Move(AgentController agent, Vector3 next)
        {
            if (rb != null)
            {
                rb.MovePosition(next);
                return;
            }

            agent.transform.position = next;
        }

        private void StopHorizontalVelocity()
        {
            if (rb == null)
                return;

            Vector2 v = rb.linearVelocity;
            v.x = 0f;
            rb.linearVelocity = v;
        }

        private void Face(AgentController agent, float dir)
        {
            if (Mathf.Abs(dir) < 0.01f)
                return;

            Vector3 scale = agent.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(dir);
            agent.transform.localScale = scale;
        }
    }
}