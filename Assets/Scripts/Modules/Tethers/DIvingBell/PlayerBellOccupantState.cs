using UnityEngine;

/// <summary>
/// Player-local state describing occupancy of a diving bell.
///
/// This is intentionally separate from PlayerBoardingState:
///
///     PlayerBoardingState = "which BOAT am I currently aboard?"
///     PlayerBellOccupantState = "which DIVING BELL am I currently inside?"
///
/// While the bell is docked, both may be true at the same time.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBellOccupantState :
    MonoBehaviour
{
    [Header("Runtime Debug")]
    [SerializeField] private DivingBellOccupancy currentBell;
    [SerializeField] private Transform parentBeforeBell;

    public bool IsInsideBell =>
        currentBell != null;

    public DivingBellOccupancy CurrentBell =>
        currentBell;

    public Transform ParentBeforeBell =>
        parentBeforeBell;

    public bool IsInside(
        DivingBellOccupancy bell)
    {
        return
            bell != null &&
            ReferenceEquals(
                currentBell,
                bell);
    }

    /// <summary>
    /// Called only by DivingBellOccupancy.
    /// Captures the player's current hierarchy parent before the bell reparents
    /// the player into its moving frame.
    /// </summary>
    public bool TryBeginOccupancy(
        DivingBellOccupancy bell)
    {
        if (bell == null)
            return false;

        if (currentBell != null &&
            !ReferenceEquals(
                currentBell,
                bell))
        {
            return false;
        }

        if (currentBell == null)
        {
            parentBeforeBell =
                transform.parent;
        }

        currentBell =
            bell;

        return true;
    }

    /// <summary>
    /// Called only by DivingBellOccupancy.
    /// Returns the hierarchy parent that should normally be restored.
    /// </summary>
    public Transform EndOccupancy(
        DivingBellOccupancy bell)
    {
        if (bell == null ||
            !ReferenceEquals(
                currentBell,
                bell))
        {
            return null;
        }

        Transform restoreParent =
            parentBeforeBell;

        currentBell =
            null;

        parentBeforeBell =
            null;

        return restoreParent;
    }

    /// <summary>
    /// Defensive cleanup for destruction/scene teardown.
    /// Does not move the player.
    /// </summary>
    public void ClearIfOwnedBy(
        DivingBellOccupancy bell)
    {
        if (bell == null ||
            !ReferenceEquals(
                currentBell,
                bell))
        {
            return;
        }

        currentBell =
            null;

        parentBeforeBell =
            null;
    }
}
