using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CharacterMotor2D))]
public sealed class PlayerBoardingState : MonoBehaviour, IMassContribution
{
    [Header("Layer Names")]
    [SerializeField] private string hullLayerName = "Hull";
    [SerializeField] private string boatItemLayerName = "BoatItem";
    [SerializeField] private string hatchLedgeLayerName = "HatchLedge";
    [SerializeField] private string groundLayerName = "Ground";
    [SerializeField] private string worldLedgeLayerName = "WorldLedge";
    [SerializeField] private string ghostCollisionLayerName = "GhostCollision";
    [SerializeField] private string bellInteriorLayerName = "BellInterior";
    [SerializeField] private string bellLedgeLayerName = "BellLedge";
    [SerializeField] private string tetherPayloadLayerName = "TetherPayload";

    [Header("Sprite Sorting")]
    [SerializeField] private string boardedSortingLayerName = "BoatPlayer";
    [SerializeField] private bool includeInactiveChildRenderers = true;

    [Tooltip("If true, stores the player's original sprite sorting layers on Awake and restores them when unboarding.")]
    [SerializeField] private bool restoreOriginalSortingLayersOnUnboard = true;

    [Header("Debug")]
    [SerializeField] private bool logMaskChanges = false;

    public bool IsBoarded { get; private set; }
    public Transform CurrentBoatRoot { get; private set; }

    public bool HasCollisionContextOverride =>
        _collisionOverrideActive &&
        _collisionOverrideOwner != null;

    public Object CollisionContextOverrideOwner =>
        HasCollisionContextOverride
            ? _collisionOverrideOwner
            : null;

    // Boarding context and physical support are deliberately separate.
    //
    // The boarded volume may extend above/around the boat so Steve remains in
    // boat gameplay context while jumping. That does NOT mean the boat should
    // keep carrying Steve's static mass/COM while his feet are in the air.
    //
    // V1 support rule: grounded while boarded = supported by the boat.
    // This can later grow to explicitly include supported ladder/seat states if
    // either proves necessary.
    public bool IsPhysicallySupportedByBoat =>
        IsBoarded &&
        _motor != null &&
        _motor.IsGrounded;

    // While physically supported, Steve contributes his truthful physical load:
    // body Rigidbody mass + carried inventory/equipment mass.
    //
    // IMPORTANT: carried mass is NOT written into Rigidbody2D.mass. Player
    // locomotion keeps its stable body physics; PlayerLoadState is the gameplay/
    // vehicle bridge for carried physical weight.
    //
    // TODO BOAT IMPULSE PASS:
    // When Steve jumps from a boat, apply the corresponding reaction impulse back
    // into the authoritative Boat. Keep that with the future generalized boat
    // impulse/contact pass rather than mixing transient impulses into this static
    // supported-mass/COM contribution.
    public float MassContribution =>
        IsPhysicallySupportedByBoat
            ? (_loadState != null
                ? Mathf.Max(0f, _loadState.TotalPhysicalMass)
                : (_rb != null
                    ? Mathf.Max(0f, _rb.mass)
                    : 0f))
            : 0f;

    public Vector2 WorldCenterOfMass =>
        _rb != null
            ? _rb.worldCenterOfMass
            : (Vector2)transform.position;

    private Rigidbody2D _rb;
    private CharacterMotor2D _motor;
    private PlayerLoadState _loadState;
    private Boat _massContributionBoat;

    private int _hullLayer;
    private int _boatItemLayer;
    private int _hatchLedgeLayer;
    private int _groundLayer;
    private int _worldLedgeLayer;
    private int _ghostCollisionLayer;
    private int _bellInteriorLayer;
    private int _bellLedgeLayer;
    private int _tetherPayloadLayer;

    private int _hullBit;
    private int _boatItemBit;
    private int _hatchLedgeBit;
    private int _groundBit;
    private int _worldLedgeBit;
    private int _ghostCollisionBit;
    private int _bellInteriorBit;
    private int _bellLedgeBit;
    private int _tetherPayloadBit;
    private int _bellCollisionBits;

