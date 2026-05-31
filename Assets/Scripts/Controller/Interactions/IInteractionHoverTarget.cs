using UnityEngine;

public readonly struct InteractionHoverTarget
{
    public readonly Collider2D SourceCollider;
    public readonly MonoBehaviour Owner;

    public readonly IInteractable Interact;
    public readonly IPickupInteractable Pickup;
    public readonly IUnsecureInteractable Unsecure;
    public readonly IToggleInteractable Toggle;

    public readonly IInteractPromptProvider PromptProvider;
    public readonly IPickupPromptProvider PickupPromptProvider;
    public readonly IInteractPromptActionProvider ActionProvider;

    public readonly IInteractionLabelProvider LabelProvider;
    public readonly IInteractionRangeProvider RangeProvider;

    public bool IsValid => Owner != null || SourceCollider != null;

    public InteractionHoverTarget(
        Collider2D sourceCollider,
        MonoBehaviour owner,
        IInteractable interact,
        IPickupInteractable pickup,
        IUnsecureInteractable unsecure,
        IToggleInteractable toggle,
        IInteractPromptProvider promptProvider,
        IPickupPromptProvider pickupPromptProvider,
        IInteractPromptActionProvider actionProvider,
        IInteractionLabelProvider labelProvider,
        IInteractionRangeProvider rangeProvider)
    {
        SourceCollider = sourceCollider;
        Owner = owner;

        Interact = interact;
        Pickup = pickup;
        Unsecure = unsecure;
        Toggle = toggle;

        PromptProvider = promptProvider;
        PickupPromptProvider = pickupPromptProvider;
        ActionProvider = actionProvider;

        LabelProvider = labelProvider;
        RangeProvider = rangeProvider;
    }
}