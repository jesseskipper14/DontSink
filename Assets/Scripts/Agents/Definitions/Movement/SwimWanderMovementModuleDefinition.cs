using UnityEngine;

[CreateAssetMenu(
    menuName = "Agents/Movement Modules/Swim Wander",
    fileName = "SwimWanderMovementModule")]
public sealed class SwimWanderMovementModuleDefinition : AgentMovementModuleDefinition
{
    [Header("Wander")]
    [SerializeField, Min(0f)] private float speed = 1.1f;
    [SerializeField] private Vector2 retargetIntervalRange = new Vector2(2f, 5f);
    [SerializeField, Min(0.05f)] private float reachDistance = 0.45f;

    [Header("Fallback Area")]
    [Tooltip("Used only if the fish has no AgentSpawnZone.")]
    [SerializeField, Min(0.1f)] private float fallbackRadius = 3f;

    [Header("Noise")]
    [SerializeField, Min(0f)] private float noiseStrength = 0.25f;
    [SerializeField, Min(0.01f)] private float noiseFrequency = 1.1f;

    public override IAgentMovementModuleRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : IAgentMovementModuleRuntime
    {
        private readonly SwimWanderMovementModuleDefinition def;

        private Vector2 target;
        private Vector2 origin;
        private Vector2 noiseSeed;
        private float nextRetargetTime;
        private bool hasTarget;

        public Runtime(SwimWanderMovementModuleDefinition def)
        {
            this.def = def;
        }

        public void Initialize(AgentController agent, AgentMovementModuleSlot slot)
        {
            origin = agent != null ? (Vector2)agent.transform.position : Vector2.zero;
            noiseSeed = Random.insideUnitCircle * 100f;
            hasTarget = false;
            nextRetargetTime = 0f;
        }

        public AgentMovementIntent Evaluate(
            AgentMovementBlackboard blackboard,
            AgentMovementModuleSlot slot,
            float dt)
        {
            if (blackboard == null || blackboard.agent == null)
                return AgentMovementIntent.None("SwimWander missing blackboard");

            Vector2 pos = blackboard.Position;
            AgentSpawnZone zone = blackboard.fishSchoolMember != null
                ? blackboard.fishSchoolMember.Zone
                : null;

            if (!hasTarget ||
                Time.time >= nextRetargetTime ||
                Vector2.Distance(pos, target) <= def.reachDistance)
            {
                target = PickTarget(zone, pos);
                hasTarget = true;
                nextRetargetTime = Time.time + GetRetargetInterval();
            }

            Vector2 toTarget = target - pos;

            if (toTarget.sqrMagnitude <= 0.0001f)
                return AgentMovementIntent.None("SwimWander reached");

            Vector2 desired =
                toTarget.normalized * def.speed +
                GetNoise() * def.noiseStrength;

            return AgentMovementIntent.Velocity(
                desired,
                slot.Strength,
                slot.Priority,
                suppressLowerPriority: false,
                debugLabel: "SwimWander");
        }

        private Vector2 PickTarget(AgentSpawnZone zone, Vector2 currentPos)
        {
            if (zone != null)
                return zone.GetRandomPoint(null);

            return origin + Random.insideUnitCircle * def.fallbackRadius;
        }

        private float GetRetargetInterval()
        {
            float min = Mathf.Max(0.1f, Mathf.Min(def.retargetIntervalRange.x, def.retargetIntervalRange.y));
            float max = Mathf.Max(min, Mathf.Max(def.retargetIntervalRange.x, def.retargetIntervalRange.y));

            return Random.Range(min, max);
        }

        private Vector2 GetNoise()
        {
            float t = Time.time * def.noiseFrequency;

            float x = Mathf.PerlinNoise(noiseSeed.x, t) - 0.5f;
            float y = Mathf.PerlinNoise(noiseSeed.y, t + 19.71f) - 0.5f;

            return new Vector2(x, y);
        }
    }
}