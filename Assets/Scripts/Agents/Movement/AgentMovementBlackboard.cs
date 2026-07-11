using UnityEngine;

public sealed class AgentMovementBlackboard
{
    public AgentController agent;
    public Rigidbody2D rb;
    public FishSchoolMember2D fishSchoolMember;

    public Vector2 Position
    {
        get
        {
            if (rb != null)
                return rb.position;

            if (agent != null)
                return agent.transform.position;

            return Vector2.zero;
        }
    }
}