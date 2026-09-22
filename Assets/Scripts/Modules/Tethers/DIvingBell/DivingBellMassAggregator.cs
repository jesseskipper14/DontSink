using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative mass / center-of-mass bridge for a live diving bell.
///
/// Ghost collision deliberately prevents occupants and BellItems from applying
/// solver impulses directly to the REAL bell Rigidbody2D. This component restores
/// their truthful weight and position to the authoritative bell body explicitly:
///
///     deployed/capturing bell mass =
///         structural dry bell + occupants + contained loose items + secured ballast
///
/// Ballast ItemInstances remain nested inside the bell payload ItemInstance so docked
/// persistence/mass propagation stays generic. Their nested mass is subtracted from
/// the payload's centered canonical WorldItem mass and re-added at authored left/right
/// ballast points while deployed.
///
/// The tether is NOT given any synthetic payload mass. It continues to transmit
/// only the reaction force actually solved by TetherConstraint2D, so a bell resting
/// on the seabed with a slack line naturally stops loading the boat through the rope.
///
/// While fully docked, the bell returns to dry Rigidbody state. Boat-side systems
/// remain responsible for docked support:
/// - the stored payload ItemInstance contributes through its installed storage/module;
/// - PlayerBoardingState contributes occupants to the Boat;
/// - BoatOwnedItem / BoatOwnedItemEscapeTracker contributes docked loose cargo.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(TetherPayload))]
[RequireComponent(typeof(DivingBellOccupancy))]
public sealed class DivingBellMassAggregator : MonoBehaviour
{
    private struct MassSample
    {
        public float Mass;
        public Vector2 WorldCenter;

        public MassSample(
            float mass,
            Vector2 worldCenter)
        {
            Mass = mass;
            WorldCenter = worldCenter;
        }
    }

    [Header("Authority")]
    [Tooltip(
        "Single-player default is ON. Future network clients that do not own bell " +
        "physics can disable authority and replicate the authoritative Rigidbody state instead.")]
    [SerializeField] private bool physicsAuthority = true;

    [Tooltip(
        "Global gameplay-authority policy layered on top of the local Physics Authority switch. " +
        "The bell mass/COM solver mutates Rigidbody state only when BOTH allow it.")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("References")]
    [SerializeField] private DivingBellOccupancy occupancy;
    [SerializeField] private TetherPayload payload;
    [SerializeField] private ForceBody2D forceBody;
    [SerializeField] private WorldItem worldItem;
    [SerializeField] private Rigidbody2D bellBody;
    [SerializeField] private DivingBellBallastSystem ballastSystem;

    [Header("Dry Bell Baseline (Runtime)")]
    [SerializeField] private bool hasDryBaseline;
    [SerializeField] private float dryMass;
    [SerializeField] private Vector2 dryLocalCenterOfMass;
    [SerializeField] private float dryInertia;

    [Header("Aggregate Debug")]
    [SerializeField] private bool aggregationActive;
    [SerializeField] private int occupantContributionCount;
    [SerializeField] private int cargoContributionCount;
    [SerializeField] private int ballastContributionCount;
    [SerializeField] private float occupantMassTotal;
    [SerializeField] private float cargoMassTotal;
    [SerializeField] private float ballastMassTotal;
    [SerializeField] private float aggregateMass;
    [SerializeField] private Vector2 aggregateLocalCenterOfMass;
    [SerializeField] private float aggregateInertia;

    [Header("Scene Gizmos")]
    [SerializeField] private bool drawCenterOfMassGizmos = true;
    [SerializeField, Min(0.01f)] private float gizmoRadius = 0.08f;

    private readonly HashSet<DivingBellContainedItem> _containedItems =
        new HashSet<DivingBellContainedItem>();

    private readonly List<DivingBellContainedItem> _cargoRemoveScratch =
        new List<DivingBellContainedItem>();

    private readonly List<MassSample> _massSamples =
        new List<MassSample>(16);

