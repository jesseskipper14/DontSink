public interface IInteractionLabelProvider
{
    string GetInteractionLabel(in InteractContext context);
}

/// <summary>
/// Supplies an optional secondary hover/detail line for the common interaction prompt.
/// Examples: wetness, lock state, damage state, power state, contamination, etc.
/// </summary>
public interface IInteractionDetailProvider
{
    bool TryGetInteractionDetail(
        in InteractContext context,
        out string detail);
}

public interface IInteractionRangeProvider
{
    bool TryGetHoverNameRange(out float range);
    bool TryGetActionRange(out float range);
}

public interface IInteractionPromptDisplayPolicyProvider
{
    bool ShouldShowHoverLabel(in InteractContext context);
}

/// <summary>
/// Supplies an optional player-facing reason why a currently hovered pickup
/// cannot be acquired. This keeps inventory/equipment rules out of prompt UI.
/// </summary>
public interface IPickupBlockReasonProvider
{
    bool TryGetPickupBlockReason(
        in InteractContext context,
        out string reason);
}
