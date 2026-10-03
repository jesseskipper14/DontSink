using UnityEngine;

/// <summary>
/// Narrow access seam for shared continuous world position.
///
/// Consequential local simulation writes are authority-gated. A future networking
/// layer may use ApplyReplicatedTrueWorldPosition to apply host state on clients.
/// Rendering/query systems should normally use TryGetTrueWorldPosition only.
/// </summary>
public static class WorldNavigationService
{
    public static bool TryGetTrueWorldPosition(out Vector2 position)
    {
        position = Vector2.zero;

        WorldNavigationState state = ResolveState();
        if (state == null || !state.HasTrueWorldPosition)
            return false;

        position = WorldTopologyService.Normalize(state.TrueWorldPosition);
        return true;
    }

    public static WorldNavigationState State => ResolveState();

    public static bool TrySetAuthoritativeTrueWorldPosition(
        Vector2 position,
        WorldNavigationPositionSource source,
        string anchorNodeStableId = null)
    {
        if (!GameplayAuthority.IsAuthoritative)
            return false;

        var topology = WorldTopologyService.Current;
        if (!WorldTopology.IsFinite(position.x) || !WorldTopology.IsFinite(position.y) ||
            topology.IsValid && !topology.ContainsY(position.y)) return false;

        WorldNavigationState state = ResolveState();
        if (state == null)
            return false;

        state.ApplyTrueWorldPosition(
            position,
            source,
            anchorNodeStableId);

        return true;
    }

    /// <summary>
    /// Explicit state-application seam for a future host replication layer.
    /// This intentionally does not perform a local authority check because applying
    /// already-authoritative replicated state is different from making a decision.
    /// </summary>
    public static bool ApplyReplicatedTrueWorldPosition(
        Vector2 position,
        string anchorNodeStableId = null)
    {
        var topology = WorldTopologyService.Current;
        if (!WorldTopology.IsFinite(position.x) || !WorldTopology.IsFinite(position.y) ||
            topology.IsValid && !topology.ContainsY(position.y)) return false;
        WorldNavigationState state = ResolveState();
        if (state == null)
            return false;

        state.ApplyTrueWorldPosition(
            position,
            WorldNavigationPositionSource.Replicated,
            anchorNodeStableId);

        return true;
    }

    public static bool TryClearAuthoritativeTrueWorldPosition()
    {
        if (!GameplayAuthority.IsAuthoritative)
            return false;

        WorldNavigationState state = ResolveState();
        if (state == null)
            return false;

        state.ClearTrueWorldPosition();
        return true;
    }

    private static WorldNavigationState ResolveState()
    {
        GameState gs = GameState.I;
        if (gs == null)
            return null;

        if (gs.worldNavigation == null)
            gs.worldNavigation = new WorldNavigationState();

        return gs.worldNavigation;
    }
}
