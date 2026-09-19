using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative runtime for a handheld sounding line.
///
/// The held ItemInstance remains the authority for the sounder and its nested
/// rope inventory. The deployed sounding weight is a temporary physical proxy:
/// it is NOT initialized with another ItemInstance, so no duplicate inventory
/// item is created.
///
/// Control arrives as HandheldSoundingLineIntentRequest. The current local
/// intent source calls this directly; a future multiplayer transport can send
/// the same request to host authority.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TetherConstraint2D))]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(HandheldSoundingLineHUD))]
public sealed class HandheldSoundingLineController :
    MonoBehaviour
{
    public enum RuntimeStatus
    {
        NotHeld = 0,
        Ready = 1,
        NoLine = 2,
        Sinking = 3,
        Bottom = 4,
        FullyExtended = 5,
        Retrieving = 6,
        BoardedBlocked = 7
    }

    [Header("Player")]
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerBoardingState boardingState;

    [Tooltip(
        "The portable-container ItemDefinition that represents the handheld sounder.")]
    [SerializeField] private ItemDefinition soundingLineItemDefinition;

    [Header("Line")]
    [SerializeField] private TetherLineCatalog lineCatalog;

    [Tooltip(
        "A small amount of extra paid-out line maintained while free sounding. " +
        "This keeps the weight from repeatedly slamming into a perfectly taut constraint.")]
    [SerializeField, Min(0f)]
    private float freePayoutSlackMeters = 0.10f;

    [Tooltip("Starting paid-out line when the weight is first released.")]
    [SerializeField, Min(0.01f)]
    private float initialDeployedLengthMeters = 0.05f;

    [Header("Weight")]
    [Tooltip(
        "Physical sounding-weight WorldItem prefab. It must have Rigidbody2D, " +
        "TetherPayload, a Collider2D, and SoundingWeightBottomContact2D on the root.")]
    [SerializeField] private WorldItem soundingWeightPrefab;

    [Tooltip(
        "World-space point the line leaves the player's hand. Falls back to this transform.")]
    [SerializeField] private Transform tetherExitPoint;

    [SerializeField]
    private Vector2 spawnOffset =
        new Vector2(0f, -0.05f);

    [Header("Hand Retrieval")]
    [SerializeField, Min(0.01f)]
    private float retrieveSpeedMetersPerSecond = 2.0f;

    [SerializeField, Min(0.01f)]
    private float dockingDistance = 0.35f;

    [Tooltip(
        "When hand retrieval has shortened the paid-out line to this amount, " +
        "treat the weight as being within Steve's reach and catch/stow it. " +
        "This prevents a tiny pendulum from orbiting forever just outside the old dock radius.")]
    [SerializeField, Min(0.01f)]
    private float autoCatchRemainingLineMeters = 0.60f;

    [Header("Held Visual")]
    [Tooltip(
        "Optional root of the normal held-item visual. Auto-resolves a child named " +
        "'HeldItemVisual' if left empty. Its renderers are suppressed while the " +
        "physical sounding weight is deployed, without disabling the held-item system.")]
    [SerializeField] private GameObject heldItemVisualRoot;

    [SerializeField] private bool hideHeldItemVisualWhileDeployed = true;

    [Header("Runtime Debug")]
    [SerializeField] private RuntimeStatus status;
    [SerializeField] private bool deployed;
    [SerializeField] private bool retrieving;
    [SerializeField] private bool bottomLatched;
    [SerializeField] private float deployedLengthMeters;
    [SerializeField] private float deployedAvailableLineMeters;
    [SerializeField] private float bottomRopeOutMeters;

    private TetherConstraint2D _constraint;

    private ItemInstance _activeSounderItem;
    private WorldItem _spawnedWeight;
    private TetherPayload _payload;
    private SoundingWeightBottomContact2D _bottomContact;

    private readonly Dictionary<Renderer, bool> _heldVisualOriginalForceOff =
        new Dictionary<Renderer, bool>();

    private bool _heldVisualSuppressed;

    private const float LineEpsilonMeters = 0.001f;

    public RuntimeStatus Status => status;
    public bool IsDeployed => deployed;
    public bool IsRetrieving => retrieving;
    public bool BottomLatched => bottomLatched;

    /// <summary>
    /// Small presentation hint for the direct HUD.
    /// The local prototype input currently uses G.
    /// </summary>
    public string ControlHintText =>
        status switch
        {
            RuntimeStatus.Ready => "PRESS G TO DEPLOY",
            RuntimeStatus.Sinking => "PRESS G TO RETRIEVE",
            RuntimeStatus.Bottom => "PRESS G TO RETRIEVE",
            RuntimeStatus.FullyExtended => "PRESS G TO RETRIEVE",
            RuntimeStatus.Retrieving => "PRESS G TO RELEASE",
            RuntimeStatus.BoardedBlocked => "MOVE TO EXTERIOR DECK",
            RuntimeStatus.NoLine => "LOAD LINE TO DEPLOY",
            _ => string.Empty
        };

    /// <summary>
    /// True when this exact ItemInstance is the sounding-line item whose
    /// physical proxy is currently deployed.
    /// </summary>
    public bool IsActiveDeployedSounder(
        ItemInstance item)
    {
        return
            deployed &&
            item != null &&
            ReferenceEquals(
                item,
                _activeSounderItem);
    }

    public float RopeOutMeters =>
        bottomLatched && !retrieving
            ? bottomRopeOutMeters
            : deployedLengthMeters;

    public float AvailableLineMeters =>
        deployed
            ? Mathf.Max(0f, deployedAvailableLineMeters)
            : CalculateCurrentlyLoadedLine();

    public float CurrentDistanceMeters =>
        _constraint != null
            ? _constraint.CurrentDistance
            : 0f;

    public bool ShouldShowReadout =>
        deployed ||
        TryGetHeldSounder(
            out _);

    public string StatusText =>
        status switch
        {
            RuntimeStatus.NotHeld => "NOT HELD",
            RuntimeStatus.Ready => "READY",
            RuntimeStatus.NoLine => "NO LINE LOADED",
            RuntimeStatus.Sinking => "SINKING",
            RuntimeStatus.Bottom => "BOTTOM",
            RuntimeStatus.FullyExtended => "LINE FULLY EXTENDED",
            RuntimeStatus.Retrieving => "RETRIEVING",
            RuntimeStatus.BoardedBlocked => "CANNOT DEPLOY SOUNDING LINE FROM INSIDE",
            _ => status.ToString().ToUpperInvariant()
        };

    private Transform ExitPoint =>
        tetherExitPoint != null
            ? tetherExitPoint
            : transform;

    private void Reset()
    {
        ResolveRefs();

        if (tetherExitPoint == null)
            tetherExitPoint = transform;
    }

    private void Awake()
    {
        ResolveRefs();
        RefreshIdleStatus();
    }

    private void OnEnable()
    {
        ResolveRefs();
        RefreshIdleStatus();
    }

    private void OnDisable()
    {
        CleanupDeployedWeight();
        status = RuntimeStatus.NotHeld;
    }

    private void FixedUpdate()
    {
        ResolveRefs();

        if (!deployed)
        {
            RefreshIdleStatus();
            return;
        }

        if (!TryGetHeldSounder(
                out ItemInstance held) ||
            !ReferenceEquals(
                held,
                _activeSounderItem))
        {
            // A deployed sounding line must never "teleport home" merely
            // because some external inventory path removed it from Hands.
            //
            // Normal player inventory routes intercept this transition before
            // removal and call TryReleaseDeployedSounderToWorld(). This fallback
            // keeps the physical proxy alive if another system changes Hands.
            PromoteProxyToWorldWithoutRemovingFromHands(
                _activeSounderItem,
                out _);

            RefreshIdleStatus();
            return;
        }

        if (_payload == null ||
            _spawnedWeight == null ||
            _constraint == null ||
            !_constraint.IsAttached)
        {
            CleanupDeployedWeight();
            RefreshIdleStatus();
            return;
        }

        if (_constraint.TryConsumeBreak(
                out _))
        {
            // This pass intentionally gives the handheld sounder no automatic
            // line-breaking ratings, so this is only a defensive recovery path.
            CleanupDeployedWeight();
            status = RuntimeStatus.Ready;
            return;
        }

        float dt =
            Mathf.Max(
                0f,
                Time.fixedDeltaTime);

        if (retrieving)
        {
            TickRetrieval(dt);
            return;
        }

        TickFreePayout();
    }

    /// <summary>
    /// Releases the currently deployed sounder from the player's Hands at the
    /// EXACT position of the already-visible physical proxy.
    ///
    /// This is the authoritative state transition used when the player tries
    /// to drag/stow/drop the deployed sounder instead of retrieving it.
    ///
    /// No replacement WorldItem is spawned near the player. The existing
    /// sounding-weight proxy is promoted into the real WorldItem and receives
    /// the SAME ItemInstance that previously lived in Hands.
    /// </summary>
    public bool TryReleaseDeployedSounderToWorld(
        ItemInstance item,
        out WorldItem releasedWorldItem)
    {
        releasedWorldItem =
            null;

        if (!IsActiveDeployedSounder(
                item))
        {
            return false;
        }

        if (equipment == null ||
            !ReferenceEquals(
                equipment.Get(
                    BottomBarSlotType.Hands),
                item))
        {
            return false;
        }

        if (_spawnedWeight == null ||
            _payload == null)
        {
            return false;
        }

        // Restore the normal held-visual renderer state BEFORE removing the
        // equipment item. PlayerHeldItemVisual may rebuild immediately when
        // PlayerEquipment raises its change notification.
        SetHeldVisualSuppressed(
            false);

        ItemInstance removed =
            equipment.Remove(
                BottomBarSlotType.Hands);

        if (!ReferenceEquals(
                removed,
                item))
        {
            // Defensive rollback. The proxy remains deployed if Hands could
            // not authoritatively yield the expected ItemInstance.
            if (removed != null)
            {
                equipment.TryPlace(
                    BottomBarSlotType.Hands,
                    removed,
                    out _);
            }

            SetHeldVisualSuppressed(
                true);

            return false;
        }

        return PromoteProxyToWorldWithoutRemovingFromHands(
            removed,
            out releasedWorldItem);
    }

    /// <summary>
    /// Converts the already-existing physical proxy into an ordinary WorldItem
    /// without changing its position, rotation, or Rigidbody2D velocity.
    ///
    /// The caller is responsible for inventory ownership. The normal path
    /// removes the item from Hands first; the fallback FixedUpdate path uses
    /// this only to avoid destroying physical state if another system removed
    /// the item unexpectedly.
    /// </summary>
    private bool PromoteProxyToWorldWithoutRemovingFromHands(
        ItemInstance item,
        out WorldItem releasedWorldItem)
    {
        releasedWorldItem =
            null;

        if (item == null ||
            item.Definition == null ||
            _spawnedWeight == null)
        {
            return false;
        }

        SetHeldVisualSuppressed(
            false);

        if (_constraint != null)
            _constraint.Detach();

        if (_payload != null)
        {
            _payload.SetTetherCollisionLayerActive(
                false);
        }

        WorldItem promoted =
            _spawnedWeight;

        // This is the critical authority handoff:
        // physical proxy -> normal world item, SAME ItemInstance.
        promoted.Initialize(
            item);

        if (soundingWeightPrefab != null)
        {
            promoted.name =
                soundingWeightPrefab.name;
        }

        releasedWorldItem =
            promoted;

        ClearDeploymentTrackingWithoutDestroy();

        status =
            RuntimeStatus.NotHeld;

        return true;
    }

    public bool TryApplyIntent(
        HandheldSoundingLineIntentRequest request,
        out string message)
    {
        if (request.version !=
            HandheldSoundingLineIntentRequest.CurrentVersion)
        {
            message =
                $"Unsupported sounding-line intent version {request.version}.";

            return false;
        }

        return TryApplyIntent(
            request.intent,
            out message);
    }

    public bool TryApplyIntent(
        HandheldSoundingLineIntent intent,
        out string message)
    {
        switch (intent)
        {
            case HandheldSoundingLineIntent.Deploy:
                return TryDeploy(
                    out message);

            case HandheldSoundingLineIntent.Retrieve:
                return TryBeginRetrieve(
                    out message);

            case HandheldSoundingLineIntent.Release:
                return TryReleaseAgain(
                    out message);

            default:
                message =
                    $"Unknown sounding-line intent: {intent}.";

                return false;
        }
    }

    private bool TryDeploy(
        out string message)
    {
        if (deployed)
        {
            message =
                "Sounding weight is already deployed.";

            return false;
        }

        if (!TryGetHeldSounder(
                out ItemInstance sounder))
        {
            message =
                "Hold the sounding line in Hands first.";

            return false;
        }

        if (IsInsideBoatInterior())
        {
            status =
                RuntimeStatus.BoardedBlocked;

            message =
                "Move to the exterior deck before using the sounding line.";

            return false;
        }

        if (lineCatalog == null)
        {
            message =
                "Sounding line has no TetherLineCatalog assigned.";

            return false;
        }

        float available =
            CalculateAvailableLine(
                sounder);

        if (available <=
            LineEpsilonMeters)
        {
            status =
                RuntimeStatus.NoLine;

            message =
                "No valid tether line is loaded.";

            return false;
        }

        if (soundingWeightPrefab == null)
        {
            message =
                "No sounding-weight prefab is assigned.";

            return false;
        }

        TetherPayload prefabPayload =
            soundingWeightPrefab
                .GetComponent<TetherPayload>();

        SoundingWeightBottomContact2D prefabBottom =
            soundingWeightPrefab
                .GetComponent<SoundingWeightBottomContact2D>();

        if (prefabPayload == null)
        {
            message =
                "Sounding-weight prefab needs TetherPayload on its root.";

            return false;
        }

        if (prefabBottom == null)
        {
            message =
                "Sounding-weight prefab needs SoundingWeightBottomContact2D on its root.";

            return false;
        }

        Transform exit =
            ExitPoint;

        Vector3 spawnPosition =
            exit.position +
            (Vector3)spawnOffset;

        WorldItem spawned =
            Instantiate(
                soundingWeightPrefab,
                spawnPosition,
                Quaternion.identity);

        if (spawned == null)
        {
            message =
                "Could not spawn sounding weight.";

            return false;
        }

        spawned.name =
            $"{soundingWeightPrefab.name} (Sounding Proxy)";

        // DO NOT call WorldItem.Initialize here.
        // The original sounder's ItemInstance remains in the player's Hands and
        // remains the sole authority for its four nested rope slots.
        TetherPayload payload =
            spawned.GetComponent<TetherPayload>();

        SoundingWeightBottomContact2D bottomContact =
            spawned.GetComponent<SoundingWeightBottomContact2D>();

        if (payload == null ||
            payload.Rigidbody == null ||
            bottomContact == null)
        {
            Destroy(
                spawned.gameObject);

            message =
                "Spawned sounding-weight prefab is missing required physics components.";

            return false;
        }

        if (!payload.SetTetherCollisionLayerActive(
                true))
        {
            Destroy(
                spawned.gameObject);

            message =
                "Could not apply TetherPayload collision layer.";

            return false;
        }

        ResolveConstraint();

        if (_constraint == null)
        {
            payload.SetTetherCollisionLayerActive(
                false);

            Destroy(
                spawned.gameObject);

            message =
                "No TetherConstraint2D is available on the player.";

            return false;
        }

        float initialLength =
            Mathf.Clamp(
                initialDeployedLengthMeters,
                0.01f,
                available);

        // V1 handheld rule:
        // startBody is intentionally null. The rope endpoint follows the hand
        // transform in world space, but the tiny sounding weight does not yank
        // the player's locomotion Rigidbody around. We can make hand-force
        // transfer physical later if it proves valuable to gameplay.
        _constraint.Bind(
            exit,
            null,
            payload,
            initialLength,
            0f,
            0f);

        if (!_constraint.IsAttached)
        {
            payload.SetTetherCollisionLayerActive(
                false);

            Destroy(
                spawned.gameObject);

            message =
                "Sounding-line tether failed to attach.";

            return false;
        }

        _activeSounderItem =
            sounder;

        _spawnedWeight =
            spawned;

        _payload =
            payload;

        _bottomContact =
            bottomContact;

        deployed =
            true;

        retrieving =
            false;

        bottomLatched =
            false;

        deployedLengthMeters =
            initialLength;

        deployedAvailableLineMeters =
            available;

        bottomRopeOutMeters =
            0f;

        status =
            RuntimeStatus.Sinking;

        SetHeldVisualSuppressed(
            true);

        message =
            $"Sounding weight deployed with {available:0.0} m of line available.";

        return true;
    }

    private bool TryBeginRetrieve(
        out string message)
    {
        if (!deployed)
        {
            message =
                "Sounding weight is not deployed.";

            return false;
        }

        retrieving =
            true;

        status =
            RuntimeStatus.Retrieving;

        message =
            "Retrieving sounding weight.";

        return true;
    }

    private bool TryReleaseAgain(
        out string message)
    {
        if (!deployed)
        {
            return TryDeploy(
                out message);
        }

        retrieving =
            false;

        // Releasing after a partial retrieval starts a fresh sounding run.
        bottomLatched =
            false;

        bottomRopeOutMeters =
            0f;

        status =
            RuntimeStatus.Sinking;

        message =
            "Sounding line released.";

        return true;
    }

    private void TickFreePayout()
    {
        if (_constraint == null)
            return;

        if (!bottomLatched &&
            _bottomContact != null &&
            _bottomContact.IsOnBottom)
        {
            bottomLatched =
                true;

            bottomRopeOutMeters =
                Mathf.Clamp(
                    deployedLengthMeters,
                    0f,
                    deployedAvailableLineMeters);

            status =
                RuntimeStatus.Bottom;

            return;
        }

        if (bottomLatched)
        {
            status =
                RuntimeStatus.Bottom;

            return;
        }

        if (deployedLengthMeters >=
            deployedAvailableLineMeters -
            LineEpsilonMeters)
        {
            deployedLengthMeters =
                deployedAvailableLineMeters;

            _constraint.SetDeployedLength(
                deployedLengthMeters);

            status =
                RuntimeStatus.FullyExtended;

            return;
        }

        float target =
            Mathf.Min(
                deployedAvailableLineMeters,
                Mathf.Max(
                    deployedLengthMeters,
                    _constraint.CurrentDistance +
                    freePayoutSlackMeters));

        deployedLengthMeters =
            target;

        _constraint.SetDeployedLength(
            deployedLengthMeters);

        status =
            RuntimeStatus.Sinking;
    }

    private void TickRetrieval(
        float dt)
    {
        if (_constraint == null ||
            _spawnedWeight == null)
        {
            CleanupDeployedWeight();
            return;
        }

        bottomLatched =
            false;

        bottomRopeOutMeters =
            0f;

        float minimum =
            Mathf.Min(
                deployedAvailableLineMeters,
                Mathf.Max(
                    0.01f,
                    initialDeployedLengthMeters));

        deployedLengthMeters =
            Mathf.Max(
                minimum,
                deployedLengthMeters -
                retrieveSpeedMetersPerSecond *
                dt);

        _constraint.SetDeployedLength(
            deployedLengthMeters);

        status =
            RuntimeStatus.Retrieving;

        // Hand-over-hand retrieval does not need the little plumb bob to solve
        // the last half-meter as a perfect physics pendulum. Once the remaining
        // paid-out line is within Steve's reach, catch it deterministically.
        if (deployedLengthMeters <=
            Mathf.Max(
                dockingDistance,
                autoCatchRemainingLineMeters))
        {
            CleanupDeployedWeight();
            RefreshIdleStatus();
            return;
        }

        float distanceToHand =
            Vector2.Distance(
                _spawnedWeight.transform.position,
                ExitPoint.position);

        if (distanceToHand <=
                dockingDistance ||
            (deployedLengthMeters <=
                 minimum +
                 LineEpsilonMeters &&
             _constraint.CurrentDistance <=
                 dockingDistance *
                 1.5f))
        {
            CleanupDeployedWeight();
            RefreshIdleStatus();
        }
    }

    private void CleanupDeployedWeight()
    {
        SetHeldVisualSuppressed(
            false);

        if (_constraint != null)
            _constraint.Detach();

        if (_payload != null)
        {
            _payload.SetTetherCollisionLayerActive(
                false);
        }

        if (_spawnedWeight != null)
        {
            Destroy(
                _spawnedWeight.gameObject);
        }

        ClearDeploymentTrackingWithoutDestroy();
    }

    private void ClearDeploymentTrackingWithoutDestroy()
    {
        _activeSounderItem =
            null;

        _spawnedWeight =
            null;

        _payload =
            null;

        _bottomContact =
            null;

        deployed =
            false;

        retrieving =
            false;

        bottomLatched =
            false;

        deployedLengthMeters =
            0f;

        deployedAvailableLineMeters =
            0f;

        bottomRopeOutMeters =
            0f;
    }

    private void RefreshIdleStatus()
    {
        if (!TryGetHeldSounder(
                out ItemInstance held))
        {
            status =
                RuntimeStatus.NotHeld;

            return;
        }

        if (IsInsideBoatInterior())
        {
            status =
                RuntimeStatus.BoardedBlocked;

            return;
        }

        status =
            CalculateAvailableLine(
                held) >
            LineEpsilonMeters
                ? RuntimeStatus.Ready
                : RuntimeStatus.NoLine;
    }

    private bool TryGetHeldSounder(
        out ItemInstance sounder)
    {
        sounder =
            null;

        if (equipment == null ||
            soundingLineItemDefinition == null)
        {
            return false;
        }

        ItemInstance hands =
            equipment.Get(
                BottomBarSlotType.Hands);

        if (hands == null ||
            hands.Definition == null ||
            !ReferenceEquals(
                hands.Definition,
                soundingLineItemDefinition))
        {
            return false;
        }

        hands.EnsureContainerStateMatchesDefinition();

        sounder =
            hands;

        return true;
    }

    private float CalculateCurrentlyLoadedLine()
    {
        return TryGetHeldSounder(
                out ItemInstance sounder)
            ? CalculateAvailableLine(
                sounder)
            : 0f;
    }

    private float CalculateAvailableLine(
        ItemInstance sounder)
    {
        if (sounder == null ||
            sounder.ContainerState == null ||
            lineCatalog == null)
        {
            return 0f;
        }

        float total =
            0f;

        ItemContainerState state =
            sounder.ContainerState;

        for (int i = 0;
             i < state.SlotCount;
             i++)
        {
            InventorySlot slot =
                state.GetSlot(i);

            ItemInstance line =
                slot != null
                    ? slot.Instance
                    : null;

            if (line == null ||
                line.Definition == null ||
                line.Quantity <= 0)
            {
                continue;
            }

            if (!lineCatalog.TryGet(
                    line.Definition,
                    out TetherLineCatalog.Entry entry) ||
                entry == null)
            {
                continue;
            }

            total +=
                entry.LengthMeters *
                Mathf.Max(
                    0,
                    line.Quantity);
        }

        return Mathf.Max(
            0f,
            total);
    }

    private void ResolveRefs()
    {
        if (equipment == null)
        {
            equipment =
                GetComponent<PlayerEquipment>();

            if (equipment == null)
            {
                equipment =
                    GetComponentInChildren<PlayerEquipment>(
                        true);
            }

            if (equipment == null)
            {
                equipment =
                    GetComponentInParent<PlayerEquipment>();
            }
        }

        if (boardingState == null)
        {
            boardingState =
                GetComponent<PlayerBoardingState>();

            if (boardingState == null)
            {
                boardingState =
                    GetComponentInParent<PlayerBoardingState>();
            }

            if (boardingState == null)
            {
                boardingState =
                    GetComponentInChildren<PlayerBoardingState>(
                        true);
            }
        }

        ResolveHeldItemVisualRoot();
        ResolveConstraint();
    }

    private void ResolveConstraint()
    {
        if (_constraint == null)
        {
            _constraint =
                GetComponent<TetherConstraint2D>();
        }
    }

    /// <summary>
    /// True only when the boarded player is currently inside this boat's
    /// highest-priority BoardedInterior visibility zone.
    ///
    /// BoardedExteriorDeck is intentionally allowed. Being boarded is not,
    /// by itself, a reason to block sounding.
    /// </summary>
    private bool IsInsideBoatInterior()
    {
        if (boardingState == null ||
            !boardingState.IsBoarded ||
            boardingState.CurrentBoatRoot == null)
        {
            return false;
        }

        BoatVisibilityZone[] zones =
            boardingState.CurrentBoatRoot
                .GetComponentsInChildren<BoatVisibilityZone>(
                    true);

        if (zones == null ||
            zones.Length == 0)
        {
            return false;
        }

        Collider2D[] playerColliders =
            boardingState.GetComponentsInChildren<Collider2D>(
                true);

        if (playerColliders == null ||
            playerColliders.Length == 0)
        {
            return false;
        }

        BoatVisibilityZone bestZone =
            null;

        int bestPriority =
            int.MinValue;

        for (int i = 0;
             i < zones.Length;
             i++)
        {
            BoatVisibilityZone zone =
                zones[i];

            if (zone == null)
                continue;

            Collider2D zoneCollider =
                zone.GetComponent<Collider2D>();

            if (zoneCollider == null ||
                !zoneCollider.enabled ||
                !zoneCollider.isTrigger)
            {
                continue;
            }

            bool touching =
                false;

            for (int j = 0;
                 j < playerColliders.Length;
                 j++)
            {
                Collider2D playerCollider =
                    playerColliders[j];

                if (playerCollider == null ||
                    !playerCollider.enabled)
                {
                    continue;
                }

                if (!zoneCollider.IsTouching(
                        playerCollider))
                {
                    continue;
                }

                touching =
                    true;

                break;
            }

            if (!touching)
                continue;

            if (zone.Priority <=
                bestPriority)
            {
                continue;
            }

            bestPriority =
                zone.Priority;

            bestZone =
                zone;
        }

        return
            bestZone != null &&
            bestZone.Mode ==
                BoatVisibilityMode.BoardedInterior;
    }

    private void ResolveHeldItemVisualRoot()
    {
        if (heldItemVisualRoot != null)
            return;

        Transform[] transforms =
            GetComponentsInChildren<Transform>(
                true);

        for (int i = 0;
             i < transforms.Length;
             i++)
        {
            Transform candidate =
                transforms[i];

            if (candidate == null)
                continue;

            if (!string.Equals(
                    candidate.name,
                    "HeldItemVisual",
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            heldItemVisualRoot =
                candidate.gameObject;

            return;
        }
    }

    private void SetHeldVisualSuppressed(
        bool suppressed)
    {
        if (!hideHeldItemVisualWhileDeployed)
            suppressed = false;

        ResolveHeldItemVisualRoot();

        if (heldItemVisualRoot == null)
            return;

        if (suppressed)
        {
            if (_heldVisualSuppressed)
                return;

            _heldVisualOriginalForceOff.Clear();

            Renderer[] renderers =
                heldItemVisualRoot.GetComponentsInChildren<Renderer>(
                    true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null)
                    continue;

                _heldVisualOriginalForceOff[renderer] =
                    renderer.forceRenderingOff;

                renderer.forceRenderingOff =
                    true;
            }

            _heldVisualSuppressed =
                true;

            return;
        }

        if (!_heldVisualSuppressed)
            return;

        foreach (KeyValuePair<Renderer, bool> pair
                 in _heldVisualOriginalForceOff)
        {
            if (pair.Key != null)
            {
                pair.Key.forceRenderingOff =
                    pair.Value;
            }
        }

        _heldVisualOriginalForceOff.Clear();

        _heldVisualSuppressed =
            false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        freePayoutSlackMeters =
            Mathf.Max(
                0f,
                freePayoutSlackMeters);

        initialDeployedLengthMeters =
            Mathf.Max(
                0.01f,
                initialDeployedLengthMeters);

        retrieveSpeedMetersPerSecond =
            Mathf.Max(
                0.01f,
                retrieveSpeedMetersPerSecond);

        dockingDistance =
            Mathf.Max(
                0.01f,
                dockingDistance);

        autoCatchRemainingLineMeters =
            Mathf.Max(
                0.01f,
                autoCatchRemainingLineMeters);
    }
#endif
}
