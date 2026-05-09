using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CharacterMotor2D))]
public sealed class PlayerBoardingState : MonoBehaviour
{
    [Header("Layer Names")]
    [SerializeField] private string hullLayerName = "Hull";
    [SerializeField] private string hullItemLayerName = "HullItem";
    [SerializeField] private string groundLayerName = "Ground";
    [SerializeField] private string nodeGroundLayerName = "NodeGround";
    [SerializeField] private string nodeDockLayerName = "NodeDock";

    [Header("Sprite Sorting")]
    [SerializeField] private string boardedSortingLayerName = "BoatPlayer";
    [SerializeField] private bool includeInactiveChildRenderers = true;

    [Tooltip("If true, stores the player's original sprite sorting layers on Awake and restores them when unboarding.")]
    [SerializeField] private bool restoreOriginalSortingLayersOnUnboard = true;

    public bool IsBoarded { get; private set; }
    public Transform CurrentBoatRoot { get; private set; }

    private Rigidbody2D _rb;
    private CharacterMotor2D _motor;

    private int _hullLayer;
    private int _hullItemLayer;
    private int _groundLayer;
    private int _nodeGroundLayer;
    private int _nodeDockLayer;

    private LayerMask _boardedGroundMask;
    private LayerMask _unboardedGroundMask;

    private int _hullBit;
    private int _hullItemBit;
    private int _nonBoatWorldBits;

    private SpriteRenderer[] _spriteRenderers;
    private string[] _originalSortingLayerNames;
    private int[] _originalSortingOrders;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _motor = GetComponent<CharacterMotor2D>();

        CacheSpriteRenderers();

        _hullLayer = LayerMask.NameToLayer(hullLayerName);
        _hullItemLayer = LayerMask.NameToLayer(hullItemLayerName);
        _groundLayer = LayerMask.NameToLayer(groundLayerName);
        _nodeGroundLayer = LayerMask.NameToLayer(nodeGroundLayerName);
        _nodeDockLayer = LayerMask.NameToLayer(nodeDockLayerName);

        if (_hullLayer < 0) Debug.LogError($"Layer '{hullLayerName}' not found.", this);
        if (_hullItemLayer < 0) Debug.LogError($"Layer '{hullItemLayerName}' not found.", this);
        if (_groundLayer < 0) Debug.LogError($"Layer '{groundLayerName}' not found.", this);
        if (_nodeGroundLayer < 0) Debug.LogError($"Layer '{nodeGroundLayerName}' not found.", this);
        if (_nodeDockLayer < 0) Debug.LogError($"Layer '{nodeDockLayerName}' not found.", this);

        _hullBit = LayerBitOrZero(_hullLayer);
        _hullItemBit = LayerBitOrZero(_hullItemLayer);

        _nonBoatWorldBits =
            LayerBitOrZero(_groundLayer) |
            LayerBitOrZero(_nodeGroundLayer) |
            LayerBitOrZero(_nodeDockLayer);

        _boardedGroundMask = _hullBit;
        _unboardedGroundMask = _nonBoatWorldBits;

        ApplyMask();
        ApplySpriteSorting();
    }

    public void Board(Transform boatRoot)
    {
        IsBoarded = true;
        CurrentBoatRoot = boatRoot;

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

    private void ApplyMask()
    {
        int mask = _rb.excludeLayers;

        // Player should never collide with loose boat-owned visual/context items.
        // HullItem is for visibility/context, not physical blocking.
        mask |= _hullItemBit;

        if (IsBoarded)
        {
            // Boarded player collides with boat hull...
            mask &= ~_hullBit;

            // ...but ignores non-boat world geometry like docks and node ground.
            mask |= _nonBoatWorldBits;

            _motor.groundMask = _boardedGroundMask;
        }
        else
        {
            // Unboarded player ignores boat hull...
            mask |= _hullBit;

            // ...but collides with normal world ground/docks.
            mask &= ~_nonBoatWorldBits;

            _motor.groundMask = _unboardedGroundMask;
        }

        _rb.excludeLayers = mask;
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