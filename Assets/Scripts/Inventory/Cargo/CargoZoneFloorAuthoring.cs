using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CargoZoneFloorAuthoring : MonoBehaviour
{
    public enum HeightMode
    {
        Fixed,
        ToNearestCeilingLater
    }

    public enum FloorBoundsSource
    {
        FloorCollider,
        RendererBounds
    }

    [Header("Refs")]
    [SerializeField] private BoatSecureZone secureZone;

    [Tooltip("Footprint/support/reference collider for the cargo floor overlay. Kept as a trigger.")]
    [SerializeField] private BoxCollider2D floorCollider;

    [Tooltip("Generated secure volume above the floor.")]
    [SerializeField] private BoxCollider2D secureZoneCollider;

    [Tooltip("Optional renderer used to infer the cargo floor's visual width. Usually the root SpriteRenderer.")]
    [SerializeField] private Renderer floorRenderer;

    [Header("Floor Bounds")]
    [SerializeField] private FloorBoundsSource boundsSource = FloorBoundsSource.RendererBounds;

    [Tooltip("Auto-find a floor renderer if Floor Renderer is empty.")]
    [SerializeField] private bool autoFindFloorRenderer = true;

    [Tooltip("When using Renderer Bounds, also resize the floor collider to match the visual width.")]
    [SerializeField] private bool autoFitFloorColliderToSourceWidth = true;

    [Tooltip("Height of the floor reference collider when auto-fitting from renderer width.")]
    [Min(0.01f)]
    [SerializeField] private float floorColliderHeight = 0.12f;

    [Tooltip("Extra vertical offset applied when auto-fitting the floor collider. Usually 0.")]
    [SerializeField] private float floorColliderTopYOffset = 0f;

    [Header("Zone Shape")]
    [SerializeField] private HeightMode heightMode = HeightMode.Fixed;

    [Tooltip("Generated secure-zone height above the floor.")]
    [Min(0.1f)]
    [SerializeField] private float fixedZoneHeight = 2.0f;

    [Tooltip("Vertical gap between the floor top and secure zone bottom.")]
    [SerializeField] private float bottomOffsetFromFloorTop = 0.02f;

    [Tooltip("Horizontal padding removed from each side of the generated secure zone.")]
    [Min(0f)]
    [SerializeField] private float horizontalPadding = 0.05f;

    [Header("Capacity / Behavior")]
    [SerializeField] private bool allowStackedCargo = false;

    [Header("Editor")]
    [SerializeField] private bool autoRefreshInEditor = true;

#if UNITY_EDITOR
    private bool _hasLastEditorSignature;
    private Vector3 _lastSourceCenter;
    private Vector3 _lastSourceSize;
    private float _lastFixedZoneHeight;
    private float _lastBottomOffset;
    private float _lastHorizontalPadding;
    private float _lastFloorColliderHeight;
    private float _lastFloorColliderTopYOffset;
    private FloorBoundsSource _lastBoundsSource;
    private bool _lastAutoFitFloorCollider;
#endif

    private bool _isRefreshing;

    private void Reset()
    {
        CacheRefs();
        ConfigureColliders();
        RefreshZone();
    }

    private void Awake()
    {
        CacheRefs();
        ConfigureColliders();
        RefreshZone();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fixedZoneHeight = Mathf.Max(0.1f, fixedZoneHeight);
        horizontalPadding = Mathf.Max(0f, horizontalPadding);
        floorColliderHeight = Mathf.Max(0.01f, floorColliderHeight);

        CacheRefs();
        ConfigureColliders();

        if (autoRefreshInEditor)
            RefreshZone();
    }

    private void Update()
    {
        if (Application.isPlaying)
            return;

        if (!autoRefreshInEditor || _isRefreshing)
            return;

        CacheRefs();

        if (NeedsEditorRefresh())
            RefreshZone();
    }
#endif

    [ContextMenu("Refresh Cargo Secure Zone")]
    public void RefreshZone()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;

        try
        {
            CacheRefs();
            ConfigureColliders();

            if (secureZone == null || floorCollider == null || secureZoneCollider == null)
                return;

            Bounds sourceBounds = ResolveSourceBounds();

            if (boundsSource == FloorBoundsSource.RendererBounds &&
                floorRenderer != null &&
                autoFitFloorColliderToSourceWidth)
            {
                AutoFitFloorColliderToSource(sourceBounds);
            }

            Bounds floorBounds = floorCollider.bounds;

            float zoneWidthWorld = Mathf.Max(
                0.1f,
                floorBounds.size.x - horizontalPadding * 2f);

            float zoneHeightWorld = Mathf.Max(
                0.1f,
                ResolveZoneHeight());

            float zoneBottomWorldY = floorBounds.max.y + bottomOffsetFromFloorTop;
            float zoneCenterWorldY = zoneBottomWorldY + zoneHeightWorld * 0.5f;

            Vector2 zoneWorldCenter = new Vector2(
                floorBounds.center.x,
                zoneCenterWorldY);

            Vector2 zoneWorldSize = new Vector2(
                zoneWidthWorld,
                zoneHeightWorld);

            SetBoxColliderWorldRect(
                secureZoneCollider,
                zoneWorldCenter,
                zoneWorldSize);

            secureZone.EditorSetStackedCargoAllowed(allowStackedCargo);

            // Temporary compatibility with current BoatSecureZone.
            // This should go away when support switches to a downward probe instead of directSupportColliders.
            secureZone.EditorSetPrimarySupportSurface(floorCollider);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                StoreEditorSignature();

                EditorUtility.SetDirty(secureZone);
                EditorUtility.SetDirty(floorCollider);
                EditorUtility.SetDirty(secureZoneCollider);
                EditorUtility.SetDirty(this);
            }
#endif
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private float ResolveZoneHeight()
    {
        switch (heightMode)
        {
            case HeightMode.Fixed:
                return fixedZoneHeight;

            case HeightMode.ToNearestCeilingLater:
                // Placeholder until ceiling/deck detection is formalized.
                return fixedZoneHeight;

            default:
                return fixedZoneHeight;
        }
    }

    private Bounds ResolveSourceBounds()
    {
        if (boundsSource == FloorBoundsSource.RendererBounds && floorRenderer != null)
            return floorRenderer.bounds;

        if (floorCollider != null)
            return floorCollider.bounds;

        return new Bounds(transform.position, Vector3.one * 0.1f);
    }

    private void AutoFitFloorColliderToSource(Bounds sourceBounds)
    {
        if (floorCollider == null)
            return;

        float width = Mathf.Max(0.01f, sourceBounds.size.x);
        float height = Mathf.Max(0.01f, floorColliderHeight);

        float centerY = sourceBounds.max.y - height * 0.5f + floorColliderTopYOffset;

        Vector2 worldCenter = new Vector2(sourceBounds.center.x, centerY);
        Vector2 worldSize = new Vector2(width, height);

        SetBoxColliderWorldRect(floorCollider, worldCenter, worldSize);
    }

    private static void SetBoxColliderWorldRect(
        BoxCollider2D collider,
        Vector2 worldCenter,
        Vector2 worldSize)
    {
        if (collider == null)
            return;

        Vector3 localCenter = collider.transform.InverseTransformPoint(worldCenter);
        Vector3 lossy = collider.transform.lossyScale;

        float sx = Mathf.Approximately(lossy.x, 0f) ? 1f : Mathf.Abs(lossy.x);
        float sy = Mathf.Approximately(lossy.y, 0f) ? 1f : Mathf.Abs(lossy.y);

        collider.offset = new Vector2(localCenter.x, localCenter.y);
        collider.size = new Vector2(
            Mathf.Max(0.01f, worldSize.x / sx),
            Mathf.Max(0.01f, worldSize.y / sy));
    }

    private void ConfigureColliders()
    {
        if (floorCollider != null)
        {
            floorCollider.enabled = true;
            floorCollider.isTrigger = true;
        }

        if (secureZoneCollider != null)
        {
            secureZoneCollider.enabled = true;
            secureZoneCollider.isTrigger = true;
        }
    }

    private void CacheRefs()
    {
        if (floorCollider == null)
            floorCollider = GetComponent<BoxCollider2D>();

        if (secureZone == null)
            secureZone = GetComponentInChildren<BoatSecureZone>(true);

        if (secureZoneCollider == null && secureZone != null)
            secureZoneCollider = secureZone.GetComponent<BoxCollider2D>();

        if (autoFindFloorRenderer && floorRenderer == null)
            floorRenderer = FindBestFloorRenderer();
    }

    private Renderer FindBestFloorRenderer()
    {
        Renderer ownRenderer = GetComponent<Renderer>();
        if (ownRenderer != null)
            return ownRenderer;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (secureZone != null && r.transform.IsChildOf(secureZone.transform))
                continue;

            return r;
        }

        return null;
    }

