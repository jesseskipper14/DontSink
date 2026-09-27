using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative occupancy/state component for a boardable diving bell.
///
/// Dry-pass responsibilities:
/// - identify whether the bell is currently docked;
/// - register any number of occupants;
/// - keep bell occupancy separate from PlayerBoardingState;
/// - move an entering player to InteriorEntryPoint;
/// - allow front-door access while docked;
/// - allow bottom-opening access while deployed/undocked;
/// - restore the player's pre-bell hierarchy parent on front exit;
/// - return bottom-exiting occupants to world space.
///
/// Collision and presentation remain delegated to the existing bell context systems.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TetherPayload))]
public sealed class DivingBellOccupancy : MonoBehaviour
{
    [Header("Bell Points")]
    [Tooltip(
        "Where a player is placed immediately after entering through the front.")]
    [SerializeField] private Transform interiorEntryPoint;

    [Tooltip(
        "Where a player is placed after leaving through the front while the bell is docked.")]
    [SerializeField] private Transform frontExitPoint;

    [Tooltip(
        "Where a player is placed immediately after entering through the bottom opening while the bell is deployed.")]
    [SerializeField] private Transform bottomInteriorPoint;

    [Tooltip(
        "Where a player is placed immediately after leaving through the bottom opening while the bell is deployed.")]
    [SerializeField] private Transform bottomExteriorPoint;

    [Tooltip(
        "Optional left-side interior transfer point used when the deployed bottom opening is obstructed or by a left-side deployed-access interactable.")]
    [SerializeField] private Transform leftSideInteriorPoint;

    [Tooltip(
        "Optional left-side exterior transfer point. Author this far enough outside the bell hull for the player's solid collider to fit cleanly.")]
    [SerializeField] private Transform leftSideExteriorPoint;

    [Tooltip(
        "Optional right-side interior transfer point used when the deployed bottom opening is obstructed or by a right-side deployed-access interactable.")]
    [SerializeField] private Transform rightSideInteriorPoint;

    [Tooltip(
        "Optional right-side exterior transfer point. Author this far enough outside the bell hull for the player's solid collider to fit cleanly.")]
    [SerializeField] private Transform rightSideExteriorPoint;

    [Header("Deployed Exit Ground Safety")]
    [Tooltip(
        "Generated ground sampler used to reject deployed exits that would place the player's solid body below the sea floor. Auto-resolves when blank.")]
    [SerializeField] private GeneratedGroundSampler2D generatedGroundSampler;

    [Tooltip(
        "Desired vertical clearance between the player's solid collider bottom and generated sea floor at a deployed exit point.")]
    [SerializeField, Min(0f)] private float deployedExitGroundClearance = 0.04f;

    [Tooltip(
        "Tiny authored/physics tolerance before an exit point is considered blocked by generated ground.")]
    [SerializeField, Min(0f)] private float deployedExitGroundTolerance = 0.02f;

    [Header("Transfer")]
    [Tooltip(
        "Parent occupants to the bell while inside so they follow the bell's frame. " +
        "Their previous parent is restored on front exit.")]
    [SerializeField]
    private bool parentOccupantsToBell =
        true;

    [SerializeField]
    private bool zeroVelocityOnTransfer =
        true;

    [Tooltip(
        "If true, Interior Entry Point represents the center of the player's primary " +
        "solid body collider rather than the player's Transform/Rigidbody pivot.")]
    [SerializeField]
    private bool centerPlayerBodyOnInteriorEntry =
        true;

    [Header("Dock Rules")]
    [Tooltip(
        "Front boarding is only allowed while this physical bell is captured by a TetherPayloadDock.")]
    [SerializeField]
    private bool requireDockedForFrontBoard =
        true;

    [Tooltip(
        "Front exit is only allowed while this physical bell is captured by a TetherPayloadDock. " +
        "Underwater/suspended access uses the bottom opening instead.")]
    [SerializeField]
    private bool requireDockedForFrontExit =
        true;

    [Header("Emergency Ejection")]
    [Tooltip(
        "If this bell is disabled/destroyed while occupied, eject every occupant BEFORE " +
        "the bell hierarchy disappears. This prevents a parented player from being destroyed " +
        "with the payload.")]
    [SerializeField] private bool ejectOccupantsOnDisable = true;

    [Tooltip(
        "Optional explicit emergency eject point. While docked, Front Exit Point is used if this is blank; " +
        "while deployed, Bottom Exterior Point is preferred.")]
    [SerializeField] private Transform emergencyEjectPoint;

    [Tooltip(
        "When emergency-ejecting from a freely moving bell, inherit the bell Rigidbody2D velocity.")]
    [SerializeField] private bool inheritBellVelocityOnEmergencyEject = true;

    [Header("Runtime Debug")]
    [SerializeField] private TetherPayload tetherPayload;

    [SerializeField]
    private List<PlayerBellOccupantState> occupants =
        new List<PlayerBellOccupantState>();

    [SerializeField] private TetherPayloadDock currentDock;

    public event Action<DivingBellOccupancy, PlayerBellOccupantState>
        OccupantEntered;

    public event Action<DivingBellOccupancy, PlayerBellOccupantState>
        OccupantExited;

    public event Action<DivingBellOccupancy, PlayerBellOccupantState>
        OccupantEmergencyEjected;

    public Transform InteriorEntryPoint =>
        interiorEntryPoint != null
            ? interiorEntryPoint
            : transform;

