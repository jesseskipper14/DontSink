public interface IUnsecureInteractable
{
    bool CanUnsecure(in InteractContext context);
    void Unsecure(in InteractContext context);
}