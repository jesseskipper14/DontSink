using System.Collections.Generic;
using UnityEngine;

public interface IAgentMovementDiagnosticsProvider
{
    AgentMovementDiagnosticsSnapshot GetMovementDiagnosticsSnapshot();
}

public sealed class AgentMovementModuleDiagnostic
{
    public int slotIndex;
    public string moduleName;
    public string debugLabel;

    public bool slotEnabled;
    public bool runtimeExists;

    public int priority;
    public float strength;

    public bool hasIntent;
    public bool suppressLowerPriority;
    public bool suppressedByHigherPriority;

    public float intentWeight;
    public Vector2 desiredVelocity;
}

public sealed class AgentMovementDiagnosticsSnapshot
{
    public string agentName;
    public string movementDefinitionName;
    public string runtimeTypeName;

    public Vector2 rawDesiredVelocity;
    public float activeMaxSpeed;
    public float activeAcceleration;

    public bool hasRigidbody;
    public Vector2 currentVelocity;

    public bool hasFinalIntent;
    public Vector2 finalDesiredVelocity;
    public Vector2 appliedVelocity;

    public int suppressPriority = int.MinValue;
    public string activeSummary;

    public readonly List<AgentMovementModuleDiagnostic> modules = new();

    public void Clear()
    {
        agentName = string.Empty;
        movementDefinitionName = string.Empty;
        runtimeTypeName = string.Empty;

        rawDesiredVelocity = Vector2.zero;
        activeMaxSpeed = 0f;
        activeAcceleration = 0f;

        hasRigidbody = false;
        currentVelocity = Vector2.zero;

        hasFinalIntent = false;
        finalDesiredVelocity = Vector2.zero;
        appliedVelocity = Vector2.zero;

        suppressPriority = int.MinValue;
        activeSummary = string.Empty;

        modules.Clear();
    }
}