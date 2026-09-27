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
    public readonly IInteractionDetailProvider[] DetailProviders;

    // Backward-compatible convenience for any adjacent code that still expects
    // the original single-provider shape.
    public IInteractionDetailProvider DetailProvider =>
        DetailProviders != null && DetailProviders.Length > 0
            ? DetailProviders[0]
            : null;
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
        IInteractionDetailProvider detailProvider,
        IInteractionRangeProvider rangeProvider)
        : this(
            sourceCollider,
            owner,
            interact,
            pickup,
            unsecure,
            toggle,
            promptProvider,
            pickupPromptProvider,
            actionProvider,
            labelProvider,
            detailProvider != null
                ? new[] { detailProvider }
                : null,
            rangeProvider)
    {
    }

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
        IInteractionDetailProvider[] detailProviders,
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
        DetailProviders = detailProviders;
        RangeProvider = rangeProvider;
    }
}