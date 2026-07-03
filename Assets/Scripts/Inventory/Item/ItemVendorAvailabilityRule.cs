using System;
using UnityEngine;

[Serializable]
public sealed class ItemVendorAvailabilityRule
{
    [Header("Availability")]
    [Tooltip("If false, this item ignores node restrictions and can appear anywhere the vendor stocks it.")]
    public bool useNodeRestrictions;

    [Header("Allowed Node Archetypes")]
    [Tooltip("Optional whitelist. If empty, any node archetype is allowed unless blocked below.")]
    public NodeArchetypeDef[] allowedNodeArchetypes;

    [Header("Blocked Node Archetypes")]
    [Tooltip("Optional blacklist. These node archetypes can never sell this item.")]
    public NodeArchetypeDef[] blockedNodeArchetypes;

    [Header("Node Stat Requirements")]
    [Min(0)] public int minProsperity;
    [Min(0)] public int minTradeRating;
    [Min(0)] public int minSecurity;
    [Min(0)] public int minStability;

    public bool HasHardRestrictions =>
        useNodeRestrictions ||
        HasAny(allowedNodeArchetypes) ||
        HasAny(blockedNodeArchetypes) ||
        minProsperity > 0 ||
        minTradeRating > 0 ||
        minSecurity > 0 ||
        minStability > 0;

    private static bool HasAny(NodeArchetypeDef[] defs)
    {
        return defs != null && defs.Length > 0;
    }
}