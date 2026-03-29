public interface IToggleInteractable
{
    bool CanToggle(in InteractContext context);
    void Toggle(in InteractContext context);
}