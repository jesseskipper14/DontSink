using UnityEngine;

public struct InteractionIntent
{
    public bool InteractPressed;
    public bool InteractDoublePressed;

    public bool PickupPressed;
    public bool PickupHeld;
    public bool PickupReleased;

    public bool TogglePressed;

    public Vector2 AimWorld;
    public bool HasAimWorld;
}

public interface IPickupInteractable
{
    int PickupPriority { get; }
    PickupInteractionMode PickupMode { get; }
    float PickupHoldDuration { get; }
    bool CanPickup(in InteractContext context);
    void Pickup(in InteractContext context);
}

public interface IInteractionIntentSource
{
    InteractionIntent Current { get; }
}