    public Transform FrontExitPoint =>
        frontExitPoint != null
            ? frontExitPoint
            : transform;

    public Transform BottomInteriorPoint =>
        bottomInteriorPoint;

    public Transform BottomExteriorPoint =>
        bottomExteriorPoint;

    public Transform LeftSideInteriorPoint =>
        leftSideInteriorPoint;

    public Transform LeftSideExteriorPoint =>
        leftSideExteriorPoint;

    public Transform RightSideInteriorPoint =>
        rightSideInteriorPoint;

    public Transform RightSideExteriorPoint =>
        rightSideExteriorPoint;

    public int OccupantCount
    {
        get
        {
            PruneOccupants();
            return occupants.Count;
        }
    }

    public bool HasOccupants =>
        OccupantCount > 0;

    public IReadOnlyList<PlayerBellOccupantState> Occupants =>
        occupants;

    public TetherPayload Payload =>
        tetherPayload;

    public TetherPayloadDock CurrentDock
    {
        get
        {
            ResolveCurrentDock();
            return currentDock;
        }
    }

    public bool IsDocked
    {
        get
        {
            ResolveCurrentDock();

            return
                currentDock != null &&
                tetherPayload != null &&
                currentDock.IsDocked(
                    tetherPayload);
        }
    }

    private void Awake()
    {
        ResolveRefs();
        PruneOccupants();
    }

    private void OnDisable()
    {
        if (!HasOccupants)
            return;

        if (!ejectOccupantsOnDisable)
        {
            ClearOccupantStateWithoutHierarchyMutation();
            return;
        }

        // IMPORTANT:
        // Unity forbids Transform.SetParent while this GameObject (or one of its
        // parents) is in the middle of activation/deactivation. OnDisable is
        // therefore already too late to safely emergency-eject a parented player.
        //
        // Every intentional bell teardown path must call
        // EmergencyEjectAllOccupants BEFORE SetActive(false), Destroy, or scene
        // teardown begins. SceneTransitionController already does this for scene
        // loads, and TetherDeploymentModule does it for payload destruction.
        //
        // Preserve occupancy here instead of corrupting hierarchy/state during a
        // temporary disable. If this is an unexpected permanent destruction,
        // OnDestroy will report it and defensively clear the stale state.
        Debug.LogWarning(
            $"[DivingBellOccupancy:{name}] Bell became disabled while still occupied. " +
            "Occupants cannot be safely reparented from OnDisable. " +
            "The caller must invoke EmergencyEjectAllOccupants BEFORE disabling/destroying the bell.\n" +
            System.Environment.StackTrace,
            this);
    }

    private void OnDestroy()
    {
        if (!HasOccupants)
            return;

        // At this point hierarchy mutation is no longer safe. Known destruction
        // paths pre-eject before teardown, so reaching this branch means some
        // future/unknown caller destroyed an occupied bell without preparation.
        Debug.LogError(
            $"[DivingBellOccupancy:{name}] Bell was destroyed while still occupied. " +
            "A teardown caller skipped EmergencyEjectAllOccupants before destruction. " +
            "Clearing bell occupancy state defensively; the teardown caller must be fixed.",
            this);

        ClearOccupantStateWithoutHierarchyMutation();
    }

    private void ClearOccupantStateWithoutHierarchyMutation()
    {
        for (int i = 0;
             i < occupants.Count;
             i++)
        {
            PlayerBellOccupantState state =
                occupants[i];

            if (state == null)
                continue;

            ApplyInteractionContext(
                state,
                active: false);

            state.ClearIfOwnedBy(
                this);
        }

        occupants.Clear();
    }

    /// <summary>
    /// Returns true when this player is registered inside this exact bell.
    /// </summary>
    public bool Contains(
        GameObject playerObject)
    {
        PlayerBellOccupantState state =
            ResolveOccupantState(
                playerObject,
                createIfMissing: false);

        return
            state != null &&
            state.IsInside(
                this);
    }

    public bool CanEnterFront(
        GameObject playerObject,
        out string reason)
    {
        reason =
            null;

        ResolveRefs();

        if (playerObject == null)
        {
            reason =
                "Missing player.";

            return false;
        }

        if (requireDockedForFrontBoard &&
            !IsDocked)
        {
            reason =
                "Diving bell is not docked.";

            return false;
        }

        GameObject playerRoot =
            ResolvePlayerRoot(
                playerObject);

        if (playerRoot == null)
        {
            reason =
                "Could not resolve player root.";

            return false;
        }

        PlayerBellOccupantState existingState =
            ResolveOccupantState(
                playerRoot,
                createIfMissing: false);

        if (existingState != null &&
            existingState.IsInsideBell)
        {
            if (existingState.IsInside(
                    this))
            {
                reason =
                    "Player is already inside this diving bell.";
            }
            else
            {
                reason =
                    "Player is already inside another diving bell.";
            }

            return false;
        }

        // When the bell is docked to a boat, entering through its front should
        // preserve that boat context rather than silently inventing a second
        // boarding system.
        Transform dockedBoatRoot =
            ResolveDockedBoatRoot();

        if (dockedBoatRoot != null)
        {
            PlayerBoardingState boarding =
                playerRoot.GetComponent<PlayerBoardingState>() ??
                playerRoot.GetComponentInChildren<PlayerBoardingState>(
                    true);

            if (boarding == null ||
                !boarding.IsBoarded ||
                boarding.CurrentBoatRoot !=
                    dockedBoatRoot)
            {
                reason =
                    "Board the boat before entering the diving bell.";

                return false;
            }
        }

        return true;
    }

