using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps diving-bell occupants in the correct BOAT boarding context as the same
/// live physical bell transitions between docked and deployed states.
///
/// Docked:
///     PlayerBellOccupantState remains active.
///     PlayerBoardingState is boarded to the dock's Boat.
///
/// Undocked:
///     PlayerBellOccupantState remains active.
///     PlayerBoardingState is unboarded immediately.
///
/// The player stays parented to the bell in both states. Bell occupancy owns the
/// local physical/collision context; boat boarding only describes whether the
/// occupant is currently physically aboard/supported by the boat.
///
/// This component intentionally observes TetherPayloadDock state rather than
/// winch commands, so cut/release/redock paths share one authority.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DivingBellOccupancy))]
public sealed class DivingBellDeploymentContext :
    MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DivingBellOccupancy occupancy;

    [Header("Runtime Debug")]
    [SerializeField] private bool lastObservedDocked;
    [SerializeField] private bool hasObservedDockState;
    [SerializeField] private Boat resolvedDockBoat;
    [SerializeField] private int reconciledOccupantCount;

    [SerializeField]
    private bool verboseLogging =
        false;

    private readonly List<PlayerBellOccupantState>
        _occupantScratch =
            new List<PlayerBellOccupantState>();

    private void Awake()
    {
        ResolveRefs();
    }

    private void OnEnable()
    {
        ResolveRefs();

        hasObservedDockState =
            false;
    }

    private void LateUpdate()
    {
        ResolveRefs();

        if (occupancy == null)
            return;

        bool docked =
            occupancy.IsDocked;

        bool changed =
            !hasObservedDockState ||
            docked != lastObservedDocked;

        if (changed)
        {
            hasObservedDockState =
                true;

            lastObservedDocked =
                docked;

            if (docked)
                HandleDocked();
            else
                HandleUndocked();

            return;
        }

        // Keep the state authoritative every frame.
        //
        // This also defeats a BoatBoardedVolume trigger that may temporarily
        // auto-board an occupant while a still-undocked bell is being hauled back
        // through the boat's volume before the dock actually captures it.
        if (docked)
            EnsureDockedOccupantsBoarded();
        else
            EnsureUndockedOccupantsUnboarded();
    }

    private void HandleUndocked()
    {
        resolvedDockBoat =
            null;

        EnsureUndockedOccupantsUnboarded();

        Log(
            "Bell undocked. Bell occupants are no longer boat-boarded.");
    }

    private void HandleDocked()
    {
        resolvedDockBoat =
            ResolveDockBoat();

        EnsureDockedOccupantsBoarded();

        Log(
            $"Bell docked. Reconciled occupants to boat " +
            $"'{(resolvedDockBoat != null ? resolvedDockBoat.name : "NONE")}'.");
    }

    private void EnsureUndockedOccupantsUnboarded()
    {
        SnapshotOccupants();

        int reconciled =
            0;

        for (int i = 0;
             i < _occupantScratch.Count;
             i++)
        {
            PlayerBellOccupantState occupant =
                _occupantScratch[i];

            if (occupant == null ||
                !occupant.IsInside(
                    occupancy))
            {
                continue;
            }

            PlayerBoardingState boarding =
                ResolveBoardingState(
                    occupant);

            if (boarding == null)
                continue;

            if (boarding.IsBoarded)
            {
                // IMPORTANT:
                // PlayerBoardingState.Unboard does NOT reparent the player.
                // Bell occupancy therefore keeps the player's hierarchy under
                // the moving bell while boat mass/collision/visual authority is
                // removed.
                boarding.Unboard();

                reconciled++;
            }
        }

        reconciledOccupantCount =
            reconciled;
    }

    private void EnsureDockedOccupantsBoarded()
    {
        Boat boat =
            ResolveDockBoat();

        resolvedDockBoat =
            boat;

        if (boat == null)
        {
            reconciledOccupantCount =
                0;

            return;
        }

        Transform boatRoot =
            boat.transform;

        SnapshotOccupants();

        int reconciled =
            0;

        for (int i = 0;
             i < _occupantScratch.Count;
             i++)
        {
            PlayerBellOccupantState occupant =
                _occupantScratch[i];

            if (occupant == null ||
                !occupant.IsInside(
                    occupancy))
            {
                continue;
            }

            PlayerBoardingState boarding =
                ResolveBoardingState(
                    occupant);

            if (boarding == null)
                continue;

            if (!boarding.IsBoarded ||
                boarding.CurrentBoatRoot !=
                    boatRoot)
            {
                boarding.Board(
                    boatRoot);

                reconciled++;
            }
        }

        // Board() refreshes the normal player collision and current boat visual
        // zones. DivingBellCollisionContext remains the higher-priority physical
        // collision override while the player is still inside the bell.
        reconciledOccupantCount =
            reconciled;
    }

    private Boat ResolveDockBoat()
    {
        if (occupancy == null ||
            !occupancy.IsDocked)
        {
            return null;
        }

        TetherPayloadDock dock =
            occupancy.CurrentDock;

        if (dock == null)
            return null;

        Boat boat =
            dock.GetComponentInParent<Boat>();

        if (boat != null)
            return boat;

        // Defensive fallback for a future dock hierarchy where the dock component
        // itself is not physically parented under Boat.
        SnapshotOccupants();

        for (int i = 0;
             i < _occupantScratch.Count;
             i++)
        {
            PlayerBellOccupantState occupant =
                _occupantScratch[i];

            PlayerBoardingState boarding =
                ResolveBoardingState(
                    occupant);

            if (boarding == null)
                continue;

            if (BoatBoardedVolume.TryFindContainingVolume(
                    boarding,
                    out BoatBoardedVolume volume) &&
                volume != null &&
                volume.BoatRoot != null)
            {
                Boat fromVolume =
                    volume.BoatRoot.GetComponent<Boat>();

                if (fromVolume != null)
                    return fromVolume;
            }
        }

        return null;
    }

    private void SnapshotOccupants()
    {
        _occupantScratch.Clear();

        if (occupancy == null)
            return;

        IReadOnlyList<PlayerBellOccupantState> occupants =
            occupancy.Occupants;

        if (occupants == null)
            return;

        for (int i = 0;
             i < occupants.Count;
             i++)
        {
            PlayerBellOccupantState state =
                occupants[i];

            if (state != null)
            {
                _occupantScratch.Add(
                    state);
            }
        }
    }

    private static PlayerBoardingState ResolveBoardingState(
        PlayerBellOccupantState occupant)
    {
        if (occupant == null)
            return null;

        return
            occupant.GetComponent<PlayerBoardingState>() ??
            occupant.GetComponentInChildren<PlayerBoardingState>(
                true);
    }

    private void ResolveRefs()
    {
        if (occupancy == null)
        {
            occupancy =
                GetComponent<DivingBellOccupancy>() ??
                GetComponentInParent<DivingBellOccupancy>() ??
                GetComponentInChildren<DivingBellOccupancy>(
                    true);
        }
    }

    private void Log(
        string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[DivingBellDeploymentContext:{name}] {message}",
            this);
    }
}
