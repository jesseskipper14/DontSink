using UnityEngine;

[CreateAssetMenu(
    menuName = "Agents/Movement Modules/School Follow",
    fileName = "SchoolFollowMovementModule")]
public sealed class SchoolFollowMovementModuleDefinition : AgentMovementModuleDefinition
{
    [Header("Fallback Values")]
    [SerializeField, Min(0f)] private float fallbackCohesionStrength = 1.5f;
    [SerializeField, Min(0f)] private float fallbackMaxIntentSpeed = 2.2f;
    [SerializeField, Min(0f)] private float fallbackNoiseStrength = 0.25f;
    [SerializeField, Min(0.01f)] private float fallbackNoiseFrequency = 1.1f;

    public override IAgentMovementModuleRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : IAgentMovementModuleRuntime
    {
        private readonly SchoolFollowMovementModuleDefinition def;

        public Runtime(SchoolFollowMovementModuleDefinition def)
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
            if (member == null || member.School == null)
                return AgentMovementIntent.None("SchoolFollow no school");

            FishSchoolController school = member.School;
            CreatureSchoolProfile profile = member.Profile != null
                ? member.Profile
                : school.Profile;

            Vector2 pos = blackboard.Position;

            Vector2 targetSlot =
                school.AnchorPosition +
                member.SchoolOffset +
                member.GetNoiseVector(
                    profile != null ? profile.IndividualNoiseFrequency : def.fallbackNoiseFrequency,
                    profile != null ? profile.IndividualNoiseStrength : def.fallbackNoiseStrength);

            float cohesion = profile != null
                ? profile.CohesionStrength
                : def.fallbackCohesionStrength;

            Vector2 toSlot = targetSlot - pos;

            Vector2 separation = school.GetSeparationVector(member, pos);

            Vector2 desired =
                school.DesiredVelocity +
                toSlot * cohesion +
                separation;

            float maxIntentSpeed = profile != null
                ? profile.MemberMaxSpeed
                : def.fallbackMaxIntentSpeed;

            desired = Vector2.ClampMagnitude(desired, maxIntentSpeed);

            return AgentMovementIntent.Velocity(
                desired,
                slot.Strength,
                slot.Priority,
                suppressLowerPriority: false,
                debugLabel: "SchoolFollow");
        }
    }
}