    public bool TryEnterFront(
        GameObject playerObject,
        out string message)
    {
        message =
            null;

        if (!CanEnterFront(
                playerObject,
                out message))
        {
            return false;
        }

        GameObject playerRoot =
            ResolvePlayerRoot(
                playerObject);

        if (playerRoot == null)
        {
            message =
                "Could not resolve player root.";

            return false;
        }

        PlayerBellOccupantState state =
            ResolveOccupantState(
                playerRoot,
                createIfMissing: true);

        if (state == null ||
            !state.TryBeginOccupancy(
                this))
        {
            message =
                "Could not enter diving bell.";

            return false;
        }

        if (!occupants.Contains(
                state))
        {
            occupants.Add(
                state);
        }

        ApplyInteractionContext(
            state,
            active: true);

        Transform playerTransform =
            state.transform;

        if (parentOccupantsToBell)
        {
            playerTransform.SetParent(
                transform,
                worldPositionStays: true);
        }

        SnapPlayer(
            state.gameObject,
            InteriorEntryPoint,
            centerPlayerBodyOnInteriorEntry);

        Physics2D.SyncTransforms();

        OccupantEntered?.Invoke(
            this,
            state);

        message =
            "Entered diving bell.";

        return true;
    }

    public bool CanExitFront(
        GameObject playerObject,
        out string reason)
    {
        reason =
            null;

        ResolveRefs();

        GameObject playerRoot =
            ResolvePlayerRoot(
                playerObject);

        if (playerRoot == null)
        {
            reason =
                "Could not resolve player root.";

            return false;
        }

        PlayerBellOccupantState state =
            ResolveOccupantState(
                playerRoot,
                createIfMissing: false);

        if (state == null ||
            !state.IsInside(
                this))
        {
            reason =
                "Player is not inside this diving bell.";

            return false;
        }

        if (requireDockedForFrontExit &&
            !IsDocked)
        {
            reason =
                "Front exit is unavailable while the diving bell is deployed.";

            return false;
        }

        return true;
    }

    public bool TryExitFront(
        GameObject playerObject,
        out string message)
    {
        message =
            null;

        if (!CanExitFront(
                playerObject,
                out message))
        {
            return false;
        }

        GameObject playerRoot =
            ResolvePlayerRoot(
                playerObject);

        PlayerBellOccupantState state =
            ResolveOccupantState(
                playerRoot,
                createIfMissing: false);

        if (state == null)
        {
            message =
                "Missing diving-bell occupant state.";

            return false;
        }

        occupants.Remove(
            state);

        Transform restoreParent =
            state.EndOccupancy(
                this);

        ApplyInteractionContext(
            state,
            active: false);

        // If the original parent disappeared or the player happened to be
        // unparented before entry, preserve the existing boat-boarded hierarchy
        // convention by falling back to CurrentBoatRoot.
        PlayerBoardingState boarding =
            state.GetComponent<PlayerBoardingState>() ??
            state.GetComponentInChildren<PlayerBoardingState>(
                true);

        if (restoreParent == null &&
            boarding != null &&
            boarding.IsBoarded &&
            boarding.CurrentBoatRoot != null)
        {
            restoreParent =
                boarding.CurrentBoatRoot;
        }

        state.transform.SetParent(
            restoreParent,
            worldPositionStays: true);

        SnapPlayer(
            state.gameObject,
            FrontExitPoint,
            alignBodyCenterToTarget: true);

        Physics2D.SyncTransforms();

        ResolvePostBellBoatContext(
            state);

        OccupantExited?.Invoke(
            this,
            state);

        // Exit subscribers may temporarily manipulate player sorting for their own
        // presentation cleanup. Reassert the authoritative current boat/world
        // presentation LAST so a front exit cannot retain stale bell sorting.
        ReapplyAuthoritativePlayerPresentation(
            state);

        message =
            "Left diving bell.";

        return true;
    }

    /// <summary>
    /// Backward-compatible bottom-route wrapper. New side access interactables
    /// should call CanEnterDeployed with their authored route.
    /// </summary>
    public bool CanEnterBottom(
        GameObject playerObject,
        out string reason)
    {
        return CanEnterDeployed(
            playerObject,
            DivingBellDeployedAccessRoute.Bottom,
            out reason);
    }

    public bool TryEnterBottom(
        GameObject playerObject,
        out string message)
    {
        return TryEnterDeployed(
            playerObject,
            DivingBellDeployedAccessRoute.Bottom,
            out message);
    }

    public bool CanExitBottom(
        GameObject playerObject,
        out string reason)
    {
        return CanExitDeployed(
            playerObject,
            DivingBellDeployedAccessRoute.Bottom,
            out reason);
    }

    public bool TryExitBottom(
        GameObject playerObject,
        out string message)
    {
        return TryExitDeployed(
            playerObject,
            DivingBellDeployedAccessRoute.Bottom,
            out message);
    }

