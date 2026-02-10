namespace WorldMap.Player.StarMap
{
    /// <summary>
    /// Player-facing knowledge states. UI should render based on this, never infer from numbers.
    /// </summary>
    public enum RouteKnowledgeState
    {
        Unknown = 0,  // No known route; typically not visible.
        Rumored = 1,  // You suspect a route exists; visible as faint/dashed.
        Partial = 2,  // Route is charted but incomplete/uncertain.
        Known = 3     // Fully charted; travelable (for your current phase).
    }
}
