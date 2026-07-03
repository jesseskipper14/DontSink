using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Behavior Set")]
public class AgentBehaviorSetDefinition : ScriptableObject
{
    [SerializeField] private string id = "behavior_new";

    [Header("Runtime Pieces")]
    [SerializeField] private AgentBrainDefinition brain;
    [SerializeField] private AgentMovementDefinition movement;
    [SerializeField] private AgentInteractionDefinition interaction;

    public string Id => id;
    public AgentBrainDefinition Brain => brain;
    public AgentMovementDefinition Movement => movement;
    public AgentInteractionDefinition Interaction => interaction;
}