    /// <summary>
    /// Returns true when the player may enter this undocked bell through the
    /// requested deployed-access route. Bottom/left/right share one occupancy
    /// system; only their authored transfer points differ.
    /// </summary>
    public bool CanEnterDeployed(
        GameObject playerObject,
        DivingBellDeployedAccessRoute requestedRoute,
        out string reason)
    {
        reason = null;
        ResolveRefs();

        if (playerObject == null)
        {
            reason = "Missing player.";
            return false;
        }

        if (IsDocked)
        {
            reason = "Deployed access is unavailable while the diving bell is docked.";
            return false;
        }

        Transform interiorPoint = ResolveDeployedInteriorPoint(requestedRoute);

        if (interiorPoint == null)
        {
            reason = $"Diving bell {requestedRoute} interior point is not configured.";
            return false;
        }

        GameObject playerRoot = ResolvePlayerRoot(playerObject);

        if (playerRoot == null)
        {
            reason = "Could not resolve player root.";
            return false;
        }

        PlayerBellOccupantState existingState =
            ResolveOccupantState(
                playerRoot,
                createIfMissing: false);

        if (existingState != null &&
            existingState.IsInsideBell)
        {
            reason = existingState.IsInside(this)
                ? "Player is already inside this diving bell."
                : "Player is already inside another diving bell.";
            return false;
        }

        return true;
    }

    public bool TryEnterDeployed(
        GameObject playerObject,
        DivingBellDeployedAccessRoute requestedRoute,
        out string message)
    {
        message = null;

        if (!CanEnterDeployed(
                playerObject,
                requestedRoute,
                out message))
        {
            return false;
        }

        GameObject playerRoot = ResolvePlayerRoot(playerObject);
        Transform interiorPoint = ResolveDeployedInteriorPoint(requestedRoute);

        if (playerRoot == null || interiorPoint == null)
        {
            message = "Could not resolve deployed diving-bell entry.";
            return false;
        }

        PlayerBellOccupantState state =
            ResolveOccupantState(
                playerRoot,
                createIfMissing: true);

        if (state == null ||
            !state.TryBeginOccupancy(this))
        {
            message = "Could not enter diving bell.";
            return false;
        }

        if (!occupants.Contains(state))
            occupants.Add(state);

        ApplyInteractionContext(state, active: true);

        if (parentOccupantsToBell)
        {
            state.transform.SetParent(
                transform,
                worldPositionStays: true);
        }

        SnapPlayer(
            state.gameObject,
            interiorPoint,
            alignBodyCenterToTarget: true);

        Physics2D.SyncTransforms();
        OccupantEntered?.Invoke(this, state);

        message = $"Entered diving bell through {requestedRoute} access.";
        return true;
    }

    public bool CanExitDeployed(
        GameObject playerObject,
        DivingBellDeployedAccessRoute requestedRoute,
        out string reason)
    {
        reason = null;
        ResolveRefs();

        GameObject playerRoot = ResolvePlayerRoot(playerObject);

        if (playerRoot == null)
        {
            reason = "Could not resolve player root.";
            return false;
        }

        PlayerBellOccupantState state =
            ResolveOccupantState(
                playerRoot,
                createIfMissing: false);

        if (state == null ||
            !state.IsInside(this))
        {
            reason = "Player is not inside this diving bell.";
            return false;
        }

        if (IsDocked)
        {
            reason = "Deployed exit is unavailable while the diving bell is docked.";
            return false;
        }

        if (!TryResolveSafeDeployedExit(
                playerRoot,
                requestedRoute,
                out _,
                out _,
                out reason))
        {
            return false;
        }

        return true;
    }

