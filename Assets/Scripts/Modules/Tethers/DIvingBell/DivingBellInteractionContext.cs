using UnityEngine;

/// <summary>
/// Per-player interaction context while occupying a diving bell.
///
/// This does not globally disable layers or interactions. It filters only the
/// Interactor2D belonging to this player, which keeps simultaneous occupants,
/// bells, boats, and future multiplayer clients independent.
/// </summary>
[DisallowMultipleComponent]
public sealed class DivingBellInteractionContext :
    MonoBehaviour,
    IInteractionTargetFilter
{
    [Header("Runtime Debug")]
    [SerializeField] private DivingBellOccupancy currentBell;

    public DivingBellOccupancy CurrentBell => currentBell;
    public bool IsActive => currentBell != null;

    public void SetBell(DivingBellOccupancy bell)
    {
        currentBell = bell;
        RefreshInteractorFilters();
    }

    public void ClearBell(DivingBellOccupancy bell)
    {
        if (bell != null && !ReferenceEquals(currentBell, bell))
            return;

        currentBell = null;
        RefreshInteractorFilters();
    }

    public bool AllowsInteractionTarget(
        MonoBehaviour targetOwner,
        in InteractContext context)
    {
        if (currentBell == null)
            return true;

        if (targetOwner == null)
            return false;

        Transform targetTransform = targetOwner.transform;
        Transform bellTransform = currentBell.transform;

        // Fixtures/controls/door interactions authored on this exact bell.
        if (targetTransform == bellTransform ||
            targetTransform.IsChildOf(bellTransform))
        {
            return true;
        }

        // Loose BellItems are intentionally NOT parented to the bell. Their
        // physical containment component is the authoritative relationship.
        DivingBellContainedItem contained =
            targetOwner.GetComponent<DivingBellContainedItem>() ??
            targetOwner.GetComponentInParent<DivingBellContainedItem>();

        if (contained != null &&
            contained.IsContainedInBell &&
            ReferenceEquals(contained.CurrentBell, currentBell))
        {
            return true;
        }

        return false;
    }

    private void OnEnable()
    {
        RefreshInteractorFilters();
    }

    private void OnDisable()
    {
        RefreshInteractorFilters();
    }

    private void RefreshInteractorFilters()
    {
        Interactor2D[] interactors =
            GetComponentsInChildren<Interactor2D>(true);

        for (int i = 0; i < interactors.Length; i++)
        {
            if (interactors[i] != null)
                interactors[i].RefreshInteractionTargetFilters();
        }
    }
}
