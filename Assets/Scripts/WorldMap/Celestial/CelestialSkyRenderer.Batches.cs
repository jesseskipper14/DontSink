using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class CelestialSkyRenderer
{
    // One mesh per sprite style, rather than one GameObject per celestial object.
    // Mesh data changes only when the spatial query changes. Observer motion,
    // projection, center culling and individual twinkle run in the vertex shader.
    private sealed class SkyBatch
    {
        public GameObject gameObject;
        public MeshRenderer renderer;
        public Mesh mesh;
        public Material material;
        public CelestialObjectKind kind;
        public readonly List<Vector3> centers = new();
        public readonly List<Vector2> uv = new();
        public readonly List<Vector2> offsets = new();
        public readonly List<Vector4> twinkle = new();
        public readonly List<Color> colors = new();
        public readonly List<int> indices = new();

        public void Clear()
        {
            centers.Clear(); uv.Clear(); offsets.Clear(); twinkle.Clear();
            colors.Clear(); indices.Clear();
        }
        public void Upload()
        {
            mesh.Clear();
            mesh.SetVertices(centers);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, offsets);
            mesh.SetUVs(2, twinkle);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0, false);
            // Vertex positions contain celestial coordinates. CPU culling needs the
            // actual shader-projected world bounds, supplied each frame below.
        }
    }

    private readonly Dictionary<Sprite, SkyBatch> _batches = new();
    private int _batchPixelWidth, _batchPixelHeight;
    private static readonly int ObserverId = Shader.PropertyToID("_Observer");
    private static readonly int ProjectionId = Shader.PropertyToID("_Projection");
    private static readonly int EnvelopeId = Shader.PropertyToID("_Envelope");
    private static readonly int OriginId = Shader.PropertyToID("_SkyOrigin");
    private static readonly int RightId = Shader.PropertyToID("_SkyRight");
    private static readonly int UpId = Shader.PropertyToID("_SkyUp");
    private static readonly int PixelsId = Shader.PropertyToID("_PixelWorld");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");

    private void RebuildBatches()
    {
        _batchPixelWidth = targetCamera.pixelWidth;
        _batchPixelHeight = targetCamera.pixelHeight;
        foreach (SkyBatch batch in _batches.Values) batch.Clear();
        foreach (CelestialObject obj in _queriedObjects)
        {
            Sprite sprite = _spriteLibrary.Resolve(obj);
            if (!_batches.TryGetValue(sprite, out SkyBatch batch))
            {
                Material template = ResolveMaterial(obj.Kind);
                if (template == null) continue;
                var go = new GameObject($"CelestialBatch_{sprite.name}");
                go.layer = gameObject.layer;
                go.transform.SetParent(renderRoot, false);
                var mesh = new Mesh { name = go.name, indexFormat = IndexFormat.UInt32 };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                var material = new Material(template) { name = go.name, hideFlags = HideFlags.DontSave };
                material.mainTexture = sprite.texture;
                renderer.sharedMaterial = material;
                batch = new SkyBatch { gameObject = go, renderer = renderer, mesh = mesh,
                    material = material, kind = obj.Kind };
                _batches.Add(sprite, batch);
            }
            batch.renderer.sortingLayerID = legacyStarsRenderer != null
                ? legacyStarsRenderer.sortingLayerID : SortingLayer.NameToID(projectionSettings.fallbackSortingLayerName);
            batch.renderer.sortingOrder = ResolveSortingOrder(obj.Kind);
            AddBatchQuad(batch, obj);
        }
        foreach (SkyBatch batch in _batches.Values) batch.Upload();
    }

    private void AddBatchQuad(SkyBatch batch, CelestialObject obj)
    {
        ResolvePixelSize(obj, out float width, out float height);
        float angle = ResolveRotation(obj) * Mathf.Deg2Rad;
        Color color = ResolveBaseColor(obj);
        color.a = ResolveObjectAlpha(obj);
        bool landmark = obj.Kind == CelestialObjectKind.LandmarkStar;
        bool star = landmark || obj.Kind == CelestialObjectKind.AmbientStar;
        Vector4 twinkle = star ? new Vector4(
            VisualHash01(obj.WorldPosition, obj.VisualVariant) * Mathf.PI * 2f,
            Mathf.Lerp(projectionSettings.twinkleSpeedMin, projectionSettings.twinkleSpeedMax,
                VisualHash01(obj.WorldPosition * 1.731f, obj.VisualVariant + 17)),
            landmark ? projectionSettings.landmarkTwinkleStrength : projectionSettings.ambientTwinkleStrength,
            (landmark ? projectionSettings.landmarkGlowStrength : projectionSettings.ambientGlowStrength) *
                Mathf.Lerp(0.9f, 1.15f, Mathf.Clamp01(obj.Brightness01))) : new Vector4(0, 0, 0, 1);
        int start = batch.centers.Count;
        for (int corner = 0; corner < 4; corner++)
        {
            float x = (corner == 1 || corner == 2) ? 1 : 0;
            float y = corner >= 2 ? 1 : 0;
            // Rotate in world XY, as the previous SpriteRenderer did. Independent
            // pixel axes are converted to world lengths by the shader.
            batch.centers.Add(new Vector3(obj.WorldPosition.x, obj.WorldPosition.y, angle));
            batch.uv.Add(new Vector2(x, y));
            batch.offsets.Add(new Vector2((x - .5f) * width, (y - .5f) * height));
            batch.twinkle.Add(twinkle);
            batch.colors.Add(color);
        }
        batch.indices.Add(start); batch.indices.Add(start + 1); batch.indices.Add(start + 2);
        batch.indices.Add(start); batch.indices.Add(start + 2); batch.indices.Add(start + 3);
    }

    private void ProjectAndRender(Vector2 observer)
    {
        if (fieldSource.Field == null || targetCamera == null) return;
        bool visible = _starVisibility01 > .001f;
        if (renderRoot.gameObject.activeSelf != visible) renderRoot.gameObject.SetActive(visible);
        if (!visible) return;

        Vector3 origin = SkyViewportToWorld(Vector2.zero);
        Vector3 right = SkyViewportToWorld(Vector2.right) - origin;
        Vector3 up = SkyViewportToWorld(Vector2.up) - origin;
        origin.z = ResolveRenderWorldZ(); right.z = up.z = 0f;
        Rect envelope = CelestialSkyProjection.GetSceneViewportRect(projectionSettings);
        Vector3 a = origin + right * envelope.xMin + up * envelope.yMin;
        Vector3 b = origin + right * envelope.xMax + up * envelope.yMax;
        Vector3 c = origin + right * envelope.xMin + up * envelope.yMax;
        Vector3 d = origin + right * envelope.xMax + up * envelope.yMin;
        Bounds bounds = new Bounds(a, Vector3.zero);
        bounds.Encapsulate(b); bounds.Encapsulate(c); bounds.Encapsulate(d);
        bounds.Expand(Mathf.Max(right.magnitude, up.magnitude) * 2f + 1f);
        Vector4 pixels = new Vector4(right.magnitude / Mathf.Max(1, _batchPixelWidth),
            up.magnitude / Mathf.Max(1, _batchPixelHeight), 0, 0);
        Vector4 projection = new Vector4(CelestialSkyProjection.GetMetricWorldSpan(fieldSource.Field.WorldBounds,
            projectionSettings), ResolveProjectionViewportAspect(), projectionSettings.SkyViewportCenterY,
            fieldSource.Field.WorldBounds.width);
        foreach (SkyBatch batch in _batches.Values)
        {
            batch.renderer.enabled = batch.indices.Count > 0 && ShouldShowKind(batch.kind);
            batch.renderer.bounds = bounds;
            Material material = batch.material;
            material.SetVector(ObserverId, new Vector4(observer.x, observer.y, 0, 0));
            material.SetVector(ProjectionId, projection);
            material.SetVector(EnvelopeId, new Vector4(envelope.xMin, envelope.yMin, envelope.xMax, envelope.yMax));
            material.SetVector(OriginId, origin); material.SetVector(RightId, right); material.SetVector(UpId, up);
            material.SetVector(PixelsId, pixels); material.SetFloat(VisibilityId, _starVisibility01);
        }
    }

    private void SetBatchesVisible(bool visible)
    {
        foreach (SkyBatch batch in _batches.Values) batch.renderer.enabled = visible;
    }

    private void UpdateVisibleCounts(Vector2 observer)
    {
        _visibleAmbient = _visibleLandmarks = _visibleNebulae = _visibleDeepSky = 0;
        foreach (CelestialObject obj in _queriedObjects)
            if (ShouldShowKind(obj.Kind) && CelestialSkyProjection.TryProjectToSceneViewport(
                fieldSource.Field.WorldBounds, observer, obj.WorldPosition, projectionSettings,
                ResolveProjectionViewportAspect(), out _, out _)) IncrementVisibleCount(obj.Kind);
    }

    private void DisposeBatches()
    {
        foreach (SkyBatch batch in _batches.Values)
        {
            Destroy(batch.mesh); Destroy(batch.material); Destroy(batch.gameObject);
        }
        _batches.Clear();
    }
}
