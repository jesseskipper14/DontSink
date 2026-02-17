using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Trade/Node Archetype Def", fileName = "NodeArchetype_")]
public sealed class NodeArchetypeDef : ScriptableObject
{
    [Header("Identity")]
    public string archetypeId = "fishing_hamlet";
    public string displayName = "Fishing Hamlet";

    [Header("Baseline pressure biases")]
    [Tooltip("Baseline resource pressure tendencies this node has regardless of cluster.")]
    public List<ResourcePressureBias> pressureBiases = new List<ResourcePressureBias>();

    [Header("Market style")]
    [Range(0f, 1f)]
    [Tooltip("0 = boring staples, 1 = exotic/odd offers.")]
    public float exoticLean01 = 0.1f;

    [Range(0.5f, 2f)]
    [Tooltip("Scales overall offer quantities for this node type.")]
    public float volumeMultiplier = 1f;
}
