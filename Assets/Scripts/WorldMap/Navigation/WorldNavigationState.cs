using System;
using UnityEngine;

/// <summary>
/// Shared authoritative world-navigation truth.
///
/// This is deliberately NOT player map knowledge or believed position.
/// It is the simulation's best authoritative answer to "where is the boat in
/// continuous world-map space right now?"
///
/// Future charting/navigation work may add a separate player/crew belief state.
/// Do not expose this value to ordinary map UI as an automatic player marker.
/// </summary>
[Serializable]
public sealed class WorldNavigationState
{
    [SerializeField] private bool hasTrueWorldPosition;
    [SerializeField] private Vector2 trueWorldPosition;
    [SerializeField] private WorldNavigationPositionSource truePositionSource;
    [SerializeField] private string anchorNodeStableId;
    [SerializeField] private int revision;

    public bool HasTrueWorldPosition => hasTrueWorldPosition;
    public Vector2 TrueWorldPosition => trueWorldPosition;
    public WorldNavigationPositionSource TruePositionSource => truePositionSource;
    public string AnchorNodeStableId => anchorNodeStableId;
    public int Revision => revision;

    internal void ApplyTrueWorldPosition(
        Vector2 position,
        WorldNavigationPositionSource source,
        string anchorNodeId)
    {
        string normalizedAnchor =
            anchorNodeId ?? string.Empty;

        bool unchanged =
            hasTrueWorldPosition &&
            (trueWorldPosition - position).sqrMagnitude <= 0.0000000001f &&
            truePositionSource == source &&
            string.Equals(
                anchorNodeStableId ?? string.Empty,
                normalizedAnchor,
                StringComparison.Ordinal);

        if (unchanged)
            return;

        hasTrueWorldPosition = true;
        trueWorldPosition = position;
        truePositionSource = source;
        anchorNodeStableId = normalizedAnchor;
        revision++;
    }

    internal void ClearTrueWorldPosition()
    {
        hasTrueWorldPosition = false;
        trueWorldPosition = Vector2.zero;
        truePositionSource = WorldNavigationPositionSource.Unknown;
        anchorNodeStableId = string.Empty;
        revision++;
    }
}

public enum WorldNavigationPositionSource
{
    Unknown = 0,

    /// <summary>
    /// Exact source-node position at voyage start.
    /// </summary>
    TravelStart = 1,

    /// <summary>
    /// Continuous BoatScene projection from local piloting navigation space.
    /// </summary>
    TravelProjection = 2,

    /// <summary>
    /// Exact node position applied when the simulation reaches/returns to a node.
    /// This is world truth; whether the player is allowed to know it is a separate rule.
    /// </summary>
    NodeArrival = 3,

    /// <summary>
    /// State supplied by a future network replication layer.
    /// </summary>
    Replicated = 4,

    Debug = 5
}
