using UnityEngine;

/// <summary>BoatScene background silhouette. No colliders, navigation writes, discovery, or camera ownership.</summary>
[DisallowMultipleComponent]
public sealed class BoatIslandVisual2D : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reuse the NodeGroundTerrain material. It must support transparent _Color tint.")]
    [SerializeField] private Material islandMaterial;
    [Tooltip("Optional: leave empty to resolve the single active simulation in this scene.")]
    [SerializeField] private BoatPilotingSimulation simulation;
    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "WorldBackdrop";
    [SerializeField] private int sortingOrder;
    [SerializeField] private Color tint = Color.white;
    [Header("Perceptual Scale (physical scene units)")]
    [SerializeField, Min(.1f)] private float widthPerMapUnit = 4f;
    [SerializeField, Min(1f)] private float minimumWidth = 36f;
    [SerializeField, Min(1f)] private float maximumWidth = 360f;
    [SerializeField, Min(1f)] private float maximumHeight = 30f;
    [SerializeField, Min(0f)] private float approachOffset = 50f;
    [SerializeField, Min(.1f)] private float recessionMarginMapUnits = 2f;
    [SerializeField, Range(0f, 1f)] private float alongsideParallax = .15f;
    [SerializeField, Min(.1f)] private float fadeSeconds = 1f;
    [SerializeField, Min(.1f)] private float smoothingSeconds = 1f;
    [Tooltip("Visual visibility range as a fraction of geographic detection range. 0.65 shortens it by 35%.")]
    [SerializeField, Range(.1f, 1f)] private float visibilityRangeMultiplier = .65f;
    private Vector2 _patternScale;
    private readonly BoatIslandProjection _projection = new();
    private Mesh _mesh;
    private MeshRenderer _renderer;
    private GameObject _visual;
    private MaterialPropertyBlock _properties;
    private BoatTerrainStreamer2D _terrain;
    private BoatPilotingSimulation _activeSimulation;
    private object _voyage;
    private int _meshId, _warpRevision, _contextRevision;
    private float _nextResolve;
    private int _visualSortingLayer, _visualSortingOrder;
    private bool _warned;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    public BoatIslandPhase Phase => _projection.Phase;
    public int VisibleLandmassId => _projection.LandmassId;

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextResolve)
        {
            Resolve(); _nextResolve = Time.unscaledTime + 1f;
        }
        var active = _activeSimulation;
        if (active == null || !active.isActiveAndEnabled || active.Boat == null ||
            _terrain == null || !_terrain.isActiveAndEnabled || !active.VoyageStrip.IsActive)
        { Hide(); return; }
        var voyage = GameState.I != null ? GameState.I.activeTravel : null;
        if (!ReferenceEquals(_voyage, voyage))
        { Hide(); _voyage = voyage; _warpRevision = active.GetComponent<BoatSceneWorldPositionBridge>()?.DebugWarpRevision ?? 0; }
        var bridge = active.GetComponent<BoatSceneWorldPositionBridge>();
        int warp = bridge != null ? bridge.DebugWarpRevision : 0;
        if (_warpRevision != warp || _contextRevision != _terrain.LandContextRevision)
        {
            _projection.Retire(); _warpRevision = warp; _contextRevision = _terrain.LandContextRevision;
        }
        if (islandMaterial == null || !islandMaterial.HasProperty(ColorId))
        {
            if (!_warned) { Debug.LogWarning("Assign a transparent NodeGroundTerrain material to BoatIslandVisual2D.", this); _warned = true; }
            Hide(); return;
        }
        _warned = false;
        if (_visual == null) CreateVisual();
        var encounter = _terrain.LandEncounter;
        if (encounter.HasLandmass)
        {
            float range = encounter.VisibilityRange * Mathf.Clamp(visibilityRangeMultiplier, .1f, 1);
            encounter = encounter.Distance <= range ? new BoatLandEncounter(encounter.BoatWorldPosition, encounter.IsOnLand,
                encounter.LandmassId, encounter.NearestLandSample, encounter.OffsetToLand, encounter.Distance,
                encounter.ApproximateArea, encounter.EquivalentDiameter, range) : default;
        }
        _projection.Tick(encounter, active.VoyageStrip.Position, Time.deltaTime,
            widthPerMapUnit, minimumWidth, maximumWidth, maximumHeight, approachOffset,
            recessionMarginMapUnits, alongsideParallax, fadeSeconds, smoothingSeconds);
        _renderer.enabled = _projection.Opacity > 0;
        if (!_renderer.enabled) { if (_projection.LandmassId == 0) _meshId = 0; return; }
        if (_meshId != _projection.LandmassId)
        {
            BuildSilhouette(_projection.LandmassId ^ _terrain.LandWorldSeed); _meshId = _projection.LandmassId;
            _patternScale = new Vector2(_projection.Width, _projection.Height);
        }
        _renderer.sharedMaterial = islandMaterial;
        _renderer.sortingLayerID = _visualSortingLayer; _renderer.sortingOrder = _visualSortingOrder;
        _visual.transform.position = new Vector3(active.Boat.transform.position.x + _projection.OffsetX, _terrain.WaterLevelY, 0);
        _visual.transform.rotation = Quaternion.identity;
        _visual.transform.localScale = new Vector3(_projection.Width, _projection.Height, 1);
        Color color = islandMaterial.GetColor(ColorId) * tint; color.a *= _projection.Opacity;
        _properties.SetColor(ColorId, color);
        _properties.SetFloat("_UseObjectPattern", 1);
        _properties.SetVector("_ObjectPatternScale", new Vector4(_patternScale.x, _patternScale.y, 0, 0));
        _properties.SetFloat("_UseGlobalWaterLevel", 0);
        _properties.SetFloat("_WaterLevelY", _terrain.WaterLevelY);
        _renderer.SetPropertyBlock(_properties);
    }

    private void Resolve()
    {
        ResolveWaterOrdering();
        BoatTerrainStreamer2D.TryGet(gameObject.scene, out _terrain);
        _activeSimulation = null;
        if (simulation != null && simulation.gameObject.scene == gameObject.scene && simulation.isActiveAndEnabled)
        { _activeSimulation = simulation; return; }
        foreach (var candidate in FindObjectsByType<BoatPilotingSimulation>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene != gameObject.scene || !candidate.isActiveAndEnabled) continue;
            if (_activeSimulation != null) { _activeSimulation = null; return; } // Ambiguous local boat context: hide.
            _activeSimulation = candidate;
        }
    }

    private void ResolveWaterOrdering()
    {
        _visualSortingLayer = SortingLayer.NameToID(sortingLayerName);
        _visualSortingOrder = sortingOrder;
        int backWater = SortingLayer.NameToID("BackWater");
        // A background silhouette must precede both ocean layers. Existing scenes
        // authored WorldBackdrop after BackWater; keep their serialized values intact.
        if (backWater == 0 || SortingLayer.GetLayerValueFromID(_visualSortingLayer) <
            SortingLayer.GetLayerValueFromID(backWater)) return;
        _visualSortingLayer = backWater;
        int firstWaterOrder = 0;
        foreach (var water in FindObjectsByType<WaterMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (water.gameObject.scene != gameObject.scene) continue;
            var renderer = water.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sortingLayerID == backWater)
                firstWaterOrder = Mathf.Min(firstWaterOrder, renderer.sortingOrder);
        }
        _visualSortingOrder = Mathf.Min(sortingOrder, firstWaterOrder - 1);
    }

    private void CreateVisual()
    {
        _visual = new GameObject("IslandBackgroundRuntime");
        _visual.transform.SetParent(transform, false);
        _visual.layer = LayerMask.NameToLayer("Ignore Raycast");
        _mesh = new Mesh { name = "Boat island silhouette" };
        _visual.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = _visual.AddComponent<MeshRenderer>();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _properties = new MaterialPropertyBlock();
    }

    private void BuildSilhouette(int seed)
    {
        const int count = 65;
        var vertices = new Vector3[count * 2]; var uv = new Vector2[count * 2];
        var colors = new Color[count * 2]; var indices = new int[(count - 1) * 6];
        float phase = (uint)seed % 997 / 997f * Mathf.PI * 2;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1), x = t - .5f;
            float envelope = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .65f);
            float crest = envelope * (.55f + .22f * Mathf.Sin(t * 7 + phase) + .12f * Mathf.Sin(t * 19 + phase * 2));
            // This is distant above-water scenery, not the physical coast. An
            // extruded underwater slab remains visible through transparent water.
            vertices[i] = new Vector3(x, Mathf.Max(0, crest - .15f), 0);
            vertices[count + i] = new Vector3(x, 0, 0);
            uv[i] = new Vector2(t, 1); uv[count + i] = new Vector2(t, 0);
            colors[i] = colors[count + i] = Color.white;
            if (i == count - 1) continue;
            int at = i * 6;
            indices[at] = i; indices[at + 1] = i + 1; indices[at + 2] = count + i + 1;
            indices[at + 3] = i; indices[at + 4] = count + i + 1; indices[at + 5] = count + i;
        }
        _mesh.Clear(); _mesh.vertices = vertices; _mesh.uv = uv; _mesh.colors = colors; _mesh.triangles = indices;
        _mesh.RecalculateBounds(); _mesh.RecalculateNormals();
    }

    private void Hide() { _projection.Reset(); _meshId = 0; if (_renderer != null) _renderer.enabled = false; }
    private void OnDisable() { Hide(); _nextResolve = 0; }
    private void OnDestroy()
    {
        if (_mesh != null) { if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh); }
        if (_visual != null) { if (Application.isPlaying) Destroy(_visual); else DestroyImmediate(_visual); }
    }
}