    public bool TryExitDeployed(
        GameObject playerObject,
        DivingBellDeployedAccessRoute requestedRoute,
        out string message)
    {
        message = null;

        if (!CanExitDeployed(
                playerObject,
                requestedRoute,
                out message))
        {
            return false;
        }

        GameObject playerRoot = ResolvePlayerRoot(playerObject);
        PlayerBellOccupantState state =
            ResolveOccupantState(
                playerRoot,
                createIfMissing: false);

        if (state == null)
        {
            message = "Missing diving-bell occupant state.";
            return false;
        }

        if (!TryResolveSafeDeployedExit(
                playerRoot,
                requestedRoute,
                out DivingBellDeployedAccessRoute actualRoute,
                out Transform exteriorPoint,
                out message))
        {
            return false;
        }

        occupants.Remove(state);

        // Deployed exit always returns the player to world space. Never restore
        // ParentBeforeBell here; it may be a boat far above the submerged bell.
        state.EndOccupancy(this);
        ApplyInteractionContext(state, active: false);

        state.transform.SetParent(
            null,
            worldPositionStays: true);

        SnapPlayer(
            state.gameObject,
            exteriorPoint,
            alignBodyCenterToTarget: true);

        Rigidbody2D playerBody = state.GetComponent<Rigidbody2D>();
        Rigidbody2D bellBody =
            tetherPayload != null
                ? tetherPayload.Rigidbody
                : GetComponent<Rigidbody2D>();

        if (playerBody != null)
        {
            playerBody.linearVelocity =
                bellBody != null
                    ? bellBody.linearVelocity
                    : Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        Physics2D.SyncTransforms();
        ResolveBottomExitWorldContext(state);
        OccupantExited?.Invoke(this, state);
        ReapplyAuthoritativePlayerPresentation(state);

        message = actualRoute == requestedRoute
            ? $"Left diving bell through {actualRoute} access."
            : $"Bottom access was obstructed; left diving bell through {actualRoute} access.";

        return true;
    }

    private bool TryResolveSafeDeployedExit(
        GameObject playerRoot,
        DivingBellDeployedAccessRoute requestedRoute,
        out DivingBellDeployedAccessRoute actualRoute,
        out Transform exteriorPoint,
        out string reason)
    {
        actualRoute = requestedRoute;
        exteriorPoint = null;
        reason = null;

        Collider2D playerCollider =
            ResolvePrimarySolidPlayerCollider(
                playerRoot,
                playerRoot != null
                    ? playerRoot.GetComponent<Rigidbody2D>()
                    : null);

        DivingBellDeployedAccessRoute[] order =
            BuildExitFallbackOrder(requestedRoute);

        for (int i = 0; i < order.Length; i++)
        {
            DivingBellDeployedAccessRoute route = order[i];
            Transform candidate = ResolveDeployedExteriorPoint(route);

            if (candidate == null)
                continue;

            if (IsDeployedExitClearOfGeneratedGround(
                    playerCollider,
                    candidate.position,
                    out _))
            {
                actualRoute = route;
                exteriorPoint = candidate;
                return true;
            }
        }

        reason =
            "No configured diving-bell deployed exit has enough sea-floor clearance for the player.";
        return false;
    }

    private bool IsDeployedExitClearOfGeneratedGround(
        Collider2D playerCollider,
        Vector2 intendedColliderCenter,
        out float requiredCorrection)
    {
        requiredCorrection = 0f;

        if (playerCollider == null)
            return true;

        if (generatedGroundSampler == null)
        {
            generatedGroundSampler =
                FindAnyObjectByType<GeneratedGroundSampler2D>();
        }

        if (generatedGroundSampler == null)
            return true;

        return GeneratedGroundClearanceUtility2D.IsPlacementClear(
            generatedGroundSampler,
            playerCollider,
            intendedColliderCenter,
            deployedExitGroundClearance,
            deployedExitGroundTolerance,
            out requiredCorrection);
    }

    private static DivingBellDeployedAccessRoute[] BuildExitFallbackOrder(
        DivingBellDeployedAccessRoute requestedRoute)
    {
        switch (requestedRoute)
        {
            case DivingBellDeployedAccessRoute.LeftSide:
                return new[]
                {
                    DivingBellDeployedAccessRoute.LeftSide,
                    DivingBellDeployedAccessRoute.RightSide,
                    DivingBellDeployedAccessRoute.Bottom
                };

            case DivingBellDeployedAccessRoute.RightSide:
                return new[]
                {
                    DivingBellDeployedAccessRoute.RightSide,
                    DivingBellDeployedAccessRoute.LeftSide,
                    DivingBellDeployedAccessRoute.Bottom
                };

            default:
                return new[]
                {
                    DivingBellDeployedAccessRoute.Bottom,
                    DivingBellDeployedAccessRoute.LeftSide,
                    DivingBellDeployedAccessRoute.RightSide
                };
        }
    }

    private Transform ResolveDeployedInteriorPoint(
        DivingBellDeployedAccessRoute route)
    {
        switch (route)
        {
            case DivingBellDeployedAccessRoute.LeftSide:
                return leftSideInteriorPoint;

            case DivingBellDeployedAccessRoute.RightSide:
                return rightSideInteriorPoint;

            default:
                return bottomInteriorPoint;
        }
    }

    private Transform ResolveDeployedExteriorPoint(
        DivingBellDeployedAccessRoute route)
    {
        switch (route)
        {
            case DivingBellDeployedAccessRoute.LeftSide:
                return leftSideExteriorPoint;

            case DivingBellDeployedAccessRoute.RightSide:
                return rightSideExteriorPoint;

            default:
                return bottomExteriorPoint;
        }
    }

    /// <summary>
    /// Emergency-release one specific occupant.
    ///
    /// Used by containment/safety systems when a player physically escapes the bell
    /// without going through the normal front-exit interaction. This restores
    /// occupancy/collision state instead of leaving the player in a permanent
    /// bell-only collision context.
    /// </summary>
    public bool TryEmergencyEjectOccupant(
        GameObject playerObject,
        string reason = null)
    {
        if (playerObject == null)
            return false;

        PlayerBellOccupantState state =
            ResolveOccupantState(
                playerObject,
                createIfMissing: false);

        if (state == null ||
            !state.IsInside(
                this))
        {
            return false;
        }

        return
            EmergencyEjectOccupant(
                state,
                reason);
    }

    /// <summary>
    /// Failsafe used when the physical bell is disabled/destroyed while occupied.
    ///
    /// Occupants are detached from the bell hierarchy BEFORE the payload disappears,
    /// their PlayerBellOccupantState is cleared, and they are moved to a safe authored
    /// point. This is intentionally separate from normal front-exit validation.
    /// </summary>
    public int EmergencyEjectAllOccupants(
        string reason = null)
    {
        PruneOccupants();

        if (occupants.Count == 0)
            return 0;

        List<PlayerBellOccupantState> snapshot =
            new List<PlayerBellOccupantState>(
                occupants);

        int ejected =
            0;

        for (int i = 0;
             i < snapshot.Count;
             i++)
        {
            PlayerBellOccupantState state =
                snapshot[i];

            if (state == null ||
                !state.IsInside(
                    this))
            {
                continue;
            }

            if (EmergencyEjectOccupant(
                    state,
                    reason))
            {
                ejected++;
            }
        }

        return ejected;
    }

    private bool EmergencyEjectOccupant(
        PlayerBellOccupantState state,
        string reason)
    {
        if (state == null ||
            !state.IsInside(
                this))
        {
            return false;
        }

        Vector3 ejectPosition =
            ResolveEmergencyEjectPosition();

        Rigidbody2D bellBody =
            tetherPayload != null
                ? tetherPayload.Rigidbody
                : GetComponent<Rigidbody2D>();

        Vector2 inheritedVelocity =
            bellBody != null
                ? bellBody.linearVelocity
                : Vector2.zero;

        occupants.Remove(
            state);

        Transform savedParent =
            state.EndOccupancy(
                this);

        ApplyInteractionContext(
            state,
            active: false);

        PlayerBoardingState boarding =
            state.GetComponent<PlayerBoardingState>() ??
            state.GetComponentInChildren<PlayerBoardingState>(
                true);

        Transform safeParent =
            null;

        // If the player is still authoritatively boarded on a boat, that boat
        // wins over the old hierarchy snapshot. This is the normal dry/docked case.
        if (boarding != null &&
            boarding.IsBoarded &&
            boarding.CurrentBoatRoot != null)
        {
            safeParent =
                boarding.CurrentBoatRoot;
        }
        else if (savedParent != null &&
                 savedParent != transform &&
                 !savedParent.IsChildOf(
                     transform))
        {
            safeParent =
                savedParent;
        }

        // CRITICAL: detach from the bell before its GameObject can finish disabling
        // or destruction would recursively take the player with it.
        state.transform.SetParent(
            safeParent,
            worldPositionStays: true);

        PlacePlayerBodyCenterAtWorldPoint(
            state.gameObject,
            ejectPosition);

        Rigidbody2D playerBody =
            state.GetComponent<Rigidbody2D>();

        if (playerBody != null)
        {
            if (inheritBellVelocityOnEmergencyEject &&
                safeParent == null)
            {
                playerBody.linearVelocity =
                    inheritedVelocity;
            }
            else
            {
                playerBody.linearVelocity =
                    Vector2.zero;
            }

            playerBody.angularVelocity =
                0f;
        }

        Physics2D.SyncTransforms();

        ResolvePostBellBoatContext(
            state);

        OccupantEmergencyEjected?.Invoke(
            this,
            state);

        ReapplyAuthoritativePlayerPresentation(
            state);

        if (!string.IsNullOrWhiteSpace(
                reason))
        {
            Debug.LogWarning(
                $"[DivingBellOccupancy:{name}] Emergency-ejected '{state.name}'. {reason}",
                this);
        }

        return true;
    }

    private static void ReapplyAuthoritativePlayerPresentation(
        PlayerBellOccupantState state)
    {
        if (state == null)
            return;

        PlayerBoardingState boarding =
            state.GetComponent<PlayerBoardingState>() ??
            state.GetComponentInParent<PlayerBoardingState>() ??
            state.GetComponentInChildren<PlayerBoardingState>(
                true);

        if (boarding != null)
            boarding.ReapplyCurrentPresentation();
    }

    private void ApplyInteractionContext(
        PlayerBellOccupantState state,
        bool active)
    {
        if (state == null)
            return;

        GameObject playerRoot =
            ResolvePlayerRoot(
                state.gameObject);

        if (playerRoot == null)
            return;

        DivingBellInteractionContext interactionContext =
            playerRoot.GetComponent<DivingBellInteractionContext>();

        if (active)
        {
            if (interactionContext == null)
            {
                interactionContext =
                    playerRoot.AddComponent<DivingBellInteractionContext>();
            }

            interactionContext.SetBell(
                this);
        }
        else if (interactionContext != null)
        {
            interactionContext.ClearBell(
                this);
        }
    }

    /// <summary>
    /// Bottom exit is semantically a world exit, never a boat re-entry.
    /// A deployed bell may still overlap an intentionally generous boarded volume
    /// while hanging over the rail or just below the hull. Do not let that overlap
    /// silently convert the swimmer back into a boarded player.
    /// </summary>
    private void ResolveBottomExitWorldContext(
        PlayerBellOccupantState state)
    {
        if (state == null)
            return;

        PlayerBoardingState boarding =
            state.GetComponent<PlayerBoardingState>() ??
            state.GetComponentInChildren<PlayerBoardingState>(
                true);

        if (boarding == null)
            return;

        if (boarding.IsBoarded)
        {
            boarding.Unboard();
            return;
        }

        // The player may already have been unboarded when the bell deployed.
        // Reapply the ordinary world context now; DivingBellCollisionContext will
        // release its higher-priority ghost override on OccupantExited immediately
        // after this method returns.
        boarding.ReapplyCollisionMask();
        boarding.ReapplyCurrentPresentation();
    }

    /// <summary>
    /// After leaving the bell, resolve boat boarding from the player's actual
    /// final physical location rather than trusting stale pre-bell state.
    /// </summary>
    private void ResolvePostBellBoatContext(
        PlayerBellOccupantState state)
    {
        if (state == null)
            return;

        PlayerBoardingState boarding =
            state.GetComponent<PlayerBoardingState>() ??
            state.GetComponentInChildren<PlayerBoardingState>(
                true);

        if (boarding == null)
            return;

        Physics2D.SyncTransforms();

        Vector2 referencePoint =
            ResolvePlayerBoatVolumeReferencePoint(
                state.gameObject);

        if (BoatBoardedVolume.TryFindContainingVolume(
                referencePoint,
                out BoatBoardedVolume volume) &&
            volume != null &&
            volume.BoatRoot != null)
        {
            Transform boatRoot =
                volume.BoatRoot;

            state.transform.SetParent(
                boatRoot,
                worldPositionStays: true);

            if (!boarding.IsBoarded ||
                boarding.CurrentBoatRoot != boatRoot)
            {
                boarding.Board(
                    boatRoot);
            }
            else
            {
                boarding.ReapplyCollisionMask();
                boarding.ReapplyCurrentPresentation();
            }

            // Position and hierarchy are final now, so visibility-zone matching
            // should be refreshed against the player's actual location.
            Physics2D.SyncTransforms();
            boarding.RefreshCurrentBoatVisualState();
            return;
        }

        state.transform.SetParent(
            null,
            worldPositionStays: true);

        if (boarding.IsBoarded)
        {
            boarding.Unboard();
        }
        else
        {
            boarding.ReapplyCollisionMask();
            boarding.ReapplyCurrentPresentation();
        }
    }

    private static Vector2 ResolvePlayerBoatVolumeReferencePoint(
        GameObject playerRoot)
    {
        if (playerRoot == null)
            return Vector2.zero;

        Rigidbody2D rb =
            playerRoot.GetComponent<Rigidbody2D>();

        Collider2D bodyCollider =
            ResolvePrimarySolidPlayerCollider(
                playerRoot,
                rb);

        if (bodyCollider != null)
            return bodyCollider.bounds.center;

        if (rb != null)
            return rb.worldCenterOfMass;

        return playerRoot.transform.position;
    }

    private Vector3 ResolveEmergencyEjectPosition()
    {
        if (emergencyEjectPoint != null)
            return emergencyEjectPoint.position;

        if (IsDocked &&
            frontExitPoint != null)
        {
            return frontExitPoint.position;
        }

        if (!IsDocked &&
            bottomExteriorPoint != null)
        {
            return bottomExteriorPoint.position;
        }

        if (frontExitPoint != null)
            return frontExitPoint.position;

        if (bottomExteriorPoint != null)
            return bottomExteriorPoint.position;

        return transform.position;
    }

    private static void PlacePlayerBodyCenterAtWorldPoint(
        GameObject playerRoot,
        Vector2 targetWorld)
    {
        if (playerRoot == null)
            return;

        Rigidbody2D rb =
            playerRoot.GetComponent<Rigidbody2D>();

        Physics2D.SyncTransforms();

        Collider2D bodyCollider =
            ResolvePrimarySolidPlayerCollider(
                playerRoot,
                rb);

        if (bodyCollider != null)
        {
            Vector2 delta =
                targetWorld -
                (Vector2)bodyCollider.bounds.center;

            if (rb != null)
            {
                rb.position +=
                    delta;
            }
            else
            {
                playerRoot.transform.position +=
                    (Vector3)delta;
            }

            Physics2D.SyncTransforms();
            return;
        }

        if (rb != null)
            rb.position = targetWorld;
        else
            playerRoot.transform.position = targetWorld;

        Physics2D.SyncTransforms();
    }

    private void SnapPlayer(
        GameObject playerRoot,
        Transform target,
        bool alignBodyCenterToTarget)
    {
        if (playerRoot == null ||
            target == null)
        {
            return;
        }

        Rigidbody2D rb =
            playerRoot.GetComponent<Rigidbody2D>();

        if (zeroVelocityOnTransfer &&
            rb != null)
        {
            rb.linearVelocity =
                Vector2.zero;

            rb.angularVelocity =
                0f;
        }

        // Parenting may have changed immediately before this call. Make collider
        // bounds truthful before using them as the positioning anchor.
        Physics2D.SyncTransforms();

        Vector2 targetWorld =
            target.position;

        if (alignBodyCenterToTarget)
        {
            PlacePlayerBodyCenterAtWorldPoint(
                playerRoot,
                targetWorld);

            return;
        }

        if (rb != null)
            rb.position = targetWorld;
        else
            playerRoot.transform.position = targetWorld;

        Physics2D.SyncTransforms();
    }

    private static Collider2D ResolvePrimarySolidPlayerCollider(
        GameObject playerRoot,
        Rigidbody2D playerBody)
    {
        if (playerRoot == null)
            return null;

        Collider2D[] colliders =
            playerRoot.GetComponentsInChildren<Collider2D>(
                true);

        if (colliders == null ||
            colliders.Length == 0)
        {
            return null;
        }

        Collider2D best =
            null;

        float bestArea =
            -1f;

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            Collider2D collider =
                colliders[i];

            if (collider == null ||
                !collider.enabled ||
                collider.isTrigger)
            {
                continue;
            }

            if (playerBody != null &&
                collider.attachedRigidbody != playerBody)
            {
                continue;
            }

            Bounds bounds =
                collider.bounds;

            float area =
                Mathf.Abs(
                    bounds.size.x *
                    bounds.size.y);

            if (area <= bestArea)
                continue;

            bestArea =
                area;

            best =
                collider;
        }

        return best;
    }