    private ItemInstance _boundPayloadItem;

    public bool PhysicsAuthority =>
        physicsAuthority &&
        GameplayAuthority.CanRun(gameplayAuthorityMode);
    public bool AggregationActive => aggregationActive;
    public float DryMass => dryMass;
    public float AggregateMass => aggregateMass;
    public Vector2 AggregateLocalCenterOfMass => aggregateLocalCenterOfMass;

    private void Awake()
    {
        ResolveRefs();
    }

    private void OnEnable()
    {
        ResolveRefs();
        ReconcileContainedItems();

        if (PhysicsAuthority)
            RefreshPayloadItemBinding();
    }

    private void OnDisable()
    {
        UnbindPayloadItem();
    }

    private void OnDestroy()
    {
        UnbindPayloadItem();
        _containedItems.Clear();
        _cargoRemoveScratch.Clear();
        _massSamples.Clear();
    }

    private void FixedUpdate()
    {
        ResolveRefs();

        if (!PhysicsAuthority ||
            bellBody == null ||
            occupancy == null)
        {
            return;
        }

        RefreshPayloadItemBinding();
        PruneContainedItems();

        bool isDocked =
            IsAuthoritativelyDocked();

        // This is the physical-support handoff only. Ownership/persistence is left
        // untouched. BoatOwnedItemEscapeTracker performs the same rule defensively.
        SetContainedCargoBoatSupport(
            isDocked);

        if (isDocked)
        {
            aggregationActive = false;
            occupantContributionCount = 0;
            cargoContributionCount = 0;
            ballastContributionCount = 0;
            occupantMassTotal = 0f;
            cargoMassTotal = 0f;
            ballastMassTotal = 0f;

            RestoreDryBodyState();
            return;
        }

        EnsureDryBaseline();

        if (!hasDryBaseline)
            return;

        ApplyAggregateBodyState();
    }

    /// <summary>
    /// Future networking seam. The host/server can own the bell Rigidbody while
    /// non-authoritative peers observe replicated state without running a second
    /// independent mass solver.
    /// </summary>
    public void SetPhysicsAuthority(
        bool authoritative)
    {
        physicsAuthority = authoritative;

        if (!PhysicsAuthority)
            UnbindPayloadItem();
        else
            RefreshPayloadItemBinding();
    }

    public void RegisterContainedItem(
        DivingBellContainedItem item)
    {
        if (item == null ||
            occupancy == null ||
            item.CurrentBell != occupancy)
        {
            return;
        }

        _containedItems.Add(
            item);
    }

    public void UnregisterContainedItem(
        DivingBellContainedItem item)
    {
        if (item == null)
            return;

        _containedItems.Remove(
            item);
    }

    [ContextMenu("Debug/Reconcile Contained Items")]
    public void ReconcileContainedItems()
    {
        ResolveRefs();

        if (occupancy == null)
            return;

        DivingBellContainedItem[] allItems =
            FindObjectsByType<DivingBellContainedItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0;
             i < allItems.Length;
             i++)
        {
            DivingBellContainedItem item =
                allItems[i];

            if (item != null &&
                item.CurrentBell == occupancy)
            {
                _containedItems.Add(
                    item);
            }
        }

