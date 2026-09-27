using UnityEngine;

/// <summary>
/// Opt-in contract for ordinary Interact actions that require a sustained hold
/// instead of executing on the initial InteractPressed frame.
///
/// Interactor2D owns timing and one-shot latching. The interactable remains
/// authoritative for CanInteract and the eventual Interact call.
/// </summary>
public interface IHoldInteractable
{
    float GetInteractionHoldDuration(
        in InteractContext context);
}