    private int _nonBoatWorldBits;

    // Generic temporary per-player collision-context override.
    // The diving bell uses this now; future vehicle/interior contexts can reuse it.
    private Object _collisionOverrideOwner;
    private bool _collisionOverrideActive;
    private LayerMask _collisionOverrideAllowedLayers;
    private LayerMask _collisionOverrideGroundMask;
    private int _excludeLayersBeforeCollisionOverride;
    private LayerMask _groundMaskBeforeCollisionOverride;

    private LayerMask _boardedGroundMask;
    private LayerMask _unboardedGroundMask;

    private SpriteRenderer[] _spriteRenderers;
    private string[] _originalSortingLayerNames;
    private int[] _originalSortingOrders;
    private int[] _originalRelativeSortingOrders;

    // Temporary visual sorting override. This is intentionally owned by the
    // player presentation state so external systems (such as the diving bell)
    // never write SpriteRenderer sorting directly and then try to repair it later.
    private Object _presentationSortingOverrideOwner;
    private bool _presentationSortingOverrideActive;
    private int _presentationSortingOverrideLayerId;
    private int _presentationSortingOverrideBaseOrder;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _motor = GetComponent<CharacterMotor2D>();

        _loadState =
            GetComponent<PlayerLoadState>() ??
            GetComponentInChildren<PlayerLoadState>(true) ??
            GetComponentInParent<PlayerLoadState>();

        CacheSpriteRenderers();
        CacheLayers();
        BuildMasks();

