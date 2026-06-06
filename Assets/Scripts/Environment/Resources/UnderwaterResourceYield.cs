using System;
using UnityEngine;

[Serializable]
public sealed class UnderwaterResourceYield
{
    [Tooltip("The item awarded when this resource is harvested.")]
    public ItemDefinition itemDefinition;

    [Min(1)]
    public int minQuantity = 1;

    [Min(1)]
    public int maxQuantity = 1;

    public int RollQuantity(System.Random rng)
    {
        int min = Mathf.Max(1, minQuantity);
        int max = Mathf.Max(min, maxQuantity);

        return rng.Next(min, max + 1);
    }
}