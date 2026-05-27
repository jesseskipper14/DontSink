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

    private bool _ownsRuntimeTextures;

    public WorldMapTopographySettings Settings => settings;
    public WorldMapTopographyField Field { get; private set; }

    public Texture2D BaseTexture => baseTexture != null ? baseTexture : debugTexture;
    public Texture2D ContourTexture => contourTexture;
    public Texture2D DebugTexture => debugTexture;
    public Texture2D ClassificationTexture => classificationTexture;

    public WorldMapBiomeLayer BiomeLayer { get; private set; }
    public Texture2D BiomeTexture => biomeTexture;
    public bool HasBiomeTexture => biomeTexture != null && BiomeLayer != null && BiomeLayer.IsValid;

    public bool HasBaseTexture => BaseTexture != null && Field != null && Field.IsValid;
    public bool HasContourTexture => contourTexture != null && Field != null && Field.IsValid;
    public bool HasDebugTexture => debugTexture != null && Field != null && Field.IsValid;
    public bool HasClassificationTexture => classificationTexture != null && Field != null && Field.IsValid;

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
            LoadOrGenerate();
    }

    [ContextMenu("Load Or Generate")]
    public void LoadOrGenerate()
    {
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
            Debug.LogError("[WorldMapTopographyDebugSource] Bake failed: base texture was null.", this);

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

        nextBase.name = $"TopographyBaseTexture_{seed}";
        AssetDatabase.AddObjectToAsset(nextBase, asset);

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
            $"Texture={nextBase.width}x{nextBase.height}",
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

    private void UseBaked(WorldMapTopographyBakeAsset asset)
    {
        if (asset == null)
            return;

        DestroyOwnedRuntimeTextures();

        Field = asset.ToField();
        EffectiveSeaLevel01 = asset.effectiveSeaLevel01;
        Stats = asset.stats;

        baseTexture = asset.BaseTexture;
        contourTexture = asset.ContourTexture;
        debugTexture = asset.DebugTexture;
        classificationTexture = asset.ClassificationTexture;

        _ownsRuntimeTextures = false;

        RebuildBiomeLayerAndTexture();
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

        _ownsRuntimeTextures =
            nextBase != null ||
            nextContour != null ||
            nextDebug != null ||
            nextClassification != null;

        RebuildBiomeLayerAndTexture();
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
        if (biomeTexture != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(biomeTexture);
            else
                UnityEngine.Object.DestroyImmediate(biomeTexture);
        }

        biomeTexture = null;
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
        if (!_ownsRuntimeTextures)
            return;

        DestroyTexture(baseTexture);
        DestroyTexture(contourTexture);
        DestroyTexture(debugTexture);
        DestroyTexture(classificationTexture);

        baseTexture = null;
        contourTexture = null;
        debugTexture = null;
        classificationTexture = null;

        _ownsRuntimeTextures = false;
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

        Field = null;
        EffectiveSeaLevel01 = 0f;
        Stats = default;
    }
}