        ApplyMask();
        ApplySpriteSorting();
    }

    private void OnEnable()
    {
        GhostCollisionProxy.ActiveProxySetChanged +=
            HandleGhostProxySetChanged;

        if (_loadState == null)
        {
            _loadState =
                GetComponent<PlayerLoadState>() ??
                GetComponentInChildren<PlayerLoadState>(true) ??
                GetComponentInParent<PlayerLoadState>();
        }

        if (_loadState != null)
            _loadState.Changed += HandlePlayerLoadChanged;

        if (IsBoarded)
        {
            RegisterMassContributionForCurrentBoat();
            ApplyMask();
        }
    }

    private void OnDisable()
    {
        GhostCollisionProxy.ActiveProxySetChanged -=
            HandleGhostProxySetChanged;

        if (_loadState != null)
            _loadState.Changed -= HandlePlayerLoadChanged;

        ClearGhostCollisionPairs();
        UnregisterMassContribution();

        if (_collisionOverrideActive)
        {
            if (_rb != null)
                _rb.excludeLayers = _excludeLayersBeforeCollisionOverride;

            if (_motor != null)
                _motor.groundMask = _groundMaskBeforeCollisionOverride;

            _collisionOverrideOwner = null;
            _collisionOverrideActive = false;
        }

        _presentationSortingOverrideOwner = null;
        _presentationSortingOverrideActive = false;
    }

    public void Board(Transform boatRoot)
    {
        ApplyBoardState(
            boatRoot,
            refreshVisualState: true);
    }

    /// <summary>
    /// Applies authoritative boarded state without immediately resolving boat
    /// visibility zones.
    ///
    /// Use this only for transactional placement flows that must move the player
    /// to a final authored position before zone-driven presentation is allowed to
    /// evaluate. The caller is responsible for refreshing presentation after the
    /// next 2D physics update.
    /// </summary>
    public void BoardDeferredPresentation(
        Transform boatRoot)
    {
        ApplyBoardState(
            boatRoot,
            refreshVisualState: false);
    }

    private void ApplyBoardState(
        Transform boatRoot,
        bool refreshVisualState)
    {
        // Defensive against a direct boat-to-boat reassignment.
        UnregisterMassContribution();

        IsBoarded = true;
        CurrentBoatRoot = boatRoot;

        RegisterMassContributionForCurrentBoat();
        ApplyMask();
        ApplySpriteSorting();

        if (refreshVisualState)
            RefreshCurrentBoatVisualState();
    }

    public void Unboard()
    {
        Transform oldBoatRoot = CurrentBoatRoot;

        UnregisterMassContribution();

        IsBoarded = false;
        CurrentBoatRoot = null;

        ApplyMask();
        ApplySpriteSorting();

        BoatVisualStateController visuals =
            ResolveBoatVisualController(
                oldBoatRoot);

        if (visuals != null)
            visuals.ForceRefreshForPlayer(this);
    }

    /// <summary>
    /// Temporarily replaces this player's physical collision context.
    ///
    /// allowedCollisionLayers means exactly what it says: while the override is
    /// active, the Rigidbody2D excludes every physics layer EXCEPT these layers.
    /// groundMaskOverride becomes CharacterMotor2D.groundMask.
    ///
    /// Only the owner that acquired the override may release it.
    /// </summary>
    public bool TrySetCollisionContextOverride(
        Object owner,
        LayerMask allowedCollisionLayers,
        LayerMask groundMaskOverride,
        out string reason)
    {
        reason = null;

        if (owner == null)
        {
            reason = "Collision override owner is null.";
            return false;
        }

        if (_rb == null || _motor == null)
        {
            reason = "Player collision components are unavailable.";
            return false;
        }

        if (_collisionOverrideActive &&
            _collisionOverrideOwner != null &&
            !ReferenceEquals(_collisionOverrideOwner, owner))
        {
            reason =
                $"Player collision context is already owned by '{_collisionOverrideOwner.name}'.";
            return false;
        }

        if (!_collisionOverrideActive)
        {
            _excludeLayersBeforeCollisionOverride =
                _rb.excludeLayers;

            _groundMaskBeforeCollisionOverride =
                _motor.groundMask;
        }

        _collisionOverrideOwner =
            owner;

        _collisionOverrideActive =
            true;

        _collisionOverrideAllowedLayers =
            allowedCollisionLayers;

        _collisionOverrideGroundMask =
            groundMaskOverride;

        ApplyMask();
        return true;
    }

    public bool ClearCollisionContextOverride(
        Object owner)
    {
        if (!_collisionOverrideActive)
            return true;

        if (owner == null ||
            _collisionOverrideOwner == null ||
            !ReferenceEquals(_collisionOverrideOwner, owner))
        {
            return false;
        }

        _collisionOverrideOwner = null;
        _collisionOverrideActive = false;

        if (_rb != null)
            _rb.excludeLayers = _excludeLayersBeforeCollisionOverride;

        if (_motor != null)
            _motor.groundMask = _groundMaskBeforeCollisionOverride;

        // Re-evaluate the player's current boat/world state instead of merely
        // restoring stale bits from the moment the override began.
        ApplyMask();
        return true;
    }

    /// <summary>
    /// Temporarily places this player's sprite renderers into another sorting context.
    /// The override is owned, so only the system that acquired it may update/release it.
    ///
    /// This is presentation-only state. It does not change boarding, collision, or authority.
    /// </summary>
    public bool TrySetPresentationSortingOverride(
        Object owner,
        int sortingLayerId,
        int baseSortingOrder)
    {
        if (owner == null)
            return false;

        if (_presentationSortingOverrideActive &&
            _presentationSortingOverrideOwner != null &&
            !ReferenceEquals(_presentationSortingOverrideOwner, owner))
        {
            return false;
        }

        if (_spriteRenderers == null ||
            _spriteRenderers.Length == 0 ||
            _originalRelativeSortingOrders == null ||
            _originalRelativeSortingOrders.Length != _spriteRenderers.Length)
        {
            CacheSpriteRenderers();
        }

        _presentationSortingOverrideOwner = owner;
        _presentationSortingOverrideActive = true;
        _presentationSortingOverrideLayerId = sortingLayerId;
        _presentationSortingOverrideBaseOrder = baseSortingOrder;

        ReapplyCurrentPresentation();
        return true;
    }

    public bool ClearPresentationSortingOverride(
        Object owner)
    {
        if (!_presentationSortingOverrideActive)
            return true;

        if (owner == null ||
            _presentationSortingOverrideOwner == null ||
            !ReferenceEquals(_presentationSortingOverrideOwner, owner))
        {
            return false;
        }

        _presentationSortingOverrideOwner = null;
        _presentationSortingOverrideActive = false;

        ReapplyCurrentPresentation();
        return true;
    }

    public bool HasPresentationSortingOverride =>
        _presentationSortingOverrideActive &&
        _presentationSortingOverrideOwner != null;

    public Object PresentationSortingOverrideOwner =>
        HasPresentationSortingOverride
            ? _presentationSortingOverrideOwner
            : null;

    /// <summary>
    /// Rebuild sprite sorting from the authoritative current boarding state, then
    /// apply any active temporary presentation override last.
    /// </summary>
    public void ReapplyCurrentPresentation()
    {
        if (_spriteRenderers == null ||
            _spriteRenderers.Length == 0)
        {
            CacheSpriteRenderers();
        }

        RestoreOriginalSortingLayers();

        if (IsBoarded)
            ApplyBoardedSortingLayer();

        if (HasPresentationSortingOverride)
            ApplyPresentationSortingOverride();
    }

    /// <summary>
    /// Re-scan BoatVisibilityZone overlaps for the currently boarded boat.
    /// </summary>
    public void RefreshCurrentBoatVisualState()
    {
        if (!IsBoarded ||
            CurrentBoatRoot == null)
        {
            return;
        }

        BoatVisualStateController visuals =
            ResolveBoatVisualController(
                CurrentBoatRoot);

        if (visuals != null)
            visuals.RefreshZonesForPlayer(this);
    }

    [ContextMenu("Reapply Collision Mask")]
    public void ReapplyCollisionMask()
    {
        CacheLayers();
        BuildMasks();
        ApplyMask();
    }

    private void CacheLayers()
    {
        _hullLayer = LayerMask.NameToLayer(hullLayerName);
        _boatItemLayer = string.IsNullOrWhiteSpace(boatItemLayerName)
            ? -1
            : LayerMask.NameToLayer(boatItemLayerName);
        _hatchLedgeLayer = LayerMask.NameToLayer(hatchLedgeLayerName);
        _groundLayer = LayerMask.NameToLayer(groundLayerName);
        _worldLedgeLayer = LayerMask.NameToLayer(worldLedgeLayerName);
        _ghostCollisionLayer = LayerMask.NameToLayer(ghostCollisionLayerName);
        _bellInteriorLayer = LayerMask.NameToLayer(bellInteriorLayerName);
        _bellLedgeLayer = LayerMask.NameToLayer(bellLedgeLayerName);
        _tetherPayloadLayer = string.IsNullOrWhiteSpace(tetherPayloadLayerName)
            ? -1
            : LayerMask.NameToLayer(tetherPayloadLayerName);

        if (_hullLayer < 0)
            Debug.LogError($"Layer '{hullLayerName}' not found.", this);

        if (!string.IsNullOrWhiteSpace(boatItemLayerName) && _boatItemLayer < 0)
        {
            Debug.LogWarning(
                $"[PlayerBoardingState:{name}] Optional layer '{boatItemLayerName}' not found. " +
                "BoatItem exclusion will be skipped.",
                this);
        }

        if (_hatchLedgeLayer < 0)
            Debug.LogError($"Layer '{hatchLedgeLayerName}' not found.", this);

        if (_groundLayer < 0)
            Debug.LogError($"Layer '{groundLayerName}' not found.", this);

        if (_worldLedgeLayer < 0)
            Debug.LogError($"Layer '{worldLedgeLayerName}' not found.", this);

        if (_ghostCollisionLayer < 0)
            Debug.LogError($"Layer '{ghostCollisionLayerName}' not found.", this);

        if (_bellInteriorLayer < 0)
            Debug.LogError($"Layer '{bellInteriorLayerName}' not found.", this);

        if (_bellLedgeLayer < 0)
            Debug.LogError($"Layer '{bellLedgeLayerName}' not found.", this);

        if (!string.IsNullOrWhiteSpace(tetherPayloadLayerName) && _tetherPayloadLayer < 0)
        {
            Debug.LogWarning(
                $"[PlayerBoardingState:{name}] Optional layer '{tetherPayloadLayerName}' not found. " +
                "Boarded-player tether-payload exclusion will be skipped.",
                this);
        }

        _hullBit = LayerBitOrZero(_hullLayer);
        _boatItemBit = LayerBitOrZero(_boatItemLayer);
        _hatchLedgeBit = LayerBitOrZero(_hatchLedgeLayer);
        _groundBit = LayerBitOrZero(_groundLayer);
        _worldLedgeBit = LayerBitOrZero(_worldLedgeLayer);
        _ghostCollisionBit = LayerBitOrZero(_ghostCollisionLayer);
        _bellInteriorBit = LayerBitOrZero(_bellInteriorLayer);
        _bellLedgeBit = LayerBitOrZero(_bellLedgeLayer);
        _tetherPayloadBit = LayerBitOrZero(_tetherPayloadLayer);

        _bellCollisionBits =
            _bellInteriorBit |
            _bellLedgeBit;
    }

    private void BuildMasks()
    {
        _nonBoatWorldBits =
            _groundBit |
            _worldLedgeBit;

        _boardedGroundMask =
            _hullBit |
            _hatchLedgeBit;

        _unboardedGroundMask =
            _groundBit |
            _worldLedgeBit;
    }

    private void ApplyMask()
    {
        if (_rb == null || _motor == null)
            return;

        // UnityEngine.Object becomes == null after its owner is destroyed.
        // If that happens, fail safe back to the ordinary player collision state.
        if (_collisionOverrideActive &&
            _collisionOverrideOwner == null)
        {
            _collisionOverrideActive = false;
            _rb.excludeLayers = _excludeLayersBeforeCollisionOverride;
            _motor.groundMask = _groundMaskBeforeCollisionOverride;
        }

        if (_collisionOverrideActive &&
            _collisionOverrideOwner != null)
        {
            // Collision matrix remains globally authoritative. This per-body mask
            // narrows the player's physical world to the requested context only.
            _rb.excludeLayers =
                ~_collisionOverrideAllowedLayers.value;

            _motor.groundMask =
                _collisionOverrideGroundMask;

            // Do NOT tear down the player's existing GhostCollisionProxy pair routing here.
            // The per-body exclude mask already blocks GhostCollision while the bell override
            // is active. Preserving the pair configuration means ordinary boarded collision
            // resumes cleanly when the override is released.
            if (logMaskChanges)
            {
                Debug.Log(
                    $"[PlayerBoardingState:{name}] ApplyMask COLLISION OVERRIDE " +
                    $"owner={_collisionOverrideOwner.name} " +
                    $"allowed={_collisionOverrideAllowedLayers.value} " +
                    $"excludeLayers={_rb.excludeLayers.value} " +
                    $"groundMask={_motor.groundMask.value}",
                    this);
            }

            return;
        }

        GhostCollisionProxy ownerGhost =
            ResolveCurrentGhostProxy();

        bool useGhost =
            IsBoarded &&
            ownerGhost != null &&
            ownerGhost.IsBuilt;

        int mask =
            _rb.excludeLayers;

        // BoatItem is for loose boat-owned items/cargo, not player body blocking.
        // Optional because early layer cleanup may not have populated/created it yet.
        mask |= _boatItemBit;

        // Bell collision geometry is opt-in through a per-player collision context.
        // Ordinary players outside bells should pass through it completely.
        mask |= _bellCollisionBits;

        if (IsBoarded)
        {
            // A live deployed tether payload is physically independent of the boat.
            // Boarded players must not be able to shove it, ride it, or inject solver
            // impulses into its tether. Bell occupants still collide with the bell
            // through the dedicated GhostCollision override instead of the real payload.
            mask |= _tetherPayloadBit;

            // Boarded player ignores world ground and world ledges/docks.
            mask |= _nonBoatWorldBits;

            // Existing hatch-ledge behavior remains available. If a particular
            // hatch collider is also one of Ghost Boat's source colliders, the
            // per-collider ghost handoff below suppresses the real copy.
            mask &= ~_hatchLedgeBit;

            if (useGhost)
            {
                // One-way interior physics:
                // player ignores real Hull, collides with owning Ghost.
                mask |= _hullBit;
                mask &= ~_ghostCollisionBit;

                _motor.groundMask =
                    _ghostCollisionBit |
                    _hatchLedgeBit;
            }
            else
            {
                // Safe fallback: preserve the pre-Ghost boarding behavior.
                mask &= ~_hullBit;
                mask |= _ghostCollisionBit;

                _motor.groundMask =
                    _boardedGroundMask;
            }
        }
        else
        {
            // Once unboarded, world/swimming players may physically collide with
            // deployed tether payloads again.
            mask &= ~_tetherPayloadBit;

            // Unboarded player ignores boat hull, hatch ledges, and ALL ghosts.
            mask |= _hullBit;
            mask |= _hatchLedgeBit;
            mask |= _ghostCollisionBit;

            // Unboarded player collides with world ground and world one-way ledges.
            mask &= ~_nonBoatWorldBits;

            _motor.groundMask =
                _unboardedGroundMask;
        }

        _rb.excludeLayers =
            mask;

        ApplyGhostCollisionPairs(
            useGhost
                ? ownerGhost
                : null);

        if (logMaskChanges)
        {
            Debug.Log(
                $"[PlayerBoardingState:{name}] ApplyMask " +
                $"IsBoarded={IsBoarded} useGhost={useGhost} " +
                $"ghost={(ownerGhost != null ? ownerGhost.name : "NONE")} " +
                $"excludeLayers={_rb.excludeLayers.value} " +
                $"groundMask={_motor.groundMask.value}",
                this);
        }
    }

    private GhostCollisionProxy ResolveCurrentGhostProxy()
    {
        if (!IsBoarded ||
            CurrentBoatRoot == null)
        {
            return null;
        }

        return CurrentBoatRoot
            .GetComponent<GhostCollisionProxy>();
    }

    private void ApplyGhostCollisionPairs(
        GhostCollisionProxy allowedProxy)
    {
        if (_rb == null)
            return;

        Collider2D[] all =
            GetComponentsInChildren<Collider2D>(
                true);

        if (all == null ||
            all.Length == 0)
        {
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

        GhostCollisionProxy.ConfigureExclusiveCollisions(
            solids,
            allowedProxy);
    }

    private void ClearGhostCollisionPairs()
    {
        ApplyGhostCollisionPairs(
            null);
    }

    private void HandleGhostProxySetChanged()
    {
        ApplyMask();
    }

    private void HandlePlayerLoadChanged(
        PlayerLoadState loadState)
    {
        if (!IsBoarded ||
            _massContributionBoat == null)
        {
            return;
        }

        _massContributionBoat.RecomputeMassAndCOM();
    }

    private void RegisterMassContributionForCurrentBoat()
    {
        if (!IsBoarded ||
            CurrentBoatRoot == null)
        {
            return;
        }

        if (!CurrentBoatRoot.TryGetComponent(
                out Boat boat) ||
            boat == null)
        {
            return;
        }

        if (ReferenceEquals(
                _massContributionBoat,
                boat))
        {
            return;
        }

        UnregisterMassContribution();

        _massContributionBoat =
            boat;

        _massContributionBoat.RegisterMassContribution(
            this);

        _massContributionBoat.RecomputeMassAndCOM();
    }

    private void UnregisterMassContribution()
    {
        if (_massContributionBoat == null)
            return;

        Boat oldBoat =
            _massContributionBoat;

        _massContributionBoat =
            null;

        oldBoat.UnregisterMassContribution(
            this);

        oldBoat.RecomputeMassAndCOM();
    }

    private void CacheSpriteRenderers()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactiveChildRenderers);

        _originalSortingLayerNames = new string[_spriteRenderers.Length];
        _originalSortingOrders = new int[_spriteRenderers.Length];
        _originalRelativeSortingOrders = new int[_spriteRenderers.Length];

        int minOrder = int.MaxValue;

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null)
                continue;

            _originalSortingLayerNames[i] = sr.sortingLayerName;
            _originalSortingOrders[i] = sr.sortingOrder;
            minOrder = Mathf.Min(minOrder, sr.sortingOrder);
        }

        if (minOrder == int.MaxValue)
            minOrder = 0;

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null)
                continue;

            _originalRelativeSortingOrders[i] =
                _originalSortingOrders[i] - minOrder;
        }
    }

    private void ApplySpriteSorting()
    {
        if (_spriteRenderers == null ||
            _spriteRenderers.Length == 0)
        {
            CacheSpriteRenderers();
        }

        if (restoreOriginalSortingLayersOnUnboard)
            RestoreOriginalSortingLayers();

        if (IsBoarded)
            ApplyBoardedSortingLayer();

        if (HasPresentationSortingOverride)
            ApplyPresentationSortingOverride();
    }

    private void ApplyPresentationSortingOverride()
    {
        if (!HasPresentationSortingOverride ||
            _spriteRenderers == null ||
            _originalRelativeSortingOrders == null)
        {
            return;
        }

        int count = Mathf.Min(
            _spriteRenderers.Length,
            _originalRelativeSortingOrders.Length);

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null)
                continue;

            sr.sortingLayerID = _presentationSortingOverrideLayerId;
            sr.sortingOrder =
                _presentationSortingOverrideBaseOrder +
                _originalRelativeSortingOrders[i];
        }
    }

    private void ApplyBoardedSortingLayer()
    {
        if (string.IsNullOrWhiteSpace(boardedSortingLayerName))
            return;

        int layerId = SortingLayer.NameToID(boardedSortingLayerName);
        if (layerId == 0 && boardedSortingLayerName != "Default")
        {
            Debug.LogWarning(
                $"[PlayerBoardingState] Sorting layer '{boardedSortingLayerName}' may not exist. " +
                $"Unity returned layer id 0. Check Project Settings > Tags and Layers > Sorting Layers.",
                this);
        }

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null)
                continue;

            sr.sortingLayerName = boardedSortingLayerName;
        }
    }

    private void RestoreOriginalSortingLayers()
    {
        if (_spriteRenderers == null)
            return;

        int count = Mathf.Min(_spriteRenderers.Length, _originalSortingLayerNames.Length);

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null)
                continue;

            if (!string.IsNullOrWhiteSpace(_originalSortingLayerNames[i]))
                sr.sortingLayerName = _originalSortingLayerNames[i];

            sr.sortingOrder = _originalSortingOrders[i];
        }
    }

    private BoatVisualStateController ResolveBoatVisualController(
        Transform boatRoot)
    {
        if (boatRoot == null)
            return null;

        BoatVisualStateController direct =
            boatRoot.GetComponent<BoatVisualStateController>();

        if (direct != null)
            return direct;

        return
            boatRoot.GetComponentInChildren<BoatVisualStateController>(
                true);
    }

    private static int LayerBitOrZero(int layer)
    {
        return layer >= 0 ? 1 << layer : 0;
    }
}