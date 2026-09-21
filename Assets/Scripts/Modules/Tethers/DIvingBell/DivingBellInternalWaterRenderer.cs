using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders the water actually inside a diving bell by clipping an authored chamber
/// collider against DivingBellAirVolume.WaterSurfaceWorldY.
///
/// This is presentation only. DivingBellAirVolume remains the authority for the
/// waterline, trapped air, venting, and air quality.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class DivingBellInternalWaterRenderer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DivingBellAirVolume airVolume;

    [Tooltip(
        "Collider describing the usable interior chamber. The existing InteriorSafetyVolume BoxCollider2D is a good default. " +
        "BoxCollider2D and single-path PolygonCollider2D are supported.")]
    [SerializeField] private Collider2D chamberCollider;

    [Tooltip(
        "Optional bell sorting authority. Leave blank to auto-resolve from the same bell.")]
    [SerializeField] private DivingBellVisualPresentation visualPresentation;

    [Header("Rendering")]
    [SerializeField] private Material waterMaterial;
    [SerializeField]
    private Color fallbackWaterColor =
        new Color(0.10f, 0.40f, 0.90f, 0.55f);

    [Tooltip(
        "Legacy relative order used when Render In Front Of All Bell Content is disabled.")]
    [SerializeField] private int sortingOrderAboveBellInterior = 1;

    [Tooltip(
        "When enabled, the internal water deliberately renders above the player, BellItems, floor, " +
        "and bell shell so the submerged portion visibly tints everything.")]
    [SerializeField] private bool renderInFrontOfAllBellContent = true;

    [Tooltip(
        "Safe high order above the bell interior when rendering water in front. " +
        "The bell's normal player/item/shell offsets are much smaller than this.")]
    [SerializeField] private int frontWaterSortingOrderOffset = 100;

    [Tooltip(
        "Fallback sorting layer used only when DivingBellVisualPresentation cannot provide its live sorting context.")]
    [SerializeField] private string fallbackSortingLayerName = "WorldItem";

    [SerializeField] private int fallbackSortingOrder = 0;

    [Header("Thresholds")]
    [SerializeField, Range(0f, 0.05f)] private float visibleWaterFillEpsilon = 0.001f;

    [Header("Debug")]
    [SerializeField] private float renderedWaterSurfaceWorldY;
    [SerializeField] private int renderedVertexCount;
    [SerializeField] private int resolvedSortingOrder;
    [SerializeField] private bool verboseLogging = false;

    private Mesh _mesh;
    private MeshRenderer _meshRenderer;
    private Material _runtimeFallbackMaterial;
    private bool _reportedUnsupportedCollider;

    private readonly List<Vector2> _worldPolygon =
        new List<Vector2>(8);

    private readonly List<Vector2> _clippedPolygon =
        new List<Vector2>(8);

    private void Awake()
    {
        ResolveRefs();
        EnsureMesh();
        ApplyRendererSettings();
    }

    private void OnEnable()
    {
        ResolveRefs();
        EnsureMesh();
        ApplyRendererSettings();
        UpdateWaterMesh();
    }

    private void LateUpdate()
    {
        ResolveRefs();
        ApplyLiveSorting();
        UpdateWaterMesh();
    }

    private void OnDisable()
    {
        ClearMesh();
    }

    private void OnDestroy()
    {
        if (_runtimeFallbackMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(_runtimeFallbackMaterial);
            else
                DestroyImmediate(_runtimeFallbackMaterial);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        visibleWaterFillEpsilon =
            Mathf.Clamp(visibleWaterFillEpsilon, 0f, 0.05f);

        // Do not manufacture transient runtime materials while editing the prefab.
        // Assigned project materials may still preview normally.
        _meshRenderer = GetComponent<MeshRenderer>();

        if (_meshRenderer != null &&
            waterMaterial != null)
        {
            _meshRenderer.sharedMaterial = waterMaterial;
        }

        ApplyLiveSorting();
    }
#endif

    public void RefreshNow()
    {
        ResolveRefs();
        EnsureMesh();
        ApplyRendererSettings();
        ApplyLiveSorting();
        UpdateWaterMesh();
    }

    private void UpdateWaterMesh()
    {
        if (airVolume == null ||
            chamberCollider == null ||
            !chamberCollider.enabled ||
            !chamberCollider.gameObject.activeInHierarchy ||
            !airVolume.OpeningSubmerged ||
            airVolume.WaterFill01 <= visibleWaterFillEpsilon)
        {
            ClearMesh();
            return;
        }

        if (!TryBuildChamberWorldPolygon(_worldPolygon))
        {
            ClearMesh();
            return;
        }

        ClipBelowWorldY(
            _worldPolygon,
            airVolume.WaterSurfaceWorldY,
            _clippedPolygon);

        if (_clippedPolygon.Count < 3)
        {
            ClearMesh();
            return;
        }

        EnsureMesh();

        Vector3[] vertices =
            new Vector3[_clippedPolygon.Count];

        for (int i = 0; i < _clippedPolygon.Count; i++)
        {
            vertices[i] =
                transform.InverseTransformPoint(
                    _clippedPolygon[i]);
        }

        int triangleCount =
            Mathf.Max(0, vertices.Length - 2);

        int[] triangles =
            new int[triangleCount * 3];

        int t = 0;

        for (int i = 1; i < vertices.Length - 1; i++)
        {
            triangles[t++] = 0;
            triangles[t++] = i;
            triangles[t++] = i + 1;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();

        renderedWaterSurfaceWorldY =
            airVolume.WaterSurfaceWorldY;

        renderedVertexCount =
            vertices.Length;
    }

    private bool TryBuildChamberWorldPolygon(
        List<Vector2> result)
    {
        result.Clear();

        if (chamberCollider is BoxCollider2D box)
        {
            Vector2 center = box.offset;
            Vector2 half = box.size * 0.5f;

            // Clockwise rectangle, matching the simple fan triangulation pattern
            // already used by CompartmentWaterRenderer.
            AddWorldPoint(result, box.transform, center + new Vector2(-half.x, half.y));
            AddWorldPoint(result, box.transform, center + new Vector2(half.x, half.y));
            AddWorldPoint(result, box.transform, center + new Vector2(half.x, -half.y));
            AddWorldPoint(result, box.transform, center + new Vector2(-half.x, -half.y));

            _reportedUnsupportedCollider = false;
            return true;
        }

        if (chamberCollider is PolygonCollider2D polygon)
        {
            if (polygon.pathCount <= 0)
                return false;

            Vector2[] path = polygon.GetPath(0);
            if (path == null || path.Length < 3)
                return false;

            for (int i = 0; i < path.Length; i++)
            {
                Vector2 local =
                    path[i] +
                    polygon.offset;

                AddWorldPoint(
                    result,
                    polygon.transform,
                    local);
            }

            _reportedUnsupportedCollider = false;
            return true;
        }

        if (!_reportedUnsupportedCollider)
        {
            Debug.LogWarning(
                $"[DivingBellInternalWaterRenderer:{name}] " +
                $"Unsupported chamber collider '{chamberCollider.GetType().Name}'. " +
                "Use the bell's InteriorSafetyVolume BoxCollider2D or a single-path PolygonCollider2D.",
                this);

            _reportedUnsupportedCollider = true;
        }

        return false;
    }

    private static void AddWorldPoint(
        List<Vector2> points,
        Transform sourceTransform,
        Vector2 localPoint)
    {
        points.Add(
            sourceTransform.TransformPoint(
                localPoint));
    }

    private static void ClipBelowWorldY(
        List<Vector2> input,
        float waterSurfaceWorldY,
        List<Vector2> output)
    {
        output.Clear();

        if (input == null || input.Count < 3)
            return;

        Vector2 previous =
            input[input.Count - 1];

        bool previousInside =
            previous.y <= waterSurfaceWorldY;

        for (int i = 0; i < input.Count; i++)
        {
            Vector2 current =
                input[i];

            bool currentInside =
                current.y <= waterSurfaceWorldY;

            if (currentInside != previousInside)
            {
                float dy =
                    current.y -
                    previous.y;

                float interpolation =
                    Mathf.Abs(dy) <= 0.000001f
                        ? 0f
                        : (waterSurfaceWorldY - previous.y) / dy;

                interpolation =
                    Mathf.Clamp01(interpolation);

                output.Add(
                    Vector2.Lerp(
                        previous,
                        current,
                        interpolation));
            }

            if (currentInside)
            {
                output.Add(
                    current);
            }

            previous =
                current;

            previousInside =
                currentInside;
        }
    }

    private void ApplyLiveSorting()
    {
        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();

        if (_meshRenderer == null)
            return;

        int orderOffset =
            renderInFrontOfAllBellContent
                ? Mathf.Max(
                    1,
                    frontWaterSortingOrderOffset)
                : sortingOrderAboveBellInterior;

        if (visualPresentation != null &&
            visualPresentation.TryGetInteriorSortingContext(
                out int sortingLayerId,
                out int interiorBaseOrder))
        {
            _meshRenderer.sortingLayerID =
                sortingLayerId;

            _meshRenderer.sortingOrder =
                interiorBaseOrder +
                orderOffset;

            resolvedSortingOrder =
                _meshRenderer.sortingOrder;

            return;
        }

        if (!string.IsNullOrWhiteSpace(
                fallbackSortingLayerName))
        {
            _meshRenderer.sortingLayerName =
                fallbackSortingLayerName;
        }

        _meshRenderer.sortingOrder =
            fallbackSortingOrder +
            (renderInFrontOfAllBellContent
                ? Mathf.Max(
                    1,
                    frontWaterSortingOrderOffset)
                : 0);

        resolvedSortingOrder =
            _meshRenderer.sortingOrder;
    }

    private void ApplyRendererSettings()
    {
        _meshRenderer =
            GetComponent<MeshRenderer>();

        if (_meshRenderer == null)
            return;

        if (waterMaterial != null)
        {
            _meshRenderer.sharedMaterial =
                waterMaterial;
        }
        else
        {
            if (_runtimeFallbackMaterial == null)
            {
                Shader shader =
                    Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    _runtimeFallbackMaterial =
                        new Material(shader)
                        {
                            name =
                                $"{name}_BellWater_Runtime",
                            color =
                                fallbackWaterColor
                        };
                }
            }

            if (_runtimeFallbackMaterial != null)
            {
                _runtimeFallbackMaterial.color =
                    fallbackWaterColor;

                _meshRenderer.sharedMaterial =
                    _runtimeFallbackMaterial;
            }
        }

        ApplyLiveSorting();
    }

    private void EnsureMesh()
    {
        if (_mesh != null)
            return;

        MeshFilter filter =
            GetComponent<MeshFilter>();

        if (filter == null)
            return;

        _mesh =
            new Mesh
            {
                name = "Diving Bell Internal Water"
            };

        _mesh.MarkDynamic();
        filter.sharedMesh = _mesh;
    }

    private void ClearMesh()
    {
        if (_mesh != null)
            _mesh.Clear();

        renderedVertexCount = 0;
    }

    private void ResolveRefs()
    {
        if (airVolume == null)
        {
            airVolume =
                GetComponent<DivingBellAirVolume>() ??
                GetComponentInParent<DivingBellAirVolume>() ??
                GetComponentInChildren<DivingBellAirVolume>(true);
        }

        if (visualPresentation == null)
        {
            visualPresentation =
                GetComponent<DivingBellVisualPresentation>() ??
                GetComponentInParent<DivingBellVisualPresentation>() ??
                GetComponentInChildren<DivingBellVisualPresentation>(true);
        }

        if (chamberCollider == null)
        {
            // Safe best-effort only: prefer an authored trigger because the bell
            // already uses InteriorSafetyVolume for containment semantics.
            Collider2D[] candidates =
                GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < candidates.Length; i++)
            {
                Collider2D candidate =
                    candidates[i];

                if (candidate == null ||
                    !candidate.isTrigger)
                {
                    continue;
                }

                if (candidate.name.Contains("InteriorSafety"))
                {
                    chamberCollider = candidate;
                    break;
                }
            }
        }
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[DivingBellInternalWaterRenderer:{name}] {message}",
            this);
    }
}
