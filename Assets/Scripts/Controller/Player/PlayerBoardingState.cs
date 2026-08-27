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

    [Header("Sprite Sorting")]
    [SerializeField] private string boardedSortingLayerName = "BoatPlayer";
    [SerializeField] private bool includeInactiveChildRenderers = true;

    [Tooltip("If true, stores the player's original sprite sorting layers on Awake and restores them when unboarding.")]
    [SerializeField] private bool restoreOriginalSortingLayersOnUnboard = true;

    [Header("Debug")]
    [SerializeField] private bool logMaskChanges = false;

    public bool IsBoarded { get; private set; }
    public Transform CurrentBoatRoot { get; private set; }

    // While boarded, Steve contributes his Rigidbody mass at his actual world
    // center of mass to the Boat's explicit mass/COM model. This preserves boat
    // listing/sink response after physical Hull reaction impulses are removed.
    public float MassContribution =>
        IsBoarded && _rb != null
            ? Mathf.Max(0f, _rb.mass)
            : 0f;

    public Vector2 WorldCenterOfMass =>
        _rb != null
            ? _rb.worldCenterOfMass
            : (Vector2)transform.position;

    private Rigidbody2D _rb;
    private CharacterMotor2D _motor;
    private Boat _massContributionBoat;

    private int _hullLayer;
    private int _boatItemLayer;
    private int _hatchLedgeLayer;
    private int _groundLayer;
    private int _worldLedgeLayer;
    private int _ghostCollisionLayer;

    private int _hullBit;
    private int _boatItemBit;
    private int _hatchLedgeBit;
    private int _groundBit;
    private int _worldLedgeBit;
    private int _ghostCollisionBit;

    private int _nonBoatWorldBits;

    private LayerMask _boardedGroundMask;
    private LayerMask _unboardedGroundMask;

    private SpriteRenderer[] _spriteRenderers;
    private string[] _originalSortingLayerNames;
    private int[] _originalSortingOrders;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _motor = GetComponent<CharacterMotor2D>();

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

        ClearGhostCollisionPairs();
        UnregisterMassContribution();
    }

    public void Board(Transform boatRoot)
    {
        // Defensive against a direct boat-to-boat reassignment.
        UnregisterMassContribution();

        IsBoarded = true;
        CurrentBoatRoot = boatRoot;

        RegisterMassContributionForCurrentBoat();
        ApplyMask();
        ApplySpriteSorting();

        if (CurrentBoatRoot != null &&
            CurrentBoatRoot.TryGetComponent(out BoatVisualStateController visuals))
        {
            visuals.RefreshZonesForPlayer(this);
        }
    }

    public void Unboard()
    {
        Transform oldBoatRoot = CurrentBoatRoot;

        UnregisterMassContribution();

        IsBoarded = false;
        CurrentBoatRoot = null;

        ApplyMask();
        ApplySpriteSorting();

        if (oldBoatRoot != null &&
            oldBoatRoot.TryGetComponent(out BoatVisualStateController visuals))
        {
            visuals.ForceRefreshForPlayer(this);
        }
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

        _hullBit = LayerBitOrZero(_hullLayer);
        _boatItemBit = LayerBitOrZero(_boatItemLayer);
        _hatchLedgeBit = LayerBitOrZero(_hatchLedgeLayer);
        _groundBit = LayerBitOrZero(_groundLayer);
        _worldLedgeBit = LayerBitOrZero(_worldLedgeLayer);
        _ghostCollisionBit = LayerBitOrZero(_ghostCollisionLayer);
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

        if (IsBoarded)
        {
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

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null)
                continue;

            _originalSortingLayerNames[i] = sr.sortingLayerName;
            _originalSortingOrders[i] = sr.sortingOrder;
        }
    }

    private void ApplySpriteSorting()
    {
        if (_spriteRenderers == null || _spriteRenderers.Length == 0)
            CacheSpriteRenderers();

        if (IsBoarded)
        {
            ApplyBoardedSortingLayer();
        }
        else if (restoreOriginalSortingLayersOnUnboard)
        {
            RestoreOriginalSortingLayers();
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

    private static int LayerBitOrZero(int layer)
    {
        return layer >= 0 ? 1 << layer : 0;
    }
}