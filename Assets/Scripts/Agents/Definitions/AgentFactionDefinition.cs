using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Faction Definition")]
public class AgentFactionDefinition : ScriptableObject
{
    [SerializeField] private string id = "faction_new";
    [SerializeField] private string displayName = "New Faction";

    public string Id => id;
    public string DisplayName => displayName;
}