using System;
using UnityEngine;

[Serializable]
public sealed class AgentMovementModuleSlot
{
    [SerializeField] private bool enabled = true;

    [SerializeField] private AgentMovementModuleDefinition module;

    [Tooltip("Higher priority wins when a module suppresses lower-priority movement.")]
    [SerializeField] private int priority = 0;

    [Tooltip("Final contribution strength for this module in this movement profile.")]
    [SerializeField, Min(0f)] private float strength = 1f;

    public bool Enabled => enabled;
    public AgentMovementModuleDefinition Module => module;
    public int Priority => priority;
    public float Strength => Mathf.Max(0f, strength);

    public string ModuleLabel =>
        module != null ? module.name : "<missing module>";
}