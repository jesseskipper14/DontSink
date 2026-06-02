using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class WorldMapTopographyDebugSource : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapGraphGenerator graphGenerator;
    [SerializeField] private WorldMapTopographySettings settings;

    [Header("Bake Cache")]
    [Tooltip("If assigned and valid, this baked asset is used instead of regenerating every Play.")]
    [SerializeField] private WorldMapTopographyBakeAsset bakedAsset;

    [Tooltip("Use baked asset when it matches the current seed/settings.")]
    [SerializeField] private bool preferBakedAsset = true;

    [Tooltip("If true, falls back to runtime generation when no matching baked asset exists.")]
    [SerializeField] private bool generateIfBakeMissingOrStale = true;

    [Header("Runtime Cache")]
    [Tooltip("If true, this source first tries to use the persistent runtime world-map cache. This keeps NodeScene and BoatScene in sync.")]
    [SerializeField] private bool preferRuntimeCache = true;

    [Tooltip("If true, any loaded/generated/restored topography is published to the persistent runtime cache.")]
    [SerializeField] private bool publishToRuntimeCache = true;

    [Header("Bake Texture Cache")]
    [Tooltip("Bake the player-facing base texture into the asset for fast map display. Recommended ON.")]
    [SerializeField] private bool bakeBaseTexture = true;

    [Tooltip("Bake contour overlay texture into the asset. Usually OFF; it can be rebuilt lazily.")]
    [SerializeField] private bool bakeContourTexture = false;

    [Tooltip("Bake legacy debug texture with contours into the asset. Usually OFF; it is redundant and large.")]
    [SerializeField] private bool bakeDebugTexture = false;

    [Tooltip("Bake classification overlay texture into the asset. Usually OFF; it can be rebuilt lazily.")]
    [SerializeField] private bool bakeClassificationTexture = false;

    [Header("Generation")]
    [SerializeField] private bool generateOnAwake = true;
    [SerializeField] private int fallbackSeed = 12345;

    [Header("Biomes")]
    [SerializeField] private WorldMapBiomeCatalog biomeCatalog;

    [Header("Runtime Textures")]
    [SerializeField] private Texture2D baseTexture;
    [SerializeField] private Texture2D contourTexture;
    [SerializeField] private Texture2D debugTexture;
    [SerializeField] private Texture2D classificationTexture;
    [SerializeField] private Texture2D biomeTexture;

    private bool _ownsBaseTexture;
    private bool _ownsContourTexture;
    private bool _ownsDebugTexture;
    private bool _ownsClassificationTexture;
    private bool _ownsBiomeTexture;

    public WorldMapTopographySettings Settings => settings;
    public WorldMapTopographyField Field { get; private set; }

    public Texture2D BaseTexture => baseTexture != null ? baseTexture : debugTexture;
    public Texture2D ContourTexture => contourTexture;
    public Texture2D DebugTexture => debugTexture;
    public Texture2D ClassificationTexture => classificationTexture;

    public WorldMapBiomeLayer BiomeLayer { get; private set; }
    public Texture2D BiomeTexture => biomeTexture;

    private bool CanBuildDerivedTextures => Field != null && Field.IsValid && settings != null;
    private bool CanBuildBiomeTexture => CanBuildDerivedTextures && biomeCatalog != null && settings.generateBiomeLayer;

    public bool HasBiomeTexture =>
        (biomeTexture != null && BiomeLayer != null && BiomeLayer.IsValid) || CanBuildBiomeTexture;

    public bool HasBaseTexture => BaseTexture != null && Field != null && Field.IsValid;
    public bool HasContourTexture => (contourTexture != null || CanBuildDerivedTextures) && Field != null && Field.IsValid;
    public bool HasDebugTexture => (debugTexture != null || CanBuildDerivedTextures) && Field != null && Field.IsValid;
    public bool HasClassificationTexture => (classificationTexture != null || CanBuildDerivedTextures) && Field != null && Field.IsValid;

    // Backward compatibility for older cartridge checks.
    public bool HasTexture => HasBaseTexture;

    public float EffectiveSeaLevel01 { get; private set; }
    public WorldMapTopographyStats Stats { get; private set; }

    public WorldMapTopographyBakeAsset BakedAsset => bakedAsset;

    private void Reset()
    {
        graphGenerator = FindAnyObjectByType<WorldMapGraphGenerator>();
    }

    private void Awake()
    {
        if (graphGenerator == null)
            graphGenerator = FindAnyObjectByType<WorldMapGraphGenerator>();

        if (generateOnAwake)
        {
            if (preferRuntimeCache && TryUseRuntimeCache("Awake"))
                return;

            if (!WorldMapSaveRestorer.TryRestoreTopographyToSource(this))
                LoadOrGenerate();
        }
    }

    [ContextMenu("Load Or Generate")]
    public void LoadOrGenerate()
    {
        if (preferRuntimeCache && TryUseRuntimeCache("LoadOrGenerate"))
            return;

        int seed = GetSeed();

        if (preferBakedAsset && TryLoadBaked(seed))
            return;

        if (generateIfBakeMissingOrStale)
        {
            GenerateRuntimeOnly();
            return;
        }

        Debug.LogWarning(
            "[WorldMapTopographyDebugSource] No valid baked topography and runtime generation is disabled.",
            this
        );
    }

    // Backward-compatible wrapper for older callers/cartridge button.
    [ContextMenu("Generate Topography Debug Texture")]
    public void Generate()
    {
        GenerateRuntimeOnly();
    }

    [ContextMenu("Generate Runtime Only")]
    public void GenerateRuntimeOnly()
    {
        if (settings == null)
        {
            Debug.LogWarning("[WorldMapTopographyDebugSource] Missing topography settings.", this);
            return;
        }

        int seed = GetSeed();

        WorldMapTopographyField field = WorldMapTopographyGenerator.Generate(seed, settings);
        if (field == null || !field.IsValid)
        {
            Debug.LogError("[WorldMapTopographyDebugSource] Runtime generation failed: invalid field.", this);
            return;
        }

        WorldMapTopographySettings resolvedSettings =
            CreateResolvedSettings(field, out float effectiveSea, out WorldMapTopographyStats stats);

        Texture2D nextBase = null;
        Texture2D nextContour = null;
        Texture2D nextDebug = null;
        Texture2D nextClass = null;

        try
        {
            nextBase = WorldMapTopographyDebugTextureBuilder.BuildBaseTexture(field, resolvedSettings);
            nextContour = WorldMapTopographyDebugTextureBuilder.BuildContourTexture(field, resolvedSettings);
            nextDebug = WorldMapTopographyDebugTextureBuilder.BuildDebugTexture(field, resolvedSettings);
            nextClass = WorldMapTopographyClassificationTextureBuilder.BuildTexture(field, resolvedSettings);
        }
        finally
        {
            DestroyResolvedSettings(resolvedSettings);
        }

        if (nextBase == null)
        {
            Debug.LogError("[WorldMapTopographyDebugSource] Runtime generation failed: base texture was null.", this);

            DestroyTexture(nextContour);
            DestroyTexture(nextDebug);
            DestroyTexture(nextClass);
            return;
        }

        UseRuntimeGenerated(
            field,
            effectiveSea,
            stats,
            nextBase,
            nextContour,
            nextDebug,
            nextClass
        );

        Debug.Log(
            $"[WorldMapTopographyDebugSource] Generated runtime topography. " +
            $"Seed={seed}, Res={Field.Width}x{Field.Height}, " +
            $"Sea={EffectiveSeaLevel01:0.000}, Water={Stats.Water01:P0}, Land={Stats.Land01:P0}, " +
            $"Raw={Field.MinRaw:0.000}..{Field.MaxRaw:0.000}",
            this
        );
    }

