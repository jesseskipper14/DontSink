using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CelestialConstellationEdge
{
    public string fromStarStableId;
    public string toStarStableId;

    public CelestialConstellationEdge(string fromStarStableId, string toStarStableId)
    {
        this.fromStarStableId = fromStarStableId;
        this.toStarStableId = toStarStableId;
    }
}

/// <summary>
/// Deterministic constellation world truth. The generated name is truth metadata, not automatically
/// player-visible knowledge. Player-facing systems should expose the name only after verification.
/// </summary>
[Serializable]
public sealed class CelestialConstellation
{
    [SerializeField] private string stableId;
    [SerializeField] private string truthName;
    [SerializeField] private Vector2 worldCentroid;
    [SerializeField] private List<string> memberStarStableIds = new();
    [SerializeField] private List<CelestialConstellationEdge> edges = new();

    public string StableId => stableId;
    public string TruthName => truthName;
    public Vector2 WorldCentroid => worldCentroid;
    public IReadOnlyList<string> MemberStarStableIds => memberStarStableIds;
    public IReadOnlyList<CelestialConstellationEdge> Edges => edges;

    internal CelestialConstellation(
        string stableId,
        string truthName,
        Vector2 worldCentroid,
        List<string> memberStarStableIds,
        List<CelestialConstellationEdge> edges)
    {
        this.stableId = stableId;
        this.truthName = truthName;
        this.worldCentroid = worldCentroid;
        this.memberStarStableIds = memberStarStableIds ?? new List<string>();
        this.edges = edges ?? new List<CelestialConstellationEdge>();
    }

    public bool ContainsStar(string starStableId)
    {
        if (string.IsNullOrWhiteSpace(starStableId) || memberStarStableIds == null)
            return false;

        for (int i = 0; i < memberStarStableIds.Count; i++)
        {
            if (memberStarStableIds[i] == starStableId)
                return true;
        }

        return false;
    }
}