    private GameObject ResolvePlayerRoot(
        GameObject candidate)
    {
        if (candidate == null)
            return null;

        PlayerBoardingState boarding =
            candidate.GetComponent<PlayerBoardingState>() ??
            candidate.GetComponentInParent<PlayerBoardingState>() ??
            candidate.GetComponentInChildren<PlayerBoardingState>(
                true);

        if (boarding != null)
            return boarding.gameObject;

        PlayerBellOccupantState state =
            candidate.GetComponent<PlayerBellOccupantState>() ??
            candidate.GetComponentInParent<PlayerBellOccupantState>() ??
            candidate.GetComponentInChildren<PlayerBellOccupantState>(
                true);

        return
            state != null
                ? state.gameObject
                : candidate;
    }

    private PlayerBellOccupantState ResolveOccupantState(
        GameObject playerObject,
        bool createIfMissing)
    {
        if (playerObject == null)
            return null;

        PlayerBellOccupantState state =
            playerObject.GetComponent<PlayerBellOccupantState>() ??
            playerObject.GetComponentInParent<PlayerBellOccupantState>() ??
            playerObject.GetComponentInChildren<PlayerBellOccupantState>(
                true);

        if (state == null &&
            createIfMissing)
        {
            GameObject root =
                ResolvePlayerRoot(
                    playerObject);

            if (root != null)
            {
                state =
                    root.AddComponent<PlayerBellOccupantState>();
            }
        }

        return state;
    }