#if UNITY_EDITOR
    [ContextMenu("Bake Topography Asset")]
    public void BakeTopographyAsset()
    {
        if (settings == null)
        {
            Debug.LogWarning("[WorldMapTopographyDebugSource] Missing topography settings.", this);
            return;
        }

        int seed = GetSeed();

        WorldMapTopographyField field = WorldMapTopographyGenerator.Generate(seed, settings);
        if (field == null || !field.IsValid)
        {
            Debug.LogError("[WorldMapTopographyDebugSource] Bake failed: generated field was invalid.", this);
            return;
        }

        WorldMapTopographySettings resolvedSettings =
            CreateResolvedSettings(field, out float effectiveSea, out WorldMapTopographyStats stats);

        Texture2D nextBase = null;
        Texture2D nextContour = null;
        Texture2D nextDebug = null;
        Texture2D nextClass = null;

        try
        {
            if (bakeBaseTexture)
                nextBase = WorldMapTopographyDebugTextureBuilder.BuildBaseTexture(field, resolvedSettings);

            if (bakeContourTexture)
                nextContour = WorldMapTopographyDebugTextureBuilder.BuildContourTexture(field, resolvedSettings);

            if (bakeDebugTexture)
                nextDebug = WorldMapTopographyDebugTextureBuilder.BuildDebugTexture(field, resolvedSettings);

            if (bakeClassificationTexture)
                nextClass = WorldMapTopographyClassificationTextureBuilder.BuildTexture(field, resolvedSettings);
        }
        finally
        {
            DestroyResolvedSettings(resolvedSettings);
        }

        if (bakeBaseTexture && nextBase == null)
        {
            Debug.LogError("[WorldMapTopographyDebugSource] Bake failed: base texture was requested but null.", this);

            DestroyTexture(nextContour);
            DestroyTexture(nextDebug);
            DestroyTexture(nextClass);
            return;
        }

        WorldMapTopographyBakeAsset asset = bakedAsset;

        if (asset == null)
        {
            string folder = "Assets/Data/WorldMap/Topography/Baked";
            EnsureFolder(folder);

            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/BakedTopography_{seed}.asset"
            );

            asset = ScriptableObject.CreateInstance<WorldMapTopographyBakeAsset>();
            AssetDatabase.CreateAsset(asset, path);
            bakedAsset = asset;
        }

        DestroyExistingTextureSubAssets(asset);

        if (nextBase != null)
        {
            nextBase.name = $"TopographyBaseTexture_{seed}";
            AssetDatabase.AddObjectToAsset(nextBase, asset);
        }

        if (nextContour != null)
        {
            nextContour.name = $"TopographyContourTexture_{seed}";
            AssetDatabase.AddObjectToAsset(nextContour, asset);
        }

        if (nextDebug != null)
        {
            nextDebug.name = $"TopographyDebugTexture_{seed}";
            AssetDatabase.AddObjectToAsset(nextDebug, asset);
        }

        if (nextClass != null)
        {
            nextClass.name = $"TopographyClassificationTexture_{seed}";
            AssetDatabase.AddObjectToAsset(nextClass, asset);
        }

        asset.Store(
            seed,
            settings,
            field,
            effectiveSea,
            stats,
            nextBase,
            nextContour,
            nextDebug,
            nextClass
        );

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(this);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UseBaked(asset);

        Debug.Log(
            $"[WorldMapTopographyDebugSource] Baked topography asset '{asset.name}'. " +
            $"Seed={seed}, Res={field.Width}x{field.Height}, " +
            $"Sea={EffectiveSeaLevel01:0.000}, Water={Stats.Water01:P0}, Land={Stats.Land01:P0}, " +
            $"BaseTexture={(nextBase != null ? nextBase.width + "x" + nextBase.height : "not baked")}, " +
            $"ContourBaked={(nextContour != null)}, DebugBaked={(nextDebug != null)}, ClassBaked={(nextClass != null)}",
            asset
        );
    }

    private static void DestroyExistingTextureSubAssets(WorldMapTopographyBakeAsset asset)
    {
        if (asset == null)
            return;

        // BaseTexture may fall back to DebugTexture on old assets, so dedupe instance IDs.
        var seen = new HashSet<int>();

        DestroySubAssetIfPresent(asset.BaseTexture, seen);
        DestroySubAssetIfPresent(asset.ContourTexture, seen);
        DestroySubAssetIfPresent(asset.DebugTexture, seen);
        DestroySubAssetIfPresent(asset.ClassificationTexture, seen);
    }

    private static void DestroySubAssetIfPresent(UnityEngine.Object obj, HashSet<int> seen)
    {
        if (obj == null)
            return;

        int id = obj.GetInstanceID();
        if (!seen.Add(id))
            return;

        if (!AssetDatabase.Contains(obj))
            return;

        UnityEngine.Object.DestroyImmediate(obj, allowDestroyingAssets: true);
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            return;

        string current = "Assets";

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
#endif

    public bool TryUseRuntimeCache(string reason = "")
    {
        WorldMapRuntimeCache cache = WorldMapRuntimeCache.I;

        if (cache == null || !cache.HasTopography)
            return false;

        DestroyOwnedRuntimeTextures();
        DestroyBiomeTexture();

        Field = cache.Field;
        EffectiveSeaLevel01 = cache.EffectiveSeaLevel01;
        Stats = cache.Stats;

        baseTexture = cache.BaseTexture;
        contourTexture = cache.ContourTexture;
        debugTexture = cache.DebugTexture;
        classificationTexture = cache.ClassificationTexture;

        BiomeLayer = cache.BiomeLayer;
        biomeTexture = cache.BiomeTexture;

        _ownsBaseTexture = false;
        _ownsContourTexture = false;
        _ownsDebugTexture = false;
        _ownsClassificationTexture = false;
        _ownsBiomeTexture = false;

        Debug.Log(
            $"[WorldMapTopographyDebugSource] Using persistent runtime cache. " +
            $"Reason='{reason}', CacheSource='{cache.SourceReason}', " +
            $"Seed={Field.Seed}, Res={Field.Width}x{Field.Height}, Sea={EffectiveSeaLevel01:0.000}",
            this
        );

        return true;
    }

    private void PushCurrentToRuntimeCache(string reason)
    {
        if (!publishToRuntimeCache)
            return;

        if (Field == null || !Field.IsValid)
            return;

        WorldMapRuntimeCache cache = WorldMapRuntimeCache.Ensure();

        cache.StoreTopography(
            Field,
            EffectiveSeaLevel01,
            Stats,
            baseTexture,
            _ownsBaseTexture,
            contourTexture,
            _ownsContourTexture,
            debugTexture,
            _ownsDebugTexture,
            classificationTexture,
            _ownsClassificationTexture,
            BiomeLayer,
            biomeTexture,
            _ownsBiomeTexture,
            reason
        );

        // Ownership transfers to the persistent cache. This prevents scene unload from destroying
        // textures that BoatScene/NodeScene should continue sharing. Unity made object lifetime a
        // scavenger hunt, because apparently that was necessary.
        _ownsBaseTexture = false;
        _ownsContourTexture = false;
        _ownsDebugTexture = false;
        _ownsClassificationTexture = false;
        _ownsBiomeTexture = false;
    }

    private bool TryLoadBaked(int seed)
    {
        if (bakedAsset == null)
            return false;

        if (!bakedAsset.Matches(seed, settings))
        {
            Debug.Log(
                "[WorldMapTopographyDebugSource] Baked topography is missing or stale. " +
                "Regenerate/bake if you want to avoid runtime generation.",
                this
            );

            return false;
        }

        UseBaked(bakedAsset);

        Debug.Log(
            $"[WorldMapTopographyDebugSource] Loaded baked topography. " +
            $"Seed={seed}, Res={Field.Width}x{Field.Height}, " +
            $"Sea={EffectiveSeaLevel01:0.000}, Water={Stats.Water01:P0}, Land={Stats.Land01:P0}",
            bakedAsset
        );

        return true;
    }

    public bool TryRestoreFromSnapshot(WorldMapTopographySaveSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.HasRawHeightData)
            return false;

        if (settings == null)
        {
            Debug.LogWarning("[WorldMapTopographyDebugSource] Cannot restore topography snapshot: missing settings.", this);
            return false;
        }

        float[] heights = snapshot.CopyHeight01();
        if (heights == null || heights.Length != snapshot.width * snapshot.height)
        {
            Debug.LogWarning("[WorldMapTopographyDebugSource] Cannot restore topography snapshot: height payload missing or invalid.", this);
            return false;
        }

        var field = new WorldMapTopographyField(
            snapshot.seed,
            snapshot.width,
            snapshot.height,
            snapshot.ToWorldBounds(),
            heights,
            snapshot.minRaw,
            snapshot.maxRaw
        );

        if (!field.IsValid)
        {
            Debug.LogWarning("[WorldMapTopographyDebugSource] Cannot restore topography snapshot: invalid restored field.", this);
            return false;
        }

        WorldMapTopographySettings resolvedSettings =
            CreateSettingsForRestoredSnapshot(snapshot);

        Texture2D nextBase = null;

        try
        {
            nextBase = WorldMapTopographyDebugTextureBuilder.BuildBaseTexture(field, resolvedSettings);
        }
        finally
        {
            DestroyResolvedSettings(resolvedSettings);
        }

        if (nextBase == null)
            return false;

        WorldMapTopographyStats restoredStats;
        WorldMapTopographySettings analysisSettings = CreateSettingsForRestoredSnapshot(snapshot);

        try
        {
            restoredStats = WorldMapTopographyAnalysis.Analyze(field, analysisSettings);
        }
        finally
        {
            DestroyResolvedSettings(analysisSettings);
        }

        UseRuntimeGenerated(
            field,
            snapshot.effectiveSeaLevel01,
            restoredStats,
            nextBase,
            null,
            null,
            null
        );

        Debug.Log(
            $"[WorldMapTopographyDebugSource] Restored persisted topography. " +
            $"Seed={field.Seed}, Res={field.Width}x{field.Height}, Sea={EffectiveSeaLevel01:0.000}",
            this
        );

        return true;
    }

    private WorldMapTopographySettings CreateSettingsForRestoredSnapshot(WorldMapTopographySaveSnapshot snapshot)
    {
        WorldMapTopographySettings resolved = UnityEngine.Object.Instantiate(settings);

        resolved.seaLevel01 = snapshot != null ? snapshot.effectiveSeaLevel01 : settings.seaLevel01;
        resolved.autoAdjustSeaLevelToTargetWater = false;

        return resolved;
    }

    private void UseBaked(WorldMapTopographyBakeAsset asset)
    {
        if (asset == null)
            return;

        DestroyOwnedRuntimeTextures();
        DestroyBiomeTexture();

        Field = asset.ToField();
        EffectiveSeaLevel01 = asset.effectiveSeaLevel01;
        Stats = asset.stats;

        baseTexture = asset.BaseTexture;
        contourTexture = asset.ContourTexture;
        debugTexture = asset.DebugTexture;
        classificationTexture = asset.ClassificationTexture;

        _ownsBaseTexture = false;
        _ownsContourTexture = false;
        _ownsDebugTexture = false;
        _ownsClassificationTexture = false;

        // Keep normal map display fast even when the bake asset only stores packed height data.
        EnsureBaseTexture();

        PushCurrentToRuntimeCache("UseBaked");
    }

    private void UseRuntimeGenerated(
        WorldMapTopographyField field,
        float effectiveSeaLevel,
        WorldMapTopographyStats stats,
        Texture2D nextBase,
        Texture2D nextContour,
        Texture2D nextDebug,
        Texture2D nextClassification)
    {
        DestroyOwnedRuntimeTextures();

        Field = field;
        EffectiveSeaLevel01 = effectiveSeaLevel;
        Stats = stats;

        baseTexture = nextBase;
        contourTexture = nextContour;
        debugTexture = nextDebug;
        classificationTexture = nextClassification;

        _ownsBaseTexture = nextBase != null;
        _ownsContourTexture = nextContour != null;
        _ownsDebugTexture = nextDebug != null;
        _ownsClassificationTexture = nextClassification != null;

        DestroyBiomeTexture();

        PushCurrentToRuntimeCache("UseRuntimeGenerated");
    }

    public bool EnsureBaseTexture()
    {
        if (BaseTexture != null)
            return true;

        if (!CanBuildDerivedTextures)
            return false;

        WorldMapTopographySettings resolved = CreateSettingsForCurrentInterpretation();

        try
        {
            Texture2D built = WorldMapTopographyDebugTextureBuilder.BuildBaseTexture(Field, resolved);
            if (built == null)
                return false;

            if (_ownsBaseTexture)
                DestroyTexture(baseTexture);

            baseTexture = built;
            _ownsBaseTexture = true;
            PushCurrentToRuntimeCache("EnsureBaseTexture");
            return true;
        }
        finally
        {
            DestroyResolvedSettings(resolved);
        }
    }

    public bool EnsureContourTexture()
    {
        if (contourTexture != null)
            return true;

        if (!CanBuildDerivedTextures)
            return false;

        WorldMapTopographySettings resolved = CreateSettingsForCurrentInterpretation();

        try
        {
            Texture2D built = WorldMapTopographyDebugTextureBuilder.BuildContourTexture(Field, resolved);
            if (built == null)
                return false;

            if (_ownsContourTexture)
                DestroyTexture(contourTexture);

            contourTexture = built;
            _ownsContourTexture = true;
            PushCurrentToRuntimeCache("EnsureContourTexture");
            return true;
        }
        finally
        {
            DestroyResolvedSettings(resolved);
        }
    }

    public bool EnsureDebugTexture()
    {
        if (debugTexture != null)
            return true;

        if (!CanBuildDerivedTextures)
            return false;

        WorldMapTopographySettings resolved = CreateSettingsForCurrentInterpretation();

        try
        {
            Texture2D built = WorldMapTopographyDebugTextureBuilder.BuildDebugTexture(Field, resolved);
            if (built == null)
                return false;

            if (_ownsDebugTexture)
                DestroyTexture(debugTexture);

            debugTexture = built;
            _ownsDebugTexture = true;
            PushCurrentToRuntimeCache("EnsureDebugTexture");
            return true;
        }
        finally
        {
            DestroyResolvedSettings(resolved);
        }
    }

    public bool EnsureClassificationTexture()
    {
        if (classificationTexture != null)
            return true;

        if (!CanBuildDerivedTextures)
            return false;

        WorldMapTopographySettings resolved = CreateSettingsForCurrentInterpretation();

        try
        {
            Texture2D built = WorldMapTopographyClassificationTextureBuilder.BuildTexture(Field, resolved);
            if (built == null)
                return false;

            if (_ownsClassificationTexture)
                DestroyTexture(classificationTexture);

            classificationTexture = built;
            _ownsClassificationTexture = true;
            PushCurrentToRuntimeCache("EnsureClassificationTexture");
            return true;
        }
        finally
        {
            DestroyResolvedSettings(resolved);
        }
    }

    public bool EnsureBiomeTexture()
    {
        if (biomeTexture != null && BiomeLayer != null && BiomeLayer.IsValid)
            return true;

        if (!CanBuildBiomeTexture)
            return false;

        RebuildBiomeLayerAndTexture();

        if (biomeTexture != null && BiomeLayer != null && BiomeLayer.IsValid)
            PushCurrentToRuntimeCache("EnsureBiomeTexture");

        return biomeTexture != null && BiomeLayer != null && BiomeLayer.IsValid;
    }

    private void RebuildBiomeLayerAndTexture()
    {
        DestroyBiomeTexture();

        BiomeLayer = null;

        if (Field == null || !Field.IsValid)
            return;

        if (settings == null || biomeCatalog == null || !settings.generateBiomeLayer)
            return;

        WorldMapTopographySettings resolved = CreateSettingsForCurrentInterpretation();

        try
        {
            BiomeLayer = WorldMapBiomeGenerator.Generate(Field, resolved, biomeCatalog);
            biomeTexture = WorldMapBiomeTextureBuilder.BuildTexture(BiomeLayer, resolved);
            _ownsBiomeTexture = biomeTexture != null;
        }
        finally
        {
            DestroyResolvedSettings(resolved);
        }
    }

    private WorldMapTopographySettings CreateSettingsForCurrentInterpretation()
    {
        WorldMapTopographySettings resolved =
            UnityEngine.Object.Instantiate(settings);

        resolved.seaLevel01 = EffectiveSeaLevel01;
        resolved.autoAdjustSeaLevelToTargetWater = false;

        return resolved;
    }

    private void DestroyBiomeTexture()
    {
        if (biomeTexture != null && _ownsBiomeTexture)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(biomeTexture);
            else
                UnityEngine.Object.DestroyImmediate(biomeTexture);
        }

        biomeTexture = null;
        _ownsBiomeTexture = false;
    }

    private WorldMapTopographySettings CreateResolvedSettings(
        WorldMapTopographyField field,
        out float effectiveSeaLevel,
        out WorldMapTopographyStats stats)
    {
        WorldMapTopographySettings resolved =
            UnityEngine.Object.Instantiate(settings);

        if (settings.autoAdjustSeaLevelToTargetWater)
        {
            resolved.seaLevel01 =
                WorldMapTopographyAnalysis.FindSeaLevelForTargetWaterPercent(
                    field,
                    settings.targetWaterPercent,
                    settings.seaLevelSolveIterations
                );
        }

        effectiveSeaLevel = resolved.seaLevel01;
        stats = WorldMapTopographyAnalysis.Analyze(field, resolved);

        return resolved;
    }

    private static void DestroyResolvedSettings(WorldMapTopographySettings resolved)
    {
        if (resolved == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(resolved);
        else
            UnityEngine.Object.DestroyImmediate(resolved);
    }

    private int GetSeed()
    {
        return graphGenerator != null ? graphGenerator.seed : fallbackSeed;
    }

    private void DestroyOwnedRuntimeTextures()
    {
        if (_ownsBaseTexture)
            DestroyTexture(baseTexture);

        if (_ownsContourTexture)
            DestroyTexture(contourTexture);

        if (_ownsDebugTexture)
            DestroyTexture(debugTexture);

        if (_ownsClassificationTexture)
            DestroyTexture(classificationTexture);

        baseTexture = null;
        contourTexture = null;
        debugTexture = null;
        classificationTexture = null;

        _ownsBaseTexture = false;
        _ownsContourTexture = false;
        _ownsDebugTexture = false;
        _ownsClassificationTexture = false;
    }

    private static void DestroyTexture(Texture2D tex)
    {
        if (tex == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(tex);
        else
            UnityEngine.Object.DestroyImmediate(tex);
    }

    private void OnDestroy()
    {
        DestroyOwnedRuntimeTextures();
        DestroyBiomeTexture();

        Field = null;
        EffectiveSeaLevel01 = 0f;
        Stats = default;
    }
}