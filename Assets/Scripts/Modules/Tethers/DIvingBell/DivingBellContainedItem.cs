using System;
using UnityEngine;

/// <summary>
/// Runtime physical-container state for a loose WorldItem inside a diving bell.
///
/// Ownership and physical containment remain separate:
///
///     BoatOwnedItem
///         who owns/persists this item?
///
///     DivingBellContainedItem
///         which moving physical container currently contains it?
///
/// Bell-contained items keep the BellItem GameObject layer for authored item
/// behavior, but their physical wall/floor contacts are routed exclusively to
/// the bell's GhostCollisionProxy. They therefore cannot propel or torque the
/// real bell through interior collision impulses.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class DivingBellContainedItem :
    MonoBehaviour
{
    public event Action<DivingBellContainedItem>
        ContainmentChanged;

    [Header("Bell Item Layer")]
    [SerializeField]
    private string bellItemLayerName =
        "BellItem";

    [Header("Bell Visual Sorting")]
    [Tooltip(
        "Bell items inherit the bell interior's Sorting Layer and render this many " +
        "orders above its back/interior renderer.")]
    [SerializeField]
    private int sortingOrderAboveBellInterior =
        13;

    [Header("Authority")]
    [Tooltip(
        "Controls who may make autonomous containment-loss decisions. " +
        "Non-authoritative peers keep bell presentation/context available, but do not " +
        "decide that an item escaped the bell from their local physics state.")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("Containment Safety")]
    [Tooltip(
        "How long the item's primary solid-body center may remain outside the " +
        "bell's authored interior safety volume before bell containment clears.")]
    [SerializeField, Min(0f)]
    private float secondsOutsideBeforeClearing =
        0.15f;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLogging =
        false;

    [SerializeField] private DivingBellOccupancy currentBell;
    [SerializeField] private GhostCollisionProxy currentBellGhost;
    [SerializeField] private DivingBellVisualPresentation currentBellVisual;
    [SerializeField] private Collider2D containmentVolume;
    [SerializeField] private float outsideSeconds;
    [SerializeField]
    private int resolvedBellItemLayer =
        -1;

    [SerializeField] private bool bellGhostReady;
    [SerializeField] private string resolvedBellSortingLayer;
    [SerializeField] private int resolvedBellItemSortingOrder;

    private Rigidbody2D _rb;
    private DivingBellMassAggregator _registeredMassAggregator;

    private int _bellItemLayer =
        -1;

    private bool _hasBellContext;

    private Transform[] _layerTargets;
    private SpriteRenderer[] _spriteRenderers;
    private int[] _relativeSpriteOrders;

    private Collider2D[] _solidColliders =
        System.Array.Empty<Collider2D>();

    public bool IsContainedInBell =>
        _hasBellContext &&
        currentBell != null;

    public DivingBellOccupancy CurrentBell =>
        IsContainedInBell
            ? currentBell
            : null;

    /// <summary>
    /// True when this peer may autonomously decide that live physics has ended
    /// bell containment. Explicit Assign/Clear calls remain state-application APIs
    /// so persistence and future replicated state can still apply authoritative data.
    /// </summary>
    public bool ContainmentAuthority =>
        GameplayAuthority.CanRun(
            gameplayAuthorityMode);

    private void Awake()
    {
        ResolveRefs();
        CacheLayer();
        CachePresentationTargets();
        ResolveSolidColliders();
    }

    private void OnEnable()
    {
        GhostCollisionProxy.ActiveProxySetChanged +=
            HandleGhostProxySetChanged;

        ResolveRefs();
        CacheLayer();
        CachePresentationTargets();
        ResolveSolidColliders();

        if (_hasBellContext &&
            currentBell != null)
        {
            ResolveContainmentVolume();
            ReapplyBellContext();
            RegisterWithBellMassAggregator();
        }
    }

    private void OnDisable()
    {
        GhostCollisionProxy.ActiveProxySetChanged -=
            HandleGhostProxySetChanged;

        UnregisterFromBellMassAggregator();
    }

    private void OnDestroy()
    {
        GhostCollisionProxy.ActiveProxySetChanged -=
            HandleGhostProxySetChanged;

        UnregisterFromBellMassAggregator();

        ContainmentChanged =
            null;
    }

    private void LateUpdate()
    {
        if (!_hasBellContext)
            return;

        // Presentation remains client-local. The decision that live physics has
        // actually ended bell containment is shared gameplay state and belongs
        // only to the authoritative simulation.
        if (!ContainmentAuthority)
        {
            outsideSeconds =
                0f;

            ReapplyVisualContext();
            return;
        }

        // Destroyed UnityEngine.Object references compare equal to null. Losing the
        // containing bell is also a containment-state mutation, so only authority
        // performs the clear.
        if (currentBell == null)
        {
            ClearBellContainment(
                "Containing diving bell no longer exists.");

            return;
        }

        if (containmentVolume == null ||
            !containmentVolume.enabled ||
            !containmentVolume.gameObject.activeInHierarchy)
        {
            ResolveContainmentVolume();
        }

        if (containmentVolume != null &&
            containmentVolume.enabled &&
            containmentVolume.gameObject.activeInHierarchy)
        {
            // During dock capture the bell is intentionally moving its physical
            // frame toward the dock. Do not let a small transient separation from
            // the authored safety volume permanently eject cargo mid-capture.
            // Once Docked, normal containment immediately becomes authoritative again.
            if (IsBellDockCapturing())
            {
                outsideSeconds = 0f;
            }
            else
            {
                Vector2 bodyCenter =
                    ResolveBodyCenter();

                if (containmentVolume.OverlapPoint(
                        bodyCenter))
                {
                    outsideSeconds =
                        0f;
                }
                else
                {
                    outsideSeconds +=
                        Time.deltaTime;

                    if (outsideSeconds >=
                        secondsOutsideBeforeClearing)
                    {
                        ClearBellContainment(
                            "Item left diving bell containment volume.");

                        return;
                    }
                }
            }
        }

        // Visual sorting follows the bell's docked/deployed presentation on every
        // peer, including non-authoritative clients.
        ReapplyVisualContext();
    }

    public bool AssignToBell(
        DivingBellOccupancy bell)
    {
        if (bell == null)
            return false;

        ResolveRefs();
        CacheLayer();
        CachePresentationTargets();
        ResolveSolidColliders();

        if (_rb == null ||
            _bellItemLayer < 0)
        {
            return false;
        }

        UnregisterFromBellMassAggregator();

        currentBell =
            bell;

        currentBellGhost =
            ResolveBellGhost(
                bell);

        currentBellVisual =
            ResolveBellVisual(
                bell);

        _hasBellContext =
            true;

        outsideSeconds =
            0f;

        ResolveContainmentVolume();

        ReapplyBellContext();
        RegisterWithBellMassAggregator();

        ContainmentChanged?.Invoke(
            this);

        Log(
            $"Assigned to bell '{bell.name}'.");

        return true;
    }

    /// <summary>
    /// Clear only bell containment. Existing ordinary boat/world policies become
    /// authoritative again from the item's CURRENT ownership.
    /// </summary>
    public void ClearBellContainment(
        string reason = null)
    {
        if (!_hasBellContext)
            return;

        DivingBellOccupancy previousBell =
            currentBell;

        UnregisterFromBellMassAggregator();

        currentBell =
            null;

        currentBellGhost =
            null;

        currentBellVisual =
            null;

        _hasBellContext =
            false;

        containmentVolume =
            null;

        outsideSeconds =
            0f;

        bellGhostReady =
            false;

        ContainmentChanged?.Invoke(
            this);

        BoatOwnedItemLayerPolicy layerPolicy =
            GetComponent<BoatOwnedItemLayerPolicy>();

        if (layerPolicy != null)
            layerPolicy.ApplyNow();

        BoatOwnedItemVisualPolicy visualPolicy =
            GetComponent<BoatOwnedItemVisualPolicy>();

        if (visualPolicy != null)
            visualPolicy.ApplyNow();

        if (!string.IsNullOrWhiteSpace(
                reason))
        {
            Log(
                $"Cleared bell '{(previousBell != null ? previousBell.name : "NULL")}'. {reason}");
        }
    }

    public void ReapplyBellContext()
    {
        if (!IsContainedInBell)
            return;

        ReapplyPhysicalContext();
        ReapplyVisualContext();
    }

    /// <summary>
    /// Keep the item's authored BellItem layer, but route its solid colliders to
    /// exactly THIS bell's ghost shell.
    ///
    /// GhostCollisionProxy.ConfigureExclusiveCollisions also ignores this bell's
    /// REAL source colliders, which prevents cargo from applying solver impulses
    /// to the authoritative bell Rigidbody2D.
    /// </summary>
    public void ReapplyPhysicalContext()
    {
        if (!IsContainedInBell)
            return;

        CacheLayer();
        CachePresentationTargets();
        ResolveSolidColliders();

        if (_bellItemLayer < 0)
            return;

        if (_layerTargets != null)
        {
            for (int i = 0;
                 i < _layerTargets.Length;
                 i++)
            {
                Transform target =
                    _layerTargets[i];

                if (target == null)
                    continue;

                target.gameObject.layer =
                    _bellItemLayer;
            }
        }

        resolvedBellItemLayer =
            _bellItemLayer;

        currentBellGhost =
            ResolveBellGhost(
                currentBell);

        GhostCollisionProxy allowed =
            currentBellGhost != null &&
            currentBellGhost.IsBuilt
                ? currentBellGhost
                : null;

        if (allowed != null)
        {
            // BellItem collision now depends on the real Physics 2D matrix:
            //
            //     BellItem <-> GhostCollision = ON
            //
            // However, BoatOwnedItemLayerPolicy may have run once before bell
            // containment was assigned and left GhostCollision in this Rigidbody's
            // per-body excludeLayers mask. Layer-matrix permission cannot override
            // a Rigidbody exclusion, so explicitly clear ONLY the GhostCollision bit
            // while the bell context is authoritative.
            int ghostLayer =
                LayerMask.NameToLayer(
                    "GhostCollision");

            if (ghostLayer >= 0)
            {
                int ghostBit =
                    1 << ghostLayer;

                _rb.excludeLayers =
                    _rb.excludeLayers.value &
                    ~ghostBit;
            }
        }

        GhostCollisionProxy.ConfigureExclusiveCollisions(
            _solidColliders,
            allowed);

        bellGhostReady =
            allowed != null;

        if (allowed == null)
        {
            LogWarning(
                "Bell GhostCollisionProxy is missing or not built. " +
                "BellItem cannot be routed to its floor/walls yet.");
        }
    }

    /// <summary>
    /// Inherit the bell interior's Sorting Layer and apply the existing authored
    /// BellItem order offset.
    /// </summary>
    public void ReapplyVisualContext()
    {
        if (!IsContainedInBell)
            return;

        CachePresentationTargets();

        if (_spriteRenderers == null ||
            _spriteRenderers.Length == 0)
        {
            return;
        }

        int targetLayerId;
        int interiorBaseOrder;

        currentBellVisual =
            ResolveBellVisual(
                currentBell);

        if (currentBellVisual != null &&
            currentBellVisual.TryGetInteriorSortingContext(
                out targetLayerId,
                out interiorBaseOrder))
        {
            // Authoritative path.
        }
        else if (TryResolveBellInteriorSortingReference(
                     out SpriteRenderer fallbackReference))
        {
            // Backward-compatible fallback for an older bell prefab that does not
            // yet carry DivingBellVisualPresentation.
            targetLayerId =
                fallbackReference.sortingLayerID;

            interiorBaseOrder =
                fallbackReference.sortingOrder;
        }
        else
        {
            return;
        }

        int targetBaseOrder =
            interiorBaseOrder +
            sortingOrderAboveBellInterior;

        for (int i = 0;
             i < _spriteRenderers.Length;
             i++)
        {
            SpriteRenderer renderer =
                _spriteRenderers[i];

            if (renderer == null)
                continue;

            int relativeOrder =
                _relativeSpriteOrders != null &&
                i < _relativeSpriteOrders.Length
                    ? _relativeSpriteOrders[i]
                    : 0;

            renderer.sortingLayerID =
                targetLayerId;

            renderer.sortingOrder =
                targetBaseOrder +
                relativeOrder;
        }

        resolvedBellSortingLayer =
            SortingLayer.IDToName(
                targetLayerId);

        resolvedBellItemSortingOrder =
            targetBaseOrder;
    }

    private void HandleGhostProxySetChanged()
    {
        if (!IsContainedInBell)
            return;

        ReapplyPhysicalContext();
    }

    private GhostCollisionProxy ResolveBellGhost(
        DivingBellOccupancy bell)
    {
        if (bell == null)
            return null;

        // Deliberately search only the bell hierarchy, never its parent boat.
        GhostCollisionProxy direct =
            bell.GetComponent<GhostCollisionProxy>();

        if (direct != null)
            return direct;

        return
            bell.GetComponentInChildren<GhostCollisionProxy>(
                true);
    }

    private bool TryResolveBellInteriorSortingReference(
        out SpriteRenderer reference)
    {
        reference =
            null;

        if (currentBell == null)
            return false;

        SpriteRenderer[] bellRenderers =
            currentBell.GetComponentsInChildren<SpriteRenderer>(
                true);

        if (bellRenderers == null ||
            bellRenderers.Length == 0)
        {
            return false;
        }

        int bestOrder =
            int.MaxValue;

        for (int i = 0;
             i < bellRenderers.Length;
             i++)
        {
            SpriteRenderer candidate =
                bellRenderers[i];

            if (candidate == null)
                continue;

            Transform candidateTransform =
                candidate.transform;

            if (candidateTransform == transform ||
                candidateTransform.IsChildOf(
                    transform))
            {
                continue;
            }

            if (candidate.sortingOrder >=
                bestOrder)
            {
                continue;
            }

            reference =
                candidate;

            bestOrder =
                candidate.sortingOrder;
        }

        return
            reference != null;
    }

    private void RegisterWithBellMassAggregator()
    {
        if (!IsContainedInBell)
            return;

        DivingBellMassAggregator aggregator =
            currentBell.GetComponent<DivingBellMassAggregator>() ??
            currentBell.GetComponentInChildren<DivingBellMassAggregator>(
                true);

        if (ReferenceEquals(
                _registeredMassAggregator,
                aggregator))
        {
            return;
        }

        UnregisterFromBellMassAggregator();

        _registeredMassAggregator =
            aggregator;

        if (_registeredMassAggregator != null)
        {
            _registeredMassAggregator.RegisterContainedItem(
                this);
        }
    }

    private void UnregisterFromBellMassAggregator()
    {
        if (_registeredMassAggregator == null)
            return;

        DivingBellMassAggregator previous =
            _registeredMassAggregator;

        _registeredMassAggregator =
            null;

        previous.UnregisterContainedItem(
            this);
    }

    private DivingBellVisualPresentation ResolveBellVisual(
        DivingBellOccupancy bell)
    {
        if (bell == null)
            return null;

        return
            bell.GetComponent<DivingBellVisualPresentation>() ??
            bell.GetComponentInChildren<DivingBellVisualPresentation>(
                true);
    }

    private bool IsBellDockCapturing()
    {
        if (currentBell == null)
            return false;

        TetherPayload payload =
            currentBell.Payload;

        TetherPayloadDock dock =
            payload != null
                ? payload.ActiveDock
                : null;

        return
            dock != null &&
            dock.IsCapturing(
                payload);
    }

    private void ResolveContainmentVolume()
    {
        containmentVolume =
            null;

        if (currentBell == null)
            return;

        DivingBellOccupantContainmentGuard guard =
            currentBell.GetComponentInChildren<DivingBellOccupantContainmentGuard>(
                true);

        if (guard == null)
        {
            LogWarning(
                $"Bell '{currentBell.name}' has no DivingBellOccupantContainmentGuard. " +
                "Physical escape cannot auto-clear item bell context.");

            return;
        }

        containmentVolume =
            guard.GetComponent<Collider2D>();

        if (containmentVolume == null)
        {
            LogWarning(
                $"Bell '{currentBell.name}' containment guard has no Collider2D.");
        }
    }

    private Vector2 ResolveBodyCenter()
    {
        ResolveRefs();
        ResolveSolidColliders();

        Collider2D best =
            null;

        float bestArea =
            -1f;

        for (int i = 0;
             i < _solidColliders.Length;
             i++)
        {
            Collider2D collider =
                _solidColliders[i];

            if (collider == null ||
                !collider.enabled)
            {
                continue;
            }

            Bounds bounds =
                collider.bounds;

            float area =
                Mathf.Abs(
                    bounds.size.x *
                    bounds.size.y);

            if (area <=
                bestArea)
            {
                continue;
            }

            best =
                collider;

            bestArea =
                area;
        }

        if (best != null)
            return best.bounds.center;

        if (_rb != null)
            return _rb.worldCenterOfMass;

        return
            transform.position;
    }

    private void ResolveSolidColliders()
    {
        ResolveRefs();

        if (_rb == null)
        {
            _solidColliders =
                System.Array.Empty<Collider2D>();

            return;
        }

        Collider2D[] all =
            GetComponentsInChildren<Collider2D>(
                true);

        if (all == null ||
            all.Length == 0)
        {
            _solidColliders =
                System.Array.Empty<Collider2D>();

            return;
        }

        System.Collections.Generic.List<Collider2D> solids =
            new System.Collections.Generic.List<Collider2D>();

        for (int i = 0;
             i < all.Length;
             i++)
        {
            Collider2D collider =
                all[i];

            if (collider == null ||
                collider.isTrigger ||
                collider.attachedRigidbody != _rb)
            {
                continue;
            }

            solids.Add(
                collider);
        }

        _solidColliders =
            solids.ToArray();
    }

    private void ResolveRefs()
    {
        if (_rb == null)
        {
            _rb =
                GetComponent<Rigidbody2D>();
        }
    }

    private void CacheLayer()
    {
        _bellItemLayer =
            LayerMask.NameToLayer(
                bellItemLayerName);

        if (_bellItemLayer < 0)
        {
            Debug.LogError(
                $"[DivingBellContainedItem:{name}] " +
                $"Physics/GameObject layer '{bellItemLayerName}' does not exist.",
                this);
        }
    }

    private void CachePresentationTargets()
    {
        if (_layerTargets == null ||
            _layerTargets.Length == 0)
        {
            _layerTargets =
                GetComponentsInChildren<Transform>(
                    true);
        }

        if (_spriteRenderers != null &&
            _spriteRenderers.Length > 0 &&
            _relativeSpriteOrders != null &&
            _relativeSpriteOrders.Length ==
                _spriteRenderers.Length)
        {
            return;
        }

        _spriteRenderers =
            GetComponentsInChildren<SpriteRenderer>(
                true);

        _relativeSpriteOrders =
            new int[_spriteRenderers.Length];

        int minimumOrder =
            int.MaxValue;

        for (int i = 0;
             i < _spriteRenderers.Length;
             i++)
        {
            SpriteRenderer renderer =
                _spriteRenderers[i];

            if (renderer == null)
                continue;

            minimumOrder =
                Mathf.Min(
                    minimumOrder,
                    renderer.sortingOrder);
        }

        if (minimumOrder ==
            int.MaxValue)
        {
            minimumOrder =
                0;
        }

        for (int i = 0;
             i < _spriteRenderers.Length;
             i++)
        {
            SpriteRenderer renderer =
                _spriteRenderers[i];

            if (renderer == null)
                continue;

            _relativeSpriteOrders[i] =
                renderer.sortingOrder -
                minimumOrder;
        }
    }

    private void Log(
        string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[DivingBellContainedItem:{name}] {message}",
            this);
    }

    private void LogWarning(
        string message)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning(
            $"[DivingBellContainedItem:{name}] {message}",
            this);
    }
}