#if UNITY_EDITOR
    private bool NeedsEditorRefresh()
    {
        Bounds b = ResolveSourceBounds();

        if (!_hasLastEditorSignature)
            return true;

        if (!Approximately(b.center, _lastSourceCenter))
            return true;

        if (!Approximately(b.size, _lastSourceSize))
            return true;

        if (!Mathf.Approximately(fixedZoneHeight, _lastFixedZoneHeight))
            return true;

        if (!Mathf.Approximately(bottomOffsetFromFloorTop, _lastBottomOffset))
            return true;

        if (!Mathf.Approximately(horizontalPadding, _lastHorizontalPadding))
            return true;

        if (!Mathf.Approximately(floorColliderHeight, _lastFloorColliderHeight))
            return true;

        if (!Mathf.Approximately(floorColliderTopYOffset, _lastFloorColliderTopYOffset))
            return true;

        if (boundsSource != _lastBoundsSource)
            return true;

        if (autoFitFloorColliderToSourceWidth != _lastAutoFitFloorCollider)
            return true;

        return false;
    }

    private void StoreEditorSignature()
    {
        Bounds b = ResolveSourceBounds();

        _hasLastEditorSignature = true;
        _lastSourceCenter = b.center;
        _lastSourceSize = b.size;
        _lastFixedZoneHeight = fixedZoneHeight;
        _lastBottomOffset = bottomOffsetFromFloorTop;
        _lastHorizontalPadding = horizontalPadding;
        _lastFloorColliderHeight = floorColliderHeight;
        _lastFloorColliderTopYOffset = floorColliderTopYOffset;
        _lastBoundsSource = boundsSource;
        _lastAutoFitFloorCollider = autoFitFloorColliderToSourceWidth;
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= 0.000001f;
    }
#endif
}