    private void ResolveRefs()
    {
        if (tetherPayload == null)
        {
            tetherPayload =
                GetComponent<TetherPayload>() ??
                GetComponentInParent<TetherPayload>() ??
                GetComponentInChildren<TetherPayload>(
                    true);
        }

        if (generatedGroundSampler == null)
        {
            generatedGroundSampler =
                FindAnyObjectByType<GeneratedGroundSampler2D>();
        }

        ResolveCurrentDock();
    }

    private void ResolveCurrentDock()
    {
        if (tetherPayload == null)
        {
            tetherPayload =
                GetComponent<TetherPayload>() ??
                GetComponentInParent<TetherPayload>() ??
                GetComponentInChildren<TetherPayload>(
                    true);
        }

        if (tetherPayload == null)
        {
            currentDock =
                null;

            return;
        }

        // TetherPayload already owns the direct payload-local dock relationship.
        // Prefer that authoritative runtime link instead of inferring ownership
        // from hierarchy or scanning every TetherPayloadDock in the scene.
        TetherPayloadDock activeDock =
            tetherPayload.ActiveDock;

        currentDock =
            activeDock != null &&
            activeDock.IsDocked(
                tetherPayload)
                ? activeDock
                : null;
    }

    private Transform ResolveDockedBoatRoot()
    {
        ResolveCurrentDock();

        if (currentDock == null)
            return null;

        Boat boat =
            currentDock.GetComponentInParent<Boat>();

        return
            boat != null
                ? boat.transform
                : null;
    }

