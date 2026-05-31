using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldMapPOISource : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapTopographyDebugSource topographySource;
    [SerializeField] private WorldMapPOIGenerationSettings settings;
    [SerializeField] private WorldMapPOICatalog catalog;

    [Header("Generation")]
    [SerializeField] private bool generateOnAwake = true;
    [SerializeField] private int seedSalt = 420691;

    [Header("Runtime State")]
    [SerializeField] private bool hasGenerated;

    private WorldMapPOIGenerationSettings _runtimeDefaultSettings;

    public WorldMapPOILayer Layer { get; private set; }
    public WorldMapPOICatalog Catalog => catalog;
    public bool HasLayer => Layer != null && Layer.IsValid;

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();

        if (generateOnAwake)
            EnsureGenerated();
    }

    public void EnsureGenerated()
    {
        if (HasLayer)
            return;

        Generate();
    }

    [ContextMenu("Generate POIs")]
    public void Generate()
    {
        AutoWire();

        if (topographySource == null)
        {
            Debug.LogWarning("[WorldMapPOISource] Missing WorldMapTopographyDebugSource.", this);
            return;
        }

        if (catalog == null || catalog.Count <= 0)
        {
            Debug.LogWarning("[WorldMapPOISource] Missing WorldMapPOICatalog or catalog has no POI definitions.", this);
            return;
        }

        if (topographySource.Field == null || !topographySource.Field.IsValid)
            topographySource.LoadOrGenerate();

        WorldMapTopographyField field = topographySource.Field;
        WorldMapTopographySettings topoSettings = topographySource.Settings;

        if (field == null || !field.IsValid || topoSettings == null)
        {
            Debug.LogWarning("[WorldMapPOISource] Cannot generate POIs without valid topography.", this);
            return;
        }

        WorldMapPOIGenerationSettings resolvedSettings = ResolveSettings();

        int seed = unchecked(field.Seed ^ seedSalt);

        Layer = WorldMapPOIGenerator.Generate(
            seed,
            field,
            topoSettings,
            topographySource.EffectiveSeaLevel01,
            resolvedSettings,
            catalog
        );

        hasGenerated = HasLayer;

        int count = HasLayer && Layer.pois != null ? Layer.pois.Count : 0;

        if (resolvedSettings != null && resolvedSettings.logGenerationSummary)
        {
            Debug.Log(
                $"[WorldMapPOISource] Generated POI layer. Seed={seed}, POIs={count}, Definitions={catalog.Count}",
                this
            );
        }
    }

    [ContextMenu("Clear POIs")]
    public void Clear()
    {
        Layer = null;
        hasGenerated = false;
    }

    public WorldMapPOIDef GetDefinition(WorldMapPOIInstance instance)
    {
        if (instance == null || catalog == null)
            return null;

        return catalog.GetById(instance.poiDefId);
    }

    private void AutoWire()
    {
        if (topographySource == null)
            topographySource = FindAnyObjectByType<WorldMapTopographyDebugSource>(FindObjectsInactive.Include);
    }

    private WorldMapPOIGenerationSettings ResolveSettings()
    {
        if (settings != null)
            return settings;

        if (_runtimeDefaultSettings == null)
        {
            _runtimeDefaultSettings = ScriptableObject.CreateInstance<WorldMapPOIGenerationSettings>();
            _runtimeDefaultSettings.name = "Runtime Default POI Generation Settings";
        }

        return _runtimeDefaultSettings;
    }

    private void OnDestroy()
    {
        if (_runtimeDefaultSettings != null)
        {
            if (Application.isPlaying)
                Destroy(_runtimeDefaultSettings);
            else
                DestroyImmediate(_runtimeDefaultSettings);
        }
    }
}
