using UnityEngine;

public readonly struct AgentMovementIntent
{
    public readonly bool hasIntent;
    public readonly Vector2 desiredVelocity;
    public readonly float weight;
    public readonly int priority;
    public readonly bool suppressLowerPriority;
    public readonly string debugLabel;

    public readonly float maxSpeedOverride;
    public readonly float accelerationOverride;

    private AgentMovementIntent(
        bool hasIntent,
        Vector2 desiredVelocity,
        float weight,
        int priority,
        bool suppressLowerPriority,
        string debugLabel,
        float maxSpeedOverride,
        float accelerationOverride)
    {
        this.hasIntent = hasIntent;
        this.desiredVelocity = desiredVelocity;
        this.weight = Mathf.Max(0f, weight);
        this.priority = priority;
        this.suppressLowerPriority = suppressLowerPriority;
        this.debugLabel = debugLabel;

        this.maxSpeedOverride = Mathf.Max(0f, maxSpeedOverride);
        this.accelerationOverride = Mathf.Max(0f, accelerationOverride);
    }

    public static AgentMovementIntent None(string debugLabel = null)
    {
        return new AgentMovementIntent(
            false,
            Vector2.zero,
            0f,
            int.MinValue,
            false,
            debugLabel,
            0f,
            0f);
    }

    public static AgentMovementIntent Velocity(
        Vector2 desiredVelocity,
        float weight,
        int priority,
        bool suppressLowerPriority = false,
        string debugLabel = null,
        float maxSpeedOverride = 0f,
        float accelerationOverride = 0f)
    {
        if (weight <= 0f || desiredVelocity.sqrMagnitude <= 0.000001f)
            return None(debugLabel);

        return new AgentMovementIntent(
            true,
            desiredVelocity,
            weight,
            priority,
            suppressLowerPriority,
            debugLabel,
            maxSpeedOverride,
            accelerationOverride);
    }
}