public interface ILinkInteractable
{
    bool CanLink(in InteractContext context);
    string GetLinkPromptVerb(in InteractContext context);
    void Link(in InteractContext context);
}