        PruneContainedItems();
    }

    [ContextMenu("Debug/Recompute Now")]
    public void RecomputeNow()
    {
        ResolveRefs();

        if (!PhysicsAuthority ||
            bellBody == null ||
            occupancy == null)
        {
            return;
        }

        RefreshPayloadItemBinding();
        PruneContainedItems();

        bool isDocked =
            IsAuthoritativelyDocked();

        SetContainedCargoBoatSupport(
            isDocked);

        if (isDocked)
        {
            aggregationActive = false;
            RestoreDryBodyState();
            return;
        }

        EnsureDryBaseline();

        if (hasDryBaseline)
            ApplyAggregateBodyState();
    }

    private void EnsureDryBaseline()
    {
        if (hasDryBaseline ||
            bellBody == null)
        {
            return;
        }

        // A live stored diving-bell shell deliberately has WorldItem.Instance == null.
        // Deployment transfers the SAME ItemInstance into that WorldItem before the
        // first deployed physics tick. Refresh here so the baseline is the canonical
        // ItemInstance/modifier mass, not an arbitrary prefab placeholder value.
        if (worldItem != null &&
            worldItem.Instance != null)
        {
            worldItem.RefreshPhysicalMass();
        }

        dryMass =
            ResolveStructuralDryMassFromCurrentBody();

        dryLocalCenterOfMass =
            bellBody.centerOfMass;

        dryInertia =
            ResolveDryInertia(
                dryMass);

        hasDryBaseline =
            IsFinite(dryMass) &&
            IsFinite(dryLocalCenterOfMass) &&
            IsFinite(dryInertia) &&
            dryInertia > 0f;

        if (!hasDryBaseline)
        {
            dryMass = 0f;
            dryLocalCenterOfMass = Vector2.zero;
            dryInertia = 0f;
        }
    }

    private void RefreshPayloadItemBinding()
    {
        ItemInstance current =
            worldItem != null
                ? worldItem.Instance
                : null;

        if (ReferenceEquals(
                current,
                _boundPayloadItem))
        {
            return;
        }

        UnbindPayloadItem();

        _boundPayloadItem =
            current;

        if (_boundPayloadItem == null)
            return;

        _boundPayloadItem.Changed +=
            HandlePayloadItemChanged;

        // If this is the first deployed frame, capture will happen below. If the
        // baseline already exists, this updates only dry mass/inertia while keeping
        // the original dry COM authored for this physical shell.
        if (worldItem != null)
            worldItem.RefreshPhysicalMass();

        if (!hasDryBaseline)
        {
            EnsureDryBaseline();
        }
        else
        {
            RefreshDryMassFromCurrentBody();
        }
    }

    private void UnbindPayloadItem()
    {
        if (_boundPayloadItem != null)
        {
            _boundPayloadItem.Changed -=
                HandlePayloadItemChanged;
        }

        _boundPayloadItem =
            null;
    }

    private void HandlePayloadItemChanged()
    {
        if (!PhysicsAuthority)
            return;

        if (worldItem == null ||
            _boundPayloadItem == null ||
            !ReferenceEquals(
                worldItem.Instance,
                _boundPayloadItem))
        {
            return;
        }

        // WorldItem is the canonical final-mass authority for its ItemInstance and
        // any IWorldItemMassModifier components. Reuse it instead of duplicating the
        // item's mass policy here.
        worldItem.RefreshPhysicalMass();

        if (!hasDryBaseline)
            EnsureDryBaseline();
        else
            RefreshDryMassFromCurrentBody();

        if (PhysicsAuthority &&
            !IsAuthoritativelyDocked())
        {
            ApplyAggregateBodyState();
        }
    }

    private void RefreshDryMassFromCurrentBody()
    {
        if (bellBody == null)
            return;

        float nextDryMass =
            ResolveStructuralDryMassFromCurrentBody();

        if (!IsFinite(nextDryMass) ||
            nextDryMass <= 0f)
        {
            return;
        }

        dryMass =
            nextDryMass;

        dryInertia =
            ResolveDryInertia(
                dryMass);
    }

    private void RestoreDryBodyState()
    {
        if (!hasDryBaseline ||
            bellBody == null)
        {
            aggregateMass =
                bellBody != null
                    ? bellBody.mass
                    : 0f;

            aggregateLocalCenterOfMass =
                bellBody != null
                    ? bellBody.centerOfMass
                    : Vector2.zero;

            aggregateInertia =
                bellBody != null
                    ? bellBody.inertia
                    : 0f;

            return;
        }

        bellBody.mass =
            dryMass;

        bellBody.centerOfMass =
            dryLocalCenterOfMass;

        bellBody.inertia =
            Mathf.Max(
                0.0001f,
                dryInertia);

        aggregateMass =
            dryMass;

        aggregateLocalCenterOfMass =
            dryLocalCenterOfMass;

        aggregateInertia =
            dryInertia;
    }

    private void ApplyAggregateBodyState()
    {
        if (!hasDryBaseline ||
            bellBody == null ||
            occupancy == null)
        {
            return;
        }

        _massSamples.Clear();

        occupantContributionCount = 0;
        cargoContributionCount = 0;
        ballastContributionCount = 0;
        occupantMassTotal = 0f;
        cargoMassTotal = 0f;
        ballastMassTotal = 0f;

        Vector2 dryWorldCenter =
            transform.TransformPoint(
                dryLocalCenterOfMass);

        float totalMass =
            Mathf.Max(
                0.0001f,
                dryMass);

        Vector2 weightedWorldSum =
            totalMass *
            dryWorldCenter;

        CollectOccupantSamples(
            ref totalMass,
            ref weightedWorldSum);

        CollectCargoSamples(
            ref totalMass,
            ref weightedWorldSum);

        CollectBallastSamples(
            ref totalMass,
            ref weightedWorldSum);

        if (totalMass <= 0.0001f ||
            !IsFinite(totalMass) ||
            !IsFinite(weightedWorldSum))
        {
            RestoreDryBodyState();
            return;
        }

        Vector2 aggregateWorldCenter =
            weightedWorldSum /
            totalMass;

        Vector2 localCenter =
            transform.InverseTransformPoint(
                aggregateWorldCenter);

        if (!IsFinite(localCenter))
        {
            RestoreDryBodyState();
            return;
        }

        // Parallel-axis approximation for the composite load. Occupants/cargo are
        // treated as point masses because they are not rigidly welded to the shell;
        // their own Rigidbody rotational inertia should not be imposed on the bell.
        float totalInertia =
            Mathf.Max(
                0.0001f,
                dryInertia) +
            dryMass *
            (dryWorldCenter - aggregateWorldCenter).sqrMagnitude;

        for (int i = 0;
             i < _massSamples.Count;
             i++)
        {
            MassSample sample =
                _massSamples[i];

            totalInertia +=
                sample.Mass *
                (sample.WorldCenter - aggregateWorldCenter).sqrMagnitude;
        }

        if (!IsFinite(totalInertia) ||
            totalInertia <= 0f)
        {
            totalInertia =
                Mathf.Max(
                    0.0001f,
                    dryInertia);
        }

        bellBody.mass =
            totalMass;

        bellBody.centerOfMass =
            localCenter;

        bellBody.inertia =
            totalInertia;

        aggregationActive =
            true;

        aggregateMass =
            totalMass;

        aggregateLocalCenterOfMass =
            localCenter;

        aggregateInertia =
            totalInertia;
    }

    private void CollectOccupantSamples(
        ref float totalMass,
        ref Vector2 weightedWorldSum)
    {
        IReadOnlyList<PlayerBellOccupantState> occupants =
            occupancy != null
                ? occupancy.Occupants
                : null;

        if (occupants == null)
            return;

        for (int i = 0;
             i < occupants.Count;
             i++)
        {
            PlayerBellOccupantState occupant =
                occupants[i];

            if (occupant == null ||
                !occupant.IsInside(
                    occupancy))
            {
                continue;
            }

            Rigidbody2D occupantBody =
                occupant.GetComponent<Rigidbody2D>() ??
                occupant.GetComponentInChildren<Rigidbody2D>(
                    true);

            PlayerLoadState loadState =
                occupant.GetComponent<PlayerLoadState>() ??
                occupant.GetComponentInChildren<PlayerLoadState>(
                    true) ??
                occupant.GetComponentInParent<PlayerLoadState>();

            float mass =
                loadState != null
                    ? loadState.TotalPhysicalMass
                    : (occupantBody != null
                        ? occupantBody.mass
                        : 0f);

            if (!IsUsableContributionMass(
                    mass))
            {
                continue;
            }

            Vector2 worldCenter =
                occupantBody != null
                    ? occupantBody.worldCenterOfMass
                    : (Vector2)occupant.transform.position;

            if (!IsFinite(worldCenter))
                continue;

            AddMassSample(
                mass,
                worldCenter,
                ref totalMass,
                ref weightedWorldSum);

            occupantContributionCount++;
            occupantMassTotal += mass;
        }
    }

    private void CollectCargoSamples(
        ref float totalMass,
        ref Vector2 weightedWorldSum)
    {
        foreach (DivingBellContainedItem item
                 in _containedItems)
        {
            if (!IsLiveContainedItem(
                    item))
            {
                continue;
            }

            Rigidbody2D itemBody =
                item.GetComponent<Rigidbody2D>();

            if (itemBody == null)
                continue;

            float mass =
                itemBody.mass;

            if (!IsUsableContributionMass(
                    mass))
            {
                continue;
            }

            Vector2 worldCenter =
                itemBody.worldCenterOfMass;

            if (!IsFinite(worldCenter))
                continue;

            AddMassSample(
                mass,
                worldCenter,
                ref totalMass,
                ref weightedWorldSum);

            cargoContributionCount++;
            cargoMassTotal += mass;
        }
    }

    private void CollectBallastSamples(
        ref float totalMass,
        ref Vector2 weightedWorldSum)
    {
        if (ballastSystem == null)
            return;

        for (int slotIndex = 0;
             slotIndex < DivingBellBallastSystem.RequiredSlotCount;
             slotIndex++)
        {
            if (!ballastSystem.TryGetMassSample(
                    slotIndex,
                    out float mass,
                    out Vector2 worldCenter))
            {
                continue;
            }

            if (!IsUsableContributionMass(mass) ||
                !IsFinite(worldCenter))
            {
                continue;
            }

            AddMassSample(
                mass,
                worldCenter,
                ref totalMass,
                ref weightedWorldSum);

            ballastContributionCount++;
            ballastMassTotal += mass;
        }
    }

    private void AddMassSample(
        float mass,
        Vector2 worldCenter,
        ref float totalMass,
        ref Vector2 weightedWorldSum)
    {
        _massSamples.Add(
            new MassSample(
                mass,
                worldCenter));

        totalMass +=
            mass;

        weightedWorldSum +=
            mass *
            worldCenter;
    }

    private void SetContainedCargoBoatSupport(
        bool supportedByBoat)
    {
        foreach (DivingBellContainedItem item
                 in _containedItems)
        {
            if (!IsLiveContainedItem(
                    item))
            {
                continue;
            }

            BoatOwnedItem owned =
                item.GetComponent<BoatOwnedItem>();

            if (owned != null)
            {
                owned.SetPhysicallyContainedByOwningBoat(
                    supportedByBoat);
            }
        }
    }

    private bool IsAuthoritativelyDocked()
    {
        if (payload == null &&
            occupancy != null)
        {
            payload =
                occupancy.Payload;
        }

        if (payload == null)
            return false;

        TetherPayloadDock activeDock =
            payload.ActiveDock;

        return
            activeDock != null &&
            activeDock.IsDocked(
                payload);
    }

    private void PruneContainedItems()
    {
        _cargoRemoveScratch.Clear();

        foreach (DivingBellContainedItem item
                 in _containedItems)
        {
            if (!IsLiveContainedItem(
                    item))
            {
                _cargoRemoveScratch.Add(
                    item);
            }
        }

        for (int i = 0;
             i < _cargoRemoveScratch.Count;
             i++)
        {
            _containedItems.Remove(
                _cargoRemoveScratch[i]);
        }
    }

    private bool IsLiveContainedItem(
        DivingBellContainedItem item)
    {
        return
            item != null &&
            item.isActiveAndEnabled &&
            occupancy != null &&
            item.IsContainedInBell &&
            item.CurrentBell == occupancy;
    }

    private float ResolveStructuralDryMassFromCurrentBody()
    {
        if (bellBody == null)
            return 0.0001f;

        float canonicalWorldMass =
            bellBody.mass;

        if (!IsFinite(canonicalWorldMass))
            canonicalWorldMass = 0.0001f;

        float nestedBallastMass =
            ballastSystem != null
                ? ballastSystem.TotalBallastMass
                : 0f;

        if (!IsFinite(nestedBallastMass) ||
            nestedBallastMass < 0f)
        {
            nestedBallastMass = 0f;
        }

        // The payload ItemInstance.TotalMass already includes ballast because the
        // ballast slots live inside its portable-container state. For deployed
        // physics, remove that centered nested mass here and re-add each ballast
        // item at its actual authored left/right contribution point.
        return
            Mathf.Max(
                0.0001f,
                canonicalWorldMass -
                nestedBallastMass);
    }

    private float ResolveDryInertia(
        float mass)
    {
        float safeMass =
            Mathf.Max(
                0.0001f,
                mass);

        if (forceBody != null)
        {
            Vector3 scale =
                transform.lossyScale;

            float physicalWidth =
                Mathf.Max(
                    0.01f,
                    forceBody.Width *
                    Mathf.Abs(scale.x));

            float physicalHeight =
                Mathf.Max(
                    0.01f,
                    forceBody.Height *
                    Mathf.Abs(scale.y));

            return
                safeMass *
                (physicalWidth * physicalWidth +
                 physicalHeight * physicalHeight) /
                12f;
        }

        if (bellBody != null &&
            IsFinite(bellBody.inertia) &&
            bellBody.inertia > 0f)
        {
            return bellBody.inertia;
        }

        return
            safeMass *
            0.1f;
    }

    private void ResolveRefs()
    {
        if (bellBody == null)
            bellBody = GetComponent<Rigidbody2D>();

        if (occupancy == null)
        {
            occupancy =
                GetComponent<DivingBellOccupancy>() ??
                GetComponentInChildren<DivingBellOccupancy>(
                    true);
        }

        if (payload == null)
        {
            payload =
                GetComponent<TetherPayload>() ??
                (occupancy != null
                    ? occupancy.Payload
                    : null);
        }

        if (forceBody == null)
        {
            forceBody =
                GetComponent<ForceBody2D>() ??
                GetComponentInChildren<ForceBody2D>(
                    true);
        }

        if (worldItem == null)
        {
            worldItem =
                GetComponent<WorldItem>() ??
                GetComponentInChildren<WorldItem>(
                    true);
        }

        if (ballastSystem == null)
        {
            ballastSystem =
                GetComponent<DivingBellBallastSystem>() ??
                GetComponentInChildren<DivingBellBallastSystem>(
                    true);
        }
    }

    private static bool IsUsableContributionMass(
        float mass)
    {
        return
            mass > 0f &&
            IsFinite(mass);
    }

    private static bool IsFinite(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    private static bool IsFinite(
        Vector2 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawCenterOfMassGizmos)
            return;

        Rigidbody2D rb =
            bellBody != null
                ? bellBody
                : GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        float radius =
            Mathf.Max(
                0.01f,
                gizmoRadius);

        if (hasDryBaseline)
        {
            Gizmos.color =
                Color.cyan;

            Gizmos.DrawWireSphere(
                transform.TransformPoint(
                    dryLocalCenterOfMass),
                radius);
        }

        Gizmos.color =
            Color.yellow;

        Gizmos.DrawSphere(
            rb.worldCenterOfMass,
            radius * 0.75f);
    }
#endif
}
