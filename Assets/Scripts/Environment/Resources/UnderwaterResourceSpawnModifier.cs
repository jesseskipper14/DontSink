using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class UnderwaterResourceSpawnModifier
{
    [Header("Identity")]
    public string modifierId;

    [Header("Budget")]
    public UnderwaterResourceCategory affectedCategory = UnderwaterResourceCategory.Collectable;

    [Min(0)]
    public int extraSpawnBudget = 0;

    [Header("Weighting")]
    [Tooltip("If true, categoryWeightMultiplier applies to every resource in affectedCategory.")]
    public bool applyCategoryMultiplier = true;

    [Min(0f)]
    public float categoryWeightMultiplier = 1f;

    [Tooltip("Resources with these tags get tagWeightMultiplier.")]
    public List<string> boostedTags = new();

    [Min(0f)]
    public float tagWeightMultiplier = 1f;

    public int GetExtraBudget(UnderwaterResourceCategory category)
    {
        return category == affectedCategory ? extraSpawnBudget : 0;
    }

    public float GetWeightMultiplier(UnderwaterResourceDefinition definition)
    {
        if (definition == null)
            return 1f;

        float multiplier = 1f;

        if (applyCategoryMultiplier && definition.category == affectedCategory)
            multiplier *= categoryWeightMultiplier;

        if (boostedTags != null && boostedTags.Count > 0)
        {
            for (int i = 0; i < boostedTags.Count; i++)
            {
                if (definition.HasTag(boostedTags[i]))
                {
                    multiplier *= tagWeightMultiplier;
                    break;
                }
            }
        }

        return Mathf.Max(0f, multiplier);
    }
}