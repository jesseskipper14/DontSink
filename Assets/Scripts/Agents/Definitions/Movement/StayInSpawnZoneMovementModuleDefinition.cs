using UnityEngine;

[CreateAssetMenu(
    menuName = "Agents/Movement Modules/Stay In Spawn Zone",
    fileName = "StayInSpawnZoneMovementModule")]
public sealed class StayInSpawnZoneMovementModuleDefinition : AgentMovementModuleDefinition
{
    [Header("Return")]
    [SerializeField, Min(0f)] private float returnSpeed = 3f;

    [Tooltip("When outside the zone, suppress lower priority movement.")]
    [SerializeField] private bool suppressLowerPriorityWhenOutside = true;

    public override IAgentMovementModuleRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : IAgentMovementModuleRuntime
    {
        private readonly StayInSpawnZoneMovementModuleDefinition def;

        public Runtime(StayInSpawnZoneMovementModuleDefinition def)
        {
            this.def = def;
        }

        public void Initialize(AgentController agent, AgentMovementModuleSlot slot)
        {
        }

        public AgentMovementIntent Evaluate(
            AgentMovementBlackboard blackboard,
            AgentMovementModuleSlot slot,
            float dt)
        {
            FishSchoolMember2D member = blackboard?.fishSchoolMember;
            if (member == null || member.Zone == null)
                return AgentMovementIntent.None("StayInZone no zone");

            Vector2 pos = blackboard.Position;

            if (member.Zone.ContainsWorldPoint(pos))
                return AgentMovementIntent.None("StayInZone inside");

            Vector2 clamped = member.Zone.ClampWorldPoint(pos);
            Vector2 toInside = clamped - pos;

            if (toInside.sqrMagnitude <= 0.0001f)
                return AgentMovementIntent.None("StayInZone clamped");

            Vector2 desired = toInside.normalized * def.returnSpeed;

            return AgentMovementIntent.Velocity(
                desired,
                slot.Strength,
                slot.Priority,
                suppressLowerPriority: def.suppressLowerPriorityWhenOutside,
                debugLabel: "StayInSpawnZone");
        }
    }
}