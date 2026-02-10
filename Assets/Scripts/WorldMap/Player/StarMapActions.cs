using WorldMap.Player.StarMap;

public static class StarMapActions
{
    public static bool TryUnlockRoute(WorldMapPlayerState player, string fromNodeId, string toNodeId)
    {
        if (player == null) return false;
        if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId)) return false;
        if (fromNodeId == toNodeId) return false;

        // simple: require one star map token item
        if (!player.inventory.Remove("item.star_map", 1))
            return false;

        // Ensure star map exists (additive, safe)
        player.starMap ??= new PlayerStarMapState();

        // 1) Update star map knowledge: force Known
        var routeKey = RouteKey.Make(fromNodeId, toNodeId);
        var svc = new StarMapService(player.starMap);
        svc.ForceState(routeKey, RouteKnowledgeState.Known);

        // 2) Compatibility bridge for current travel implementation:
        // Travel currently gates cross-cluster routes using unlockedRoutes.
        // Intra-cluster is known by default, so no need to add keys for those.
        //
        // We don't know clusters here (only stableIds), so we always add the key.
        // This is safe because intra-cluster keys are harmless and keep behavior consistent.
        player.unlockedRoutes.Add(routeKey);

        return true;
    }
}
