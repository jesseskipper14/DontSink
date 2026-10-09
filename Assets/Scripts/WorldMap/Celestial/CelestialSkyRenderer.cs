using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 3 BoatScene renderer for the deterministic celestial field.
///
/// It does not create or mutate celestial truth. It asks CelestialFieldSource for the
/// same world truth used by the map overlay, reads authoritative true world position
/// through WorldNavigationService, and projects nearby celestial objects into camera
/// viewport space.
///
/// This component is presentation-only and therefore intentionally contains no
/// GameplayAuthority gate.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CelestialFieldSource))]
public sealed partial class CelestialSkyRenderer : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private CelestialFieldSource fieldSource;
    [SerializeField] private CelestialSkyProjectionSettings projectionSettings;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer legacyStarsRenderer;
    [SerializeField] private SkyVisualManager skyVisualManager;

    [Header("Runtime Root")]
    [SerializeField] private Transform renderRoot;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;
    [SerializeField] private bool warnWhenViewCrossesFieldBounds = true;

    private readonly List<CelestialObject> _queriedObjects = new();


    private CelestialSkySpriteLibrary _spriteLibrary;
    private Material _starMaterial;
    private Material _deepSkyMaterial;
    private Vector2 _lastQueryObserverPosition;
    private float _lastQueryTime = float.NegativeInfinity;
    private bool _hasQueryPosition;
    private bool _forceRefresh = true;
    private bool _ready;
    private bool _runtimeRootInitialized;
    private bool _legacyWasEnabled;
    private bool _legacyTakeoverApplied;
    private bool _warnedMissingSettings;
    private bool _warnedMissingCamera;
    private bool _warnedMissingWorldPosition;
    private bool _warnedCrossingBounds;
    private float _starVisibility01 = 1f;
    private SkyVisualManager _subscribedSkyVisualManager;

    private int _visibleAmbient;
    private int _visibleLandmarks;
    private int _visibleNebulae;
    private int _visibleDeepSky;

    public bool IsReady => _ready;
    public int QueriedObjectCount => _queriedObjects.Count;
    public float StarVisibility01 => _starVisibility01;
    public CelestialSkyProjectionSettings ProjectionSettings => projectionSettings;

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();
        EnsureRenderRoot();
        _spriteLibrary = new CelestialSkySpriteLibrary();
        BuildRuntimeMaterials();
    }

    private void OnEnable()
    {
        AutoWire();
        SubscribeSkyVisibility();
        _forceRefresh = true;
    }

    private void OnDisable()
    {
        UnsubscribeSkyVisibility();
        RestoreLegacyRenderer();
        SetBatchesVisible(false);
        HideConstellationLook();
        _ready = false;
    }

    private void OnDestroy()
    {
        UnsubscribeSkyVisibility();
        RestoreLegacyRenderer();

        if (_spriteLibrary != null)
        {
            _spriteLibrary.Dispose();
            _spriteLibrary = null;
        }

        DisposeBatches();
        DestroyRuntimeMaterial(ref _starMaterial);
        DestroyRuntimeMaterial(ref _deepSkyMaterial);
        DestroyRuntimeMaterial(ref _constellationLineMaterial);

        if (renderRoot != null && renderRoot.parent == transform && renderRoot.name == "__CelestialSkyRuntime")
            Destroy(renderRoot.gameObject);
    }

    private void Update()
    {
        if (!EnsureReady())
            return;

        if (!TryGetObserverPosition(out Vector2 observerWorldPosition))
        {
            if (!_warnedMissingWorldPosition && verboseLogging)
            {
                _warnedMissingWorldPosition = true;
                Debug.LogWarning(
                    "[CelestialSkyRenderer] No authoritative true world position is available yet.",
                    this);
            }

            SetBatchesVisible(false);
            return;
        }

        _warnedMissingWorldPosition = false;

        if (_batchPixelWidth != targetCamera.pixelWidth || _batchPixelHeight != targetCamera.pixelHeight || ShouldRefreshQuery(observerWorldPosition))
            RefreshQuery(observerWorldPosition);

        ProjectAndRender(observerWorldPosition);
    }

    private bool EnsureReady()
    {
        AutoWire();

        if (projectionSettings == null)
        {
            if (!_warnedMissingSettings)
            {
                _warnedMissingSettings = true;
                Debug.LogWarning(
                    "[CelestialSkyRenderer] Missing CelestialSkyProjectionSettings.",
                    this);
            }
            return false;
        }

        _warnedMissingSettings = false;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                if (!_warnedMissingCamera)
                {
                    _warnedMissingCamera = true;
                    Debug.LogWarning(
                        "[CelestialSkyRenderer] No target camera / Camera.main available.",
                        this);
                }
                return false;
            }
        }

        _warnedMissingCamera = false;

        if (fieldSource == null || !fieldSource.EnsureField())
            return false;

        EnsureRenderRoot();
        // Runtime-only resources are not serialized across Editor domain reloads.
        if (_spriteLibrary == null) _spriteLibrary = new CelestialSkySpriteLibrary();
        BuildRuntimeMaterials();
        ApplyLegacyTakeoverIfNeeded();

        _ready = true;
        return true;
    }

    private void RefreshQuery(Vector2 observerWorldPosition)
    {
        CelestialField field = fieldSource.Field;
        if (field == null || !field.IsValid)
            return;

        Rect queryRect = CelestialSkyProjection.BuildSceneQueryWorldRect(
            field.WorldBounds,
            observerWorldPosition,
            projectionSettings, ResolveProjectionViewportAspect());

        field.Query(queryRect, _queriedObjects, clearResults: true);
        CelestialFieldGenerator.AppendVoidAmbientStars(field, queryRect,
            projectionSettings.voidAmbientFadeDistanceWorld, _queriedObjects);

        RebuildBatches();

        _lastQueryObserverPosition = observerWorldPosition;
        _hasQueryPosition = true;
        _lastQueryTime = Time.unscaledTime;
        _forceRefresh = false;

        bool crossesBounds = CelestialSkyProjection.ViewCrossesFieldBounds(
            field.WorldBounds,
            observerWorldPosition,
            projectionSettings);

        if (crossesBounds && warnWhenViewCrossesFieldBounds && !_warnedCrossingBounds)
        {
            _warnedCrossingBounds = true;
            Debug.LogWarning(
                "[CelestialSkyRenderer] The visible celestial window currently crosses the generated field bounds. " +
                "Outside the known world, only the configured fading ambient void transition is rendered; navigational objects remain within known bounds.",
                this);
        }
        else if (!crossesBounds)
        {
            _warnedCrossingBounds = false;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[CelestialSkyRenderer] Refreshed query | Observer={Format(observerWorldPosition)} " +
                $"VisibleWorld={Format(CelestialSkyProjection.GetVisibleWorldSize(field.WorldBounds, projectionSettings))} " +
                $"Queried={_queriedObjects.Count}",
                this);
        }
    }

    private bool ShouldRefreshQuery(Vector2 observerWorldPosition)
    {
        if (_forceRefresh || !_hasQueryPosition)
            return true;

        CelestialField field = fieldSource != null ? fieldSource.Field : null;
        if (field == null)
            return true;

        Vector2 visibleSize = CelestialSkyProjection.GetVisibleWorldSize(field.WorldBounds, projectionSettings);
        float threshold = Mathf.Max(
            0.001f,
            Mathf.Min(visibleSize.x, visibleSize.y) * projectionSettings.queryRefreshDistanceFraction);

        if ((observerWorldPosition - _lastQueryObserverPosition).sqrMagnitude >= threshold * threshold)
            return true;

        return Time.unscaledTime - _lastQueryTime >= projectionSettings.maximumQueryRefreshSeconds;
    }

    private void EnsureRenderRoot()
    {
        if (renderRoot == null)
        {
            var go = new GameObject("__CelestialSkyRuntime");
            go.transform.SetParent(transform, false);
            renderRoot = go.transform;
        }
        if (_runtimeRootInitialized) return;
        // Hot reload may retain runtime children but lose the nonserialized pool.
        // Retire only this renderer's star objects; constellation look uses its
        // own children and is independent of the celestial sprite batches.
        for (int i = renderRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = renderRoot.GetChild(i).gameObject;
            if (!child.name.StartsWith("Celestial_") && !child.name.StartsWith("CelestialSkySlot_") &&
                !child.name.StartsWith("CelestialBatch_")) continue;
            child.SetActive(false);
            if (child.name.StartsWith("CelestialBatch_"))
            {
                MeshFilter filter = child.GetComponent<MeshFilter>();
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                if (filter != null) Destroy(filter.sharedMesh);
                if (renderer != null) Destroy(renderer.sharedMaterial);
            }
            Destroy(child);
        }
        _runtimeRootInitialized = true;
    }

    private bool TryGetObserverPosition(out Vector2 position)
    {
        if (fieldSource != null && fieldSource.FixedSky != null)
        {
            position = fieldSource.FixedSky.ObserverPosition;
            return fieldSource.FixedSky.IsGenerated;
        }
        return WorldNavigationService.TryGetTrueWorldPosition(out position);
    }

    private void AutoWire()
    {
        if (fieldSource == null)
            fieldSource = GetComponent<CelestialFieldSource>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (fieldSource != null && fieldSource.FixedSky != null)
        {
            UnsubscribeSkyVisibility();
            HandleStarVisibilityChanged(1f);
            return;
        }

        if (legacyStarsRenderer == null && SceneContext.Current != null)
            legacyStarsRenderer = SceneContext.Current.starsRenderer;

        SkyVisualManager authoritativeSky = ServiceRoot.Instance != null ? ServiceRoot.Instance.SkyManager : null;
        if (authoritativeSky != null)
            skyVisualManager = authoritativeSky;
        else if (skyVisualManager == null)
        {
            skyVisualManager = FindAnyObjectByType<SkyVisualManager>();
        }
        if (isActiveAndEnabled) SubscribeSkyVisibility();
    }

    private void SubscribeSkyVisibility()
    {
        if (fieldSource != null && fieldSource.FixedSky != null)
        {
            UnsubscribeSkyVisibility();
            HandleStarVisibilityChanged(1f);
            return;
        }
        if (_subscribedSkyVisualManager != skyVisualManager)
        {
            UnsubscribeSkyVisibility();
            _subscribedSkyVisualManager = skyVisualManager;
            if (_subscribedSkyVisualManager != null)
                _subscribedSkyVisualManager.OnStarVisibilityChanged += HandleStarVisibilityChanged;
            _forceRefresh = true;
        }
        if (_subscribedSkyVisualManager != null)
            HandleStarVisibilityChanged(_subscribedSkyVisualManager.StarVisibility01);
    }

    private void UnsubscribeSkyVisibility()
    {
        if (_subscribedSkyVisualManager != null)
            _subscribedSkyVisualManager.OnStarVisibilityChanged -= HandleStarVisibilityChanged;
        _subscribedSkyVisualManager = null;
    }

    private void HandleStarVisibilityChanged(float visibility01)
    {
        float visibility = Mathf.Clamp01(visibility01);
        if (Mathf.Approximately(_starVisibility01, visibility)) return;
        _starVisibility01 = visibility;

        if (_starVisibility01 > 0.001f)
            _forceRefresh = true;
    }

    private void ApplyLegacyTakeoverIfNeeded()
    {
        if (_legacyTakeoverApplied || projectionSettings == null || !projectionSettings.disableLegacyStarsRenderer)
            return;

        if (legacyStarsRenderer == null && fieldSource?.FixedSky == null && SceneContext.Current != null)
            legacyStarsRenderer = SceneContext.Current.starsRenderer;

        if (legacyStarsRenderer == null)
            return;

        _legacyWasEnabled = legacyStarsRenderer.enabled;
        legacyStarsRenderer.enabled = false;
        _legacyTakeoverApplied = true;


    }

    private void RestoreLegacyRenderer()
    {
        if (!_legacyTakeoverApplied)
            return;

        if (legacyStarsRenderer != null)
            legacyStarsRenderer.enabled = _legacyWasEnabled;

        _legacyTakeoverApplied = false;
    }

    private void BuildRuntimeMaterials()
    {
        if (_starMaterial == null)
        {
            Shader shader = Shader.Find("Custom/CelestialStarBatch2D");
            if (shader != null)
            {
                _starMaterial = new Material(shader)
                {
                    name = "CelestialStarUnlit_Runtime",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                Debug.LogWarning(
                    "[CelestialSkyRenderer] Custom/CelestialStarBatch2D shader was not found. " +
                    "Star batches cannot render until this shader is available.",
                    this);
            }
        }

        if (_deepSkyMaterial == null)
        {
            Shader shader = Shader.Find("Custom/CelestialAlphaBatch2D");
            if (shader != null)
            {
                _deepSkyMaterial = new Material(shader)
                {
                    name = "CelestialDeepSkyUnlit_Runtime",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                Debug.LogWarning(
                    "[CelestialSkyRenderer] Custom/CelestialAlphaBatch2D shader was not found. " +
                    "Nebula/deep-sky batches cannot render until this shader is available.",
                    this);
            }
        }
    }

    private Material ResolveMaterial(CelestialObjectKind kind)
    {
        switch (kind)
        {
            case CelestialObjectKind.AmbientStar:
            case CelestialObjectKind.LandmarkStar:
                return _starMaterial;

            case CelestialObjectKind.Nebula:
            case CelestialObjectKind.DeepSkyObject:
                return _deepSkyMaterial;

            default:
                return _deepSkyMaterial;
        }
    }

    private static float VisualHash01(Vector2 position, int variant)
    {
        float n = Mathf.Sin(
            position.x * 12.9898f +
            position.y * 78.233f +
            variant * 37.719f) * 43758.5453f;

        return n - Mathf.Floor(n);
    }

    private static void DestroyRuntimeMaterial(ref Material material)
    {
        if (material != null)
            Destroy(material);
        material = null;
    }

    private int ResolveSortingOrder(CelestialObjectKind kind)
    {
        int baseOrder = legacyStarsRenderer != null
            ? legacyStarsRenderer.sortingOrder
            : projectionSettings.fallbackSortingOrder;

        switch (kind)
        {
            case CelestialObjectKind.Nebula: return baseOrder - 2;
            case CelestialObjectKind.DeepSkyObject: return baseOrder - 1;
            case CelestialObjectKind.LandmarkStar: return baseOrder + 1;
            default: return baseOrder;
        }
    }

    private Color ResolveBaseColor(CelestialObject obj)
    {
        float saturation;

        switch (obj.Kind)
        {
            case CelestialObjectKind.LandmarkStar:
                saturation = projectionSettings.landmarkColorSaturation;
                break;
            case CelestialObjectKind.Nebula:
                saturation = projectionSettings.nebulaColorSaturation;
                break;
            case CelestialObjectKind.DeepSkyObject:
                saturation = projectionSettings.deepSkyColorSaturation;
                break;
            default:
                saturation = projectionSettings.ambientColorSaturation;
                break;
        }

        return projectionSettings.ResolveColor(obj.ColorClass, saturation);
    }

    private float ResolveObjectAlpha(CelestialObject obj)
    {
        float brightness = Mathf.Lerp(0.48f, 1f, obj.Brightness01);
        if (obj.Kind == CelestialObjectKind.AmbientStar && fieldSource != null && fieldSource.Field != null &&
            !fieldSource.Field.WorldBounds.Contains(obj.WorldPosition))
            brightness *= CelestialFieldGenerator.GetVoidAmbientWeight(fieldSource.Field.WorldBounds,
                obj.WorldPosition, projectionSettings.voidAmbientFadeDistanceWorld);

        switch (obj.Kind)
        {
            case CelestialObjectKind.LandmarkStar:
                return projectionSettings.landmarkAlpha * brightness;
            case CelestialObjectKind.Nebula:
                return projectionSettings.nebulaAlpha * brightness;
            case CelestialObjectKind.DeepSkyObject:
                return projectionSettings.deepSkyAlpha * brightness;
            default:
                return projectionSettings.ambientAlpha * brightness;
        }
    }

    private void ResolvePixelSize(CelestialObject obj, out float widthPixels, out float heightPixels)
    {
        float emphasis = Mathf.Clamp01(obj.Prominence01 * 0.68f + obj.Brightness01 * 0.32f);

        switch (obj.Kind)
        {
            case CelestialObjectKind.LandmarkStar:
            {
                float size = Mathf.Lerp(
                    projectionSettings.landmarkSizePixelsMin,
                    projectionSettings.landmarkSizePixelsMax,
                    emphasis);
                widthPixels = size;
                heightPixels = size;
                break;
            }

            case CelestialObjectKind.Nebula:
            {
                ProjectFootprintPixels(
                    obj,
                    projectionSettings.nebulaSizeMultiplier,
                    projectionSettings.nebulaMinimumSizePixels,
                    projectionSettings.nebulaMaximumSizePixels,
                    out widthPixels,
                    out heightPixels);

                // Deterministic shape variation without mutating world truth.
                float aspect = Mathf.Lerp(0.48f, 0.88f, (Mathf.Abs(obj.VisualVariant) % 4) / 3f);
                heightPixels *= aspect;
                break;
            }

            case CelestialObjectKind.DeepSkyObject:
                ProjectFootprintPixels(
                    obj,
                    projectionSettings.deepSkySizeMultiplier,
                    projectionSettings.deepSkyMinimumSizePixels,
                    projectionSettings.deepSkyMaximumSizePixels,
                    out widthPixels,
                    out heightPixels);
                break;

            default:
            {
                float size = Mathf.Lerp(
                    projectionSettings.ambientSizePixelsMin,
                    projectionSettings.ambientSizePixelsMax,
                    emphasis);
                widthPixels = size;
                heightPixels = size;
                break;
            }
        }
    }

    private void ProjectFootprintPixels(
        CelestialObject obj,
        float multiplier,
        float minPixels,
        float maxPixels,
        out float widthPixels,
        out float heightPixels)
    {
        float metricWorldSpan = CelestialSkyProjection.GetMetricWorldSpan(
            fieldSource.Field.WorldBounds,
            projectionSettings);

        float diameterWorld = Mathf.Max(0.01f, obj.FootprintRadiusWorld * 2f);

        // Shape-preserving celestial projection means a circular world-space footprint should remain
        // circular on screen. Use the horizontal metric scale for BOTH pixel dimensions rather than
        // independently normalizing X and Y.
        float projectedPixels =
            diameterWorld / Mathf.Max(0.0001f, metricWorldSpan) *
            Mathf.Max(1, targetCamera.pixelWidth) * multiplier;

        float clamped = Mathf.Clamp(projectedPixels, minPixels, maxPixels);
        widthPixels = clamped;
        heightPixels = clamped;
    }

    private float ResolveProjectionViewportAspect()
    {
        if (targetCamera == null)
            return 1f;

        int width = Mathf.Max(1, targetCamera.pixelWidth);
        int height = Mathf.Max(1, targetCamera.pixelHeight);
        return width / (float)height;
    }

    private float ResolveRotation(CelestialObject obj)
    {
        switch (obj.Kind)
        {
            case CelestialObjectKind.Nebula:
            case CelestialObjectKind.DeepSkyObject:
                return obj.RotationDegrees;

            case CelestialObjectKind.LandmarkStar:
                return (Mathf.Abs(obj.VisualVariant) % 2 == 0)
                    ? obj.RotationDegrees * 0.25f
                    : 0f;

            default:
                return 0f;
        }
    }

    private bool ShouldShowKind(CelestialObjectKind kind)
    {
        switch (kind)
        {
            case CelestialObjectKind.AmbientStar: return projectionSettings.showAmbientStars;
            case CelestialObjectKind.LandmarkStar: return projectionSettings.showLandmarkStars;
            case CelestialObjectKind.Nebula: return projectionSettings.showNebulae;
            case CelestialObjectKind.DeepSkyObject: return projectionSettings.showDeepSkyObjects;
            default: return false;
        }
    }

    private void GetWorldUnitsPerPixel(out float worldPerPixelX, out float worldPerPixelY)
    {
        Vector3 left = SkyViewportToWorld(new Vector2(0f, 0.5f));
        Vector3 right = SkyViewportToWorld(new Vector2(1f, 0.5f));
        Vector3 bottom = SkyViewportToWorld(new Vector2(0.5f, 0f));
        Vector3 top = SkyViewportToWorld(new Vector2(0.5f, 1f));

        worldPerPixelX = Vector3.Distance(left, right) / Mathf.Max(1, targetCamera.pixelWidth);
        worldPerPixelY = Vector3.Distance(bottom, top) / Mathf.Max(1, targetCamera.pixelHeight);
    }

    private float ResolveProjectionDepth()
    {
        // For the project's orthographic BoatScene camera this value does not alter X/Y,
        // but supplying a valid positive distance also keeps this safe for a perspective camera.
        return Mathf.Max(targetCamera.nearClipPlane + 0.01f, 1f);
    }

    private float ResolveRenderWorldZ()
    {
        if (legacyStarsRenderer != null)
            return legacyStarsRenderer.transform.position.z;

        return 0f;
    }

    private void IncrementVisibleCount(CelestialObjectKind kind)
    {
        switch (kind)
        {
            case CelestialObjectKind.AmbientStar: _visibleAmbient++; break;
            case CelestialObjectKind.LandmarkStar: _visibleLandmarks++; break;
            case CelestialObjectKind.Nebula: _visibleNebulae++; break;
            case CelestialObjectKind.DeepSkyObject: _visibleDeepSky++; break;
        }
    }

    [ContextMenu("Force Refresh Celestial Sky")]
    public void ForceRefresh()
    {
        _forceRefresh = true;
    }

    [ContextMenu("Log Celestial Sky Projection")]
    public void LogProjectionSummary()
    {
        if (!EnsureReady())
        {
            Debug.LogWarning("[CelestialSkyRenderer] Renderer is not ready.", this);
            return;
        }

        bool hasPosition = TryGetObserverPosition(out Vector2 observer);
        if (hasPosition) UpdateVisibleCounts(observer);
        Vector2 visibleWorld = CelestialSkyProjection.GetVisibleWorldSize(
            fieldSource.Field.WorldBounds,
            projectionSettings);
        Vector2 sceneMetricWorld = CelestialSkyProjection.GetSceneMetricWorldSize(
            fieldSource.Field.WorldBounds,
            projectionSettings,
            ResolveProjectionViewportAspect());

        Debug.Log(
            $"[CelestialSkyRenderer] Ready={_ready}, " +
            $"Observer={(hasPosition ? Format(observer) : "<none>")}, " +
            $"WorldFraction=({projectionSettings.horizontalWorldFraction:0.####}, {projectionSettings.verticalWorldFraction:0.####}), " +
            $"QueryWorld={Format(visibleWorld)}, SceneMetricWorld={Format(sceneMetricWorld)}, NorthUp=True, " +
            $"Queried={_queriedObjects.Count}, " +
            $"Visible Ambient={_visibleAmbient}, Landmarks={_visibleLandmarks}, Nebulae={_visibleNebulae}, DeepSky={_visibleDeepSky}, " +
            $"StarAlpha={_starVisibility01:0.000}, " +
            $"CrossesBounds={(hasPosition && CelestialSkyProjection.ViewCrossesFieldBounds(fieldSource.Field.WorldBounds, observer, projectionSettings))}",
            this);
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
