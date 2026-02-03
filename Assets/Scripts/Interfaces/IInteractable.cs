using UnityEngine;

public interface IInteractable
{
    /// Higher wins. Use this to prefer chairs over random colliders, etc.
    int InteractionPriority { get; }

    /// Called by the world-authoritative interactor to validate interaction.
    bool CanInteract(in InteractContext context);

    /// Perform the interaction.
    void Interact(in InteractContext context);
}

/// Minimal context, easy to extend later (MP identity, inventory, etc.)
public readonly struct InteractContext
{
    public readonly GameObject InteractorGO;
    public readonly Transform InteractorTransform;
    public readonly Vector2 Origin;
    public readonly Vector2 AimDir;

    public InteractContext(GameObject interactorGO, Transform interactorTransform, Vector2 origin, Vector2 aimDir)
    {
        InteractorGO = interactorGO;
        InteractorTransform = interactorTransform;
        Origin = origin;
        AimDir = aimDir.sqrMagnitude > 0.0001f ? aimDir.normalized : Vector2.right;
    }
}
