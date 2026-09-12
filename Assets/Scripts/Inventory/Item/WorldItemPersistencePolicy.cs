public enum WorldItemPersistencePolicy
{
    // Default value intentionally remains zero so existing ItemDefinition assets
    // preserve current behavior without requiring a migration pass.
    BoatOnly = 0,

    // Survives as an independent WorldItem in the world context where it was left.
    // Use for recoverable important equipment such as anchors / bells / salvage gear.
    PersistentWorld = 1,

    // Never participate in loose-world persistence, even if temporarily boat-owned.
    // Useful for ephemeral/generated objects that should disappear with the scene.
    Never = 2
}
