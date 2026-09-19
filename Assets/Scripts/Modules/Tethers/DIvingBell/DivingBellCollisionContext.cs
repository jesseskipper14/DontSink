using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-player physical collision context for diving-bell occupants.
///
/// The bell's REAL BellInterior / BellLedge colliders remain attached to the
/// authoritative dynamic bell Rigidbody2D, but occupants never collide with
/// those real colliders.
///
/// Instead, the bell owns a generic GhostCollisionProxy that mirrors the
/// interior/floor geometry onto the normal GhostCollision layer. Occupants are
/// routed exclusively to that bell ghost, so their wall/floor impulses cannot
/// propel or torque the real bell.
///
/// No global Physics2D.IgnoreLayerCollision calls are used.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DivingBellOccupancy))]
public sealed class DivingBellCollisionContext :
    MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DivingBellOccupancy occupancy;

    [Tooltip(
        "Generic GhostCollisionProxy on THIS diving bell. Its Follow Body must be " +
        "the real bell Rigidbody2D and its Source Roots must contain the real " +
        "BellInterior/BellLedge colliders.")]
    [SerializeField] private GhostCollisionProxy bellGhostProxy;

    [Header("Ghost Layer")]
    [Tooltip(
        "Must match the layer used by Bell Ghost Proxy. Reusing the existing " +
        "GhostCollision layer lets the mature exclusive-ghost routing handle " +
        "boat ghosts and other bell ghosts together.")]
    [SerializeField]
    private string ghostCollisionLayerName =
        "GhostCollision";

    [Header("Debug")]
    [SerializeField]
    private bool verboseLogging =
        false;

    [SerializeField] private int trackedOccupants;
    [SerializeField] private bool bellGhostReady;
    [SerializeField]
    private int resolvedGhostLayer =
        -1;

    private int _ghostLayer =
        -1;

    private int _ghostBit;

    private readonly Dictionary<PlayerBellOccupantState, OccupantCollisionState>
        _activeOccupants =
            new Dictionary<PlayerBellOccupantState, OccupantCollisionState>();

    private readonly List<PlayerBellOccupantState>
        _currentOccupants =
            new List<PlayerBellOccupantState>();

    private readonly List<PlayerBellOccupantState>
        _scratchRemove =
            new List<PlayerBellOccupantState>();

    private void Awake()
    {
        ResolveRefs();
        CacheLayer();
    }

    private void OnEnable()
    {
        ResolveRefs();
        CacheLayer();

        if (occupancy != null)
        {
            occupancy.OccupantEntered +=
                HandleOccupantEntered;

            occupancy.OccupantExited +=
                HandleOccupantExited;

            occupancy.OccupantEmergencyEjected +=
                HandleOccupantEmergencyEjected;
        }

        GhostCollisionProxy.ActiveProxySetChanged +=
            HandleGhostProxySetChanged;

        ReconcileOccupants();
        RefreshGhostRoutingForTrackedOccupants();
    }

    private void OnDisable()
    {
        if (occupancy != null)
        {
            occupancy.OccupantEntered -=
                HandleOccupantEntered;

            occupancy.OccupantExited -=
                HandleOccupantExited;

            occupancy.OccupantEmergencyEjected -=
                HandleOccupantEmergencyEjected;
        }

        GhostCollisionProxy.ActiveProxySetChanged -=
            HandleGhostProxySetChanged;

        RestoreAllOccupants();
    }

    private void LateUpdate()
    {
        // Event-driven in the normal path, with reconciliation for save/load,
        // bootstrap, and future replicated occupancy changes.
        ReconcileOccupants();

        bellGhostReady =
            bellGhostProxy != null &&
            bellGhostProxy.IsBuilt;
    }

    private void HandleOccupantEntered(
        DivingBellOccupancy bell,
        PlayerBellOccupantState state)
    {
        if (bell != occupancy)
            return;

        ApplyOccupant(
            state);

        RefreshGhostRoutingForTrackedOccupants();
    }

    private void HandleOccupantExited(
        DivingBellOccupancy bell,
        PlayerBellOccupantState state)
    {
        if (bell != occupancy)
            return;

        RestoreOccupant(
            state);
    }

    private void HandleOccupantEmergencyEjected(
        DivingBellOccupancy bell,
        PlayerBellOccupantState state)
    {
        if (bell != occupancy)
            return;

        RestoreOccupant(
            state);
    }

    private void HandleGhostProxySetChanged()
    {
        ResolveRefs();
        RefreshGhostRoutingForTrackedOccupants();
    }

    private void ReconcileOccupants()
    {
        ResolveRefs();

        _currentOccupants.Clear();

        if (occupancy != null)
        {
            IReadOnlyList<PlayerBellOccupantState> occupants =
                occupancy.Occupants;

            if (occupants != null)
            {
                for (int i = 0;
                     i < occupants.Count;
                     i++)
                {
                    PlayerBellOccupantState state =
                        occupants[i];

                    if (state == null ||
                        !state.IsInside(
                            occupancy))
                    {
                        continue;
                    }

                    _currentOccupants.Add(
                        state);

                    if (!_activeOccupants.ContainsKey(
                            state))
                    {
                        ApplyOccupant(
                            state);
                    }
                }
            }
        }

        _scratchRemove.Clear();

        foreach (KeyValuePair<PlayerBellOccupantState, OccupantCollisionState> pair
                 in _activeOccupants)
        {
            if (pair.Key == null ||
                !_currentOccupants.Contains(
                    pair.Key))
            {
                _scratchRemove.Add(
                    pair.Key);
            }
        }

        for (int i = 0;
             i < _scratchRemove.Count;
             i++)
        {
            RestoreOccupant(
                _scratchRemove[i]);
        }

        trackedOccupants =
            _activeOccupants.Count;
    }

    private bool ApplyOccupant(
        PlayerBellOccupantState state)
    {
        if (state == null ||
            _activeOccupants.ContainsKey(
                state))
        {
            return false;
        }

        if (!EnsureLayerReady())
            return false;

        PlayerBoardingState boarding =
            state.GetComponent<PlayerBoardingState>() ??
            state.GetComponentInChildren<PlayerBoardingState>(
                true);

        if (boarding == null)
        {
            Debug.LogWarning(
                $"[DivingBellCollisionContext:{name}] " +
                $"Occupant '{state.name}' has no PlayerBoardingState.",
                state);

            return false;
        }

        LayerMask allowed =
            _ghostBit;

        LayerMask ground =
            _ghostBit;

        if (!boarding.TrySetCollisionContextOverride(
                this,
                allowed,
                ground,
                out string reason))
        {
            Debug.LogWarning(
                $"[DivingBellCollisionContext:{name}] " +
                $"Could not apply bell collision context to '{state.name}': {reason}",
                state);

            return false;
        }

        OccupantCollisionState runtimeState =
            new OccupantCollisionState(
                state,
                boarding);

        _activeOccupants.Add(
            state,
            runtimeState);

        ApplyGhostRouting(
            runtimeState);

        trackedOccupants =
            _activeOccupants.Count;

        Log(
            $"Applied occupant context to '{state.name}'.");

        return true;
    }

    private void RestoreOccupant(
        PlayerBellOccupantState state)
    {
        if (state == null)
            return;

        if (!_activeOccupants.TryGetValue(
                state,
                out OccupantCollisionState runtimeState))
        {
            return;
        }

        // ClearCollisionContextOverride re-runs PlayerBoardingState's ordinary
        // collision policy. That restores the owning BOAT ghost if docked/boarded,
        // or ordinary world collision if unboarded.
        if (runtimeState.Boarding != null)
        {
            runtimeState.Boarding.ClearCollisionContextOverride(
                this);
        }

        _activeOccupants.Remove(
            state);

        trackedOccupants =
            _activeOccupants.Count;

        Log(
            $"Restored occupant context for '{state.name}'.");
    }

    private void RestoreAllOccupants()
    {
        _scratchRemove.Clear();

        foreach (PlayerBellOccupantState state
                 in _activeOccupants.Keys)
        {
            _scratchRemove.Add(
                state);
        }

        for (int i = 0;
             i < _scratchRemove.Count;
             i++)
        {
            RestoreOccupant(
                _scratchRemove[i]);
        }

        _scratchRemove.Clear();
    }

    private void RefreshGhostRoutingForTrackedOccupants()
    {
        foreach (OccupantCollisionState state
                 in _activeOccupants.Values)
        {
            ApplyGhostRouting(
                state);
        }
    }

    private void ApplyGhostRouting(
        OccupantCollisionState state)
    {
        if (state == null)
            return;

        ResolveRefs();

        Collider2D[] playerColliders =
            state.ResolvePlayerColliders();

        if (playerColliders == null ||
            playerColliders.Length == 0)
        {
            return;
        }

        GhostCollisionProxy allowed =
            bellGhostProxy != null &&
            bellGhostProxy.IsBuilt
                ? bellGhostProxy
                : null;

        // Generic ghost routing does all of the important work:
        // - allows exactly this bell's ghost;
        // - ignores every boat ghost and every other bell ghost;
        // - ignores this bell ghost's REAL source colliders.
        GhostCollisionProxy.ConfigureExclusiveCollisions(
            playerColliders,
            allowed);

        bellGhostReady =
            allowed != null;

        if (allowed == null)
        {
            LogWarning(
                "Bell GhostCollisionProxy is missing or not built. " +
                "Occupant has ghost-only collision mask and may fall through until it is ready.");
        }
    }

    private bool EnsureLayerReady()
    {
        if (_ghostLayer >= 0)
            return true;

        CacheLayer();

        return
            _ghostLayer >= 0;
    }

    private void CacheLayer()
    {
        _ghostLayer =
            LayerMask.NameToLayer(
                ghostCollisionLayerName);

        resolvedGhostLayer =
            _ghostLayer;

        if (_ghostLayer < 0)
        {
            Debug.LogError(
                $"[DivingBellCollisionContext:{name}] " +
                $"Physics layer '{ghostCollisionLayerName}' does not exist.",
                this);

            _ghostBit =
                0;

            return;
        }

        _ghostBit =
            1 << _ghostLayer;
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

        if (bellGhostProxy == null)
        {
            // Deliberately resolve only on the bell itself/its children, never up
            // into the boat hierarchy where we could accidentally grab Ghost Boat.
            Transform bellRoot =
                occupancy != null
                    ? occupancy.transform
                    : transform;

            bellGhostProxy =
                bellRoot.GetComponent<GhostCollisionProxy>() ??
                bellRoot.GetComponentInChildren<GhostCollisionProxy>(
                    true);
        }
    }

    private void Log(
        string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[DivingBellCollisionContext:{name}] {message}",
            this);
    }

    private void LogWarning(
        string message)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning(
            $"[DivingBellCollisionContext:{name}] {message}",
            this);
    }

    private sealed class OccupantCollisionState
    {
        public readonly PlayerBellOccupantState Occupant;
        public readonly PlayerBoardingState Boarding;

        public OccupantCollisionState(
            PlayerBellOccupantState occupant,
            PlayerBoardingState boarding)
        {
            Occupant =
                occupant;

            Boarding =
                boarding;
        }

        public Collider2D[] ResolvePlayerColliders()
        {
            if (Occupant == null)
                return System.Array.Empty<Collider2D>();

            Rigidbody2D rb =
                Occupant.GetComponent<Rigidbody2D>() ??
                Occupant.GetComponentInChildren<Rigidbody2D>(
                    true);

            Collider2D[] all =
                Occupant.GetComponentsInChildren<Collider2D>(
                    true);

            if (rb == null ||
                all == null ||
                all.Length == 0)
            {
                return
                    all ??
                    System.Array.Empty<Collider2D>();
            }

            List<Collider2D> attached =
                new List<Collider2D>();

            for (int i = 0;
                 i < all.Length;
                 i++)
            {
                Collider2D collider =
                    all[i];

                if (collider != null &&
                    !collider.isTrigger &&
                    collider.attachedRigidbody == rb)
                {
                    attached.Add(
                        collider);
                }
            }

            return
                attached.ToArray();
        }
    }
}
