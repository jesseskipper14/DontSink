using UnityEngine;

public enum FlotationPlacementIntentKind
{
    None = 0,
    TogglePlacement = 1,
    CommitPlacement = 2,
    CancelPlacement = 3
}

/// <summary>
/// Semantic local/client intent for flotation placement.
/// Physical bindings belong in a local adapter, never in gameplay authority.
/// </summary>
public readonly struct FlotationPlacementIntentRequest
{
    public readonly FlotationPlacementIntentKind Kind;
    public readonly Vector2 AimWorld;
    public readonly bool HasAimWorld;

    public FlotationPlacementIntentRequest(
        FlotationPlacementIntentKind kind,
        Vector2 aimWorld,
        bool hasAimWorld)
    {
        Kind = kind;
        AimWorld = aimWorld;
        HasAimWorld = hasAimWorld;
    }

    public static FlotationPlacementIntentRequest Create(
        FlotationPlacementIntentKind kind,
        Vector2 aimWorld = default,
        bool hasAimWorld = false)
    {
        return new FlotationPlacementIntentRequest(
            kind,
            aimWorld,
            hasAimWorld);
    }
}
