using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Trade/Cluster Affinity Def", fileName = "ClusterAffinity_")]
public sealed class ClusterAffinityDef : ScriptableObject
{
    [Header("Identity")]
    public string affinityId = "food_belt";
    public string displayName = "Food Belt";

    [Header("Archetype weights")]
    [Tooltip("Weighted distribution of node archetypes that can spawn in this cluster.")]
    public List<WeightedArchetype> archetypeWeights = new List<WeightedArchetype>();

    [Header("Market style (optional)")]
    [Range(0f, 1f)]
    public float exoticLean01 = 0.0f;

    [Range(0.5f, 2f)]
    public float volumeMultiplier = 1f;

    [Header("Affinity Selection Weight")]
    [Min(0f)]
    public float baseWeight = 1f;

    public NodeArchetypeDef PickArchetype(System.Random rng)
    {
        if (archetypeWeights == null || archetypeWeights.Count == 0) return null;

        float total = 0f;
        for (int i = 0; i < archetypeWeights.Count; i++)
        {
            var e = archetypeWeights[i];
            if (e.archetype == null || e.weight <= 0f) continue;
            total += e.weight;
        }

        if (total <= 0f) return null;

        float roll = (float)(rng.NextDouble() * total);
        for (int i = 0; i < archetypeWeights.Count; i++)
        {
            var e = archetypeWeights[i];
            if (e.archetype == null || e.weight <= 0f) continue;

            roll -= e.weight;
            if (roll <= 0f) return e.archetype;
        }

        // Fallback (float error)
        for (int i = archetypeWeights.Count - 1; i >= 0; i--)
            if (archetypeWeights[i].archetype != null && archetypeWeights[i].weight > 0f)
                return archetypeWeights[i].archetype;

        return null;
    }
}