    private void PruneOccupants()
    {
        for (int i =
                 occupants.Count - 1;
             i >= 0;
             i--)
        {
            PlayerBellOccupantState state =
                occupants[i];

            if (state == null ||
                !state.IsInside(
                    this))
            {
                occupants.RemoveAt(
                    i);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        deployedExitGroundClearance = Mathf.Max(0f, deployedExitGroundClearance);
        deployedExitGroundTolerance = Mathf.Max(0f, deployedExitGroundTolerance);
    }

    private void OnDrawGizmosSelected()
    {
        if (interiorEntryPoint != null)
        {
            Gizmos.color =
                new Color(
                    0.15f,
                    1f,
                    0.35f,
                    0.95f);

            Gizmos.DrawSphere(
                interiorEntryPoint.position,
                0.08f);
        }

        if (frontExitPoint != null)
        {
            Gizmos.color =
                new Color(
                    0.25f,
                    0.65f,
                    1f,
                    0.95f);

            Gizmos.DrawSphere(
                frontExitPoint.position,
                0.08f);
        }

        if (bottomInteriorPoint != null)
        {
            Gizmos.color =
                new Color(
                    0.2f,
                    1f,
                    0.85f,
                    0.95f);

            Gizmos.DrawSphere(
                bottomInteriorPoint.position,
                0.08f);
        }

        if (bottomExteriorPoint != null)
        {
            Gizmos.color =
                new Color(
                    0.2f,
                    0.55f,
                    1f,
                    0.95f);

            Gizmos.DrawSphere(
                bottomExteriorPoint.position,
                0.08f);
        }

        DrawDeployedSideGizmo(
            leftSideInteriorPoint,
            leftSideExteriorPoint,
            new Color(0.3f, 1f, 0.65f, 0.95f));

        DrawDeployedSideGizmo(
            rightSideInteriorPoint,
            rightSideExteriorPoint,
            new Color(1f, 0.65f, 0.25f, 0.95f));
    }

    private static void DrawDeployedSideGizmo(
        Transform interiorPoint,
        Transform exteriorPoint,
        Color color)
    {
        Gizmos.color = color;

        if (interiorPoint != null)
            Gizmos.DrawSphere(interiorPoint.position, 0.07f);

        if (exteriorPoint != null)
            Gizmos.DrawSphere(exteriorPoint.position, 0.09f);

        if (interiorPoint != null && exteriorPoint != null)
        {
            Gizmos.DrawLine(
                interiorPoint.position,
                exteriorPoint.position);
        }
    }
#endif
}
