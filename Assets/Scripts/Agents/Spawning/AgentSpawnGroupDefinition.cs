using UnityEngine;

public abstract class AgentSpawnGroupDefinition : ScriptableObject
{
    [Header("Spawn Group")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Lower runs earlier.")]
    [SerializeField] private int priority = 0;

    [SerializeField] private string displayName = "Spawn Group";

    public bool Enabled => enabled;
    public int Priority => priority;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    public abstract void Spawn(AgentSpawnContext context);
}