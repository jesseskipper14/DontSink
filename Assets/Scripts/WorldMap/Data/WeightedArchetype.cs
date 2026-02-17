using System;
using UnityEngine;

[Serializable]
public struct WeightedArchetype
{
    public NodeArchetypeDef archetype;

    [Min(0f)]
    public float weight;
}
