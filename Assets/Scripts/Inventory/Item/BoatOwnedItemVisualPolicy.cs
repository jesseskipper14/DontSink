using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatOwnedItemVisualPolicy : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoatOwnedItem ownedItem;

    [Header("Renderer Search")]
    [SerializeField] private bool includeInactiveRenderers = true;

    [Header("Boat-Owned Sorting Layer")]
    [SerializeField] private string boatOwnedSortingLayer = "BoatItem";
    [SerializeField] private int boatOwnedSortingOrder = 0;

    [Header("Boat-Owned GameObject Layer")]
    [Tooltip("GameObject layer used for boat-owned loose items so BoatVisualStateController/camera masks can hide/show them with boat visuals.")]
    [SerializeField] private string boatOwnedGameObjectLayer = "Hull";

    [Tooltip("If true, applies the GameObject layer to this object and all children.")]
    [SerializeField] private bool applyGameObjectLayerToChildren = true;

    [Header("Unowned Restore")]
    [Tooltip("If true, restores each renderer's original sorting layer/order when ownership is cleared.")]
    [SerializeField] private bool restoreOriginalSortingOnUnowned = true;

    [Tooltip("If true, restores original GameObject layers when ownership is cleared.")]
    [SerializeField] private bool restoreOriginalGameObjectLayerOnUnowned = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private SpriteRenderer[] _spriteRenderers;
    private string[] _originalSortingLayers;
    private int[] _originalSortingOrders;

    private Transform[] _layerTargets;
    private int[] _originalGameObjectLayers;

    private bool _cached;

    private void Awake()
    {
        Cache();
    }

    private void OnEnable()
    {
        Cache();

        if (ownedItem != null)
            ownedItem.OwnershipChanged += OnOwnershipChanged;

        ApplyNow();
    }

    private void OnDisable()
    {
        if (ownedItem != null)
            ownedItem.OwnershipChanged -= OnOwnershipChanged;
    }

    private void OnOwnershipChanged(BoatOwnedItem item)
    {
        ApplyNow();
    }

    public void ApplyNow()
    {
        Cache();

        bool boatOwned = ownedItem != null && ownedItem.IsOwnedByBoat;

        if (boatOwned)
        {
            ApplyBoatOwnedSorting();
            ApplyBoatOwnedGameObjectLayer();
            return;
        }

        if (restoreOriginalSortingOnUnowned)
            RestoreOriginalSorting();

        if (restoreOriginalGameObjectLayerOnUnowned)
            RestoreOriginalGameObjectLayers();
    }

    private void ApplyBoatOwnedSorting()
    {
        if (string.IsNullOrWhiteSpace(boatOwnedSortingLayer))
            return;

        int layerId = SortingLayer.NameToID(boatOwnedSortingLayer);
        if (layerId == 0 && boatOwnedSortingLayer != "Default")
        {
            LogWarning(
                $"Sorting layer '{boatOwnedSortingLayer}' may not exist. " +
                "Unity returned layer id 0. Check Project Settings > Tags and Layers > Sorting Layers.");
        }

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null)
                continue;

            sr.sortingLayerName = boatOwnedSortingLayer;
            sr.sortingOrder = boatOwnedSortingOrder;
        }

        Log($"Applied boat-owned sorting layer '{boatOwnedSortingLayer}' order={boatOwnedSortingOrder}.");
    }

    private void ApplyBoatOwnedGameObjectLayer()
    {
        if (string.IsNullOrWhiteSpace(boatOwnedGameObjectLayer))
            return;

        int layer = LayerMask.NameToLayer(boatOwnedGameObjectLayer);
        if (layer < 0)
        {
            LogWarning($"GameObject layer '{boatOwnedGameObjectLayer}' does not exist.");
            return;
        }

        if (_layerTargets == null)
            return;

        for (int i = 0; i < _layerTargets.Length; i++)
        {
            Transform t = _layerTargets[i];
            if (t == null)
                continue;

            t.gameObject.layer = layer;
        }

        Log($"Applied boat-owned GameObject layer '{boatOwnedGameObjectLayer}' to {_layerTargets.Length} object(s).");
    }

    private void RestoreOriginalSorting()
    {
        if (_spriteRenderers == null || _originalSortingLayers == null || _originalSortingOrders == null)
            return;

        int count = Mathf.Min(
            _spriteRenderers.Length,
            Mathf.Min(_originalSortingLayers.Length, _originalSortingOrders.Length));

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null)
                continue;

            if (!string.IsNullOrWhiteSpace(_originalSortingLayers[i]))
                sr.sortingLayerName = _originalSortingLayers[i];

            sr.sortingOrder = _originalSortingOrders[i];
        }

        Log("Restored original sorting.");
    }

    private void RestoreOriginalGameObjectLayers()
    {
        if (_layerTargets == null || _originalGameObjectLayers == null)
            return;

        int count = Mathf.Min(_layerTargets.Length, _originalGameObjectLayers.Length);

        for (int i = 0; i < count; i++)
        {
            Transform t = _layerTargets[i];
            if (t == null)
                continue;

            t.gameObject.layer = _originalGameObjectLayers[i];
        }

        Log("Restored original GameObject layers.");
    }

    private void Cache()
    {
        if (_cached)
            return;

        if (ownedItem == null)
            ownedItem = GetComponent<BoatOwnedItem>();

        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactiveRenderers);

        _originalSortingLayers = new string[_spriteRenderers.Length];
        _originalSortingOrders = new int[_spriteRenderers.Length];

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null)
                continue;

            _originalSortingLayers[i] = sr.sortingLayerName;
            _originalSortingOrders[i] = sr.sortingOrder;
        }

        _layerTargets = applyGameObjectLayerToChildren
            ? GetComponentsInChildren<Transform>(includeInactiveRenderers)
            : new[] { transform };

        _originalGameObjectLayers = new int[_layerTargets.Length];

        for (int i = 0; i < _layerTargets.Length; i++)
        {
            Transform t = _layerTargets[i];
            _originalGameObjectLayers[i] = t != null ? t.gameObject.layer : gameObject.layer;
        }

        _cached = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(boatOwnedSortingLayer))
            boatOwnedSortingLayer = "BoatItem";

        if (string.IsNullOrWhiteSpace(boatOwnedGameObjectLayer))
            boatOwnedGameObjectLayer = "Hull";
    }
#endif

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatOwnedItemVisualPolicy:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[BoatOwnedItemVisualPolicy:{name}] {msg}", this);
    }
}