using System;
using UnityEngine;

[Serializable]
public sealed class UnderwaterResourceRuntimeInstance
{
    public string instanceId;
    public string definitionStableId;

    public Vector2 worldPosition;

    public float depth;
    public float quality01;

    public int remainingCharges;

    public bool discovered;
    public bool depleted;

    public static UnderwaterResourceRuntimeInstance Create(
        string instanceId,
        UnderwaterResourceDefinition definition,
        Vector2 worldPosition,
        float depth,
        float quality01)
    {
        return new UnderwaterResourceRuntimeInstance
        {
            instanceId = instanceId,
            definitionStableId = definition != null ? definition.stableId : string.Empty,
            worldPosition = worldPosition,
            depth = depth,
            quality01 = Mathf.Clamp01(quality01),
            remainingCharges = definition != null ? Mathf.Max(1, definition.charges) : 1,
            discovered = false,
            depleted = false
        };
    }
}