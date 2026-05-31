public interface IInteractionLabelProvider
{
    string GetInteractionLabel(in InteractContext context);
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