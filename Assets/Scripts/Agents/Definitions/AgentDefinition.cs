using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Agent Definition")]
public class AgentDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id = "agent_new";
    [SerializeField] private string displayName = "New Agent";
    [SerializeField] private AgentKind kind = AgentKind.TownNpc;
    [SerializeField] private AgentFactionDefinition faction;

    [Header("Prefab")]
    [Tooltip("Optional prefab used by AgentSpawnPoint. The prefab should have AgentController on it.")]
    [SerializeField] private GameObject prefab;

    [Header("Behavior Composition")]
    [SerializeField] private AgentBehaviorSetDefinition behaviorSet;

    [Header("Services")]
    [Tooltip("Services this agent can provide to the player, such as market trade or item vendor.")]
    [SerializeField] private AgentServiceDefinition[] services;

    public string Id => id;
    public string DisplayName => displayName;
    public AgentKind Kind => kind;
    public AgentFactionDefinition Faction => faction;
    public GameObject Prefab => prefab;
    public AgentBehaviorSetDefinition BehaviorSet => behaviorSet;
    public AgentServiceDefinition[] Services => services;
}