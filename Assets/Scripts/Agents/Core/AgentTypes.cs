using System;
using UnityEngine;

public enum AgentKind
{
    TownNpc = 0,
    Creature = 1,
    Other = 2
}

public enum AgentServiceKind
{
    None = 0,
    MarketTrade = 10,
    ItemVendor = 20,
    BoatVendorPlaceholder = 30,
    DialogueOnly = 40
}

[Serializable]
public struct AgentRuntimeSnapshot
{
    public string stableId;
    public string definitionId;
    public string nodeId;
    public AgentKind kind;
    public bool removed;
}

public readonly struct AgentServiceContext
{
    public readonly AgentController Agent;
    public readonly AgentServiceDefinition Service;
    public readonly InteractContext InteractContext;

    public GameObject Actor => InteractContext.InteractorGO;
    public Transform ActorTransform => InteractContext.InteractorTransform;

    public AgentServiceContext(
        AgentController agent,
        AgentServiceDefinition service,
        in InteractContext interactContext)
    {
        Agent = agent;
        Service = service;
        InteractContext = interactContext;
    }
}

public struct AgentServiceResult
{
    public bool handled;
    public string message;

    public static AgentServiceResult Handled(string message = null)
    {
        return new AgentServiceResult
        {
            handled = true,
            message = message
        };
    }

    public static AgentServiceResult Unhandled(string message = null)
    {
        return new AgentServiceResult
        {
            handled = false,
            message = message
        };
    }
}