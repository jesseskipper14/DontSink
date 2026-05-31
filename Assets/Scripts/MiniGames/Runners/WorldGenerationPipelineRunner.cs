using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Obsolete("Deprecated. Use WorldMapTopographyGenerator / WorldMapTopographyDebugSource instead. This WorldGen Lab pipeline is shelved and should not be used.", false)]
[DisallowMultipleComponent]
public sealed class WorldGenerationPipelineRunner : MonoBehaviour
{
    #region Inspector

    [Header("Refs")]
    [SerializeField] private WorldMapTopographySettings settings;

    [Header("Seed")]
    [SerializeField] private int fallbackSeed = 12345;

    [Header("WorldGen Bounds and Grid")]
    [Tooltip("If true, pulls worldSize from WorldMapTopographySettings. If false, uses the explicit WorldGen worldSize below.")]
    [SerializeField] private bool syncWorldSizeFromTopographySettings = false;

    [Tooltip("Explicit generated world size in graph/world units. This controls the actual generated bounds, not just preview display.")]
    [SerializeField] private Vector2 worldSize = new Vector2(480f, 300f);

    [Tooltip("Generated height grid width. This does not need to be square.")]
    [Min(16)]
    [SerializeField] private int gridWidth = 768;

    [Tooltip("Generated height grid height. This does not need to be square.")]
    [Min(16)]
    [SerializeField] private int gridHeight = 480;

    [Header("Generation")]
    [Tooltip("Rows processed before yielding back to Unity. Lower = smoother editor responsiveness, higher = faster total generation.")]
    [Range(1, 64)]
    [SerializeField] private int rowsPerYield = 8;

    [Header("Preview")]
    [SerializeField] private bool previewDrawContours = true;

    [SerializeField, HideInInspector]
    private bool previewDefaultsInitialized;

    [Header("Preview Contours")]
    [SerializeField] private bool previewOverrideContourSettings = true;

    [Tooltip("Higher = more contour lines / smaller height steps.")]
    [Range(8, 240)]
    [SerializeField] private int previewContourCount = 120;

    [Tooltip("Lower = thinner contour lines. Use lower values when contour count is high.")]
    [Range(0.0005f, 0.05f)]
    [SerializeField] private float previewContourThickness = 0.0045f;

    [Range(1, 12)]
    [SerializeField] private int previewMajorContourEvery = 5;

    [Header("Base Ocean")]
    [Tooltip("Very smooth ocean floor scale. Lower = broader ocean forms.")]
    [Min(0.0001f)]
    [SerializeField] private float baseOceanNoiseScale = 0.0025f;

    [Tooltip("How much secondary low-frequency detail is blended into the base ocean.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseOceanSecondaryBlend = 0.12f;

    [Tooltip("Shape strength that gently deepens map edges.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseOceanEdgeFalloffStrength = 0.20f;

    [Tooltip("Final base-ocean contrast. Keep this modest; ocean floor should stay smooth.")]
    [Range(0.25f, 3f)]
    [SerializeField] private float baseOceanContrast = 0.65f;

    [Header("Base Ocean Height Band")]
    [Tooltip("Lowest normalized height used by the smooth base ocean layer.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseOceanMinHeight01 = 0.08f;

    [Tooltip("Highest normalized height used by the smooth base ocean layer. This is clamped below sea level.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseOceanMaxHeight01 = 0.42f;

    [Tooltip("How far below sea level the highest base ocean terrain should remain.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float baseOceanSeaClearance01 = 0.08f;

    [Header("Ocean Macro")]
    [Tooltip("How far below sea level ocean-macro-only terrain should remain. Prevents this layer from secretly creating islands.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float oceanMacroSeaClearance01 = 0.035f;

    [Tooltip("Clamp negative/positive ocean macro deltas before composition. Keeps this layer subtle.")]
    [Range(0.005f, 0.25f)]
    [SerializeField] private float oceanMacroDeltaClamp = 0.075f;

    [Tooltip("Broad low-frequency bathymetry variation. Lower = larger, smoother regions.")]
    [Min(0.0001f)]
    [SerializeField] private float oceanMacroNoiseScale = 0.0032f;

    [Tooltip("Secondary macro noise frequency multiplier. Keep this low to avoid noisy seafloor garbage.")]
    [Range(1.01f, 4f)]
    [SerializeField] private float oceanMacroSecondaryScaleMultiplier = 1.75f;

    [Tooltip("How much secondary macro noise contributes. Higher = more variation, but too high gets crunchy.")]
    [Range(0f, 1f)]
    [SerializeField] private float oceanMacroSecondaryBlend = 0.18f;

    [Tooltip("Overall strength of broad macro bathymetry variation.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float oceanMacroStrength = 0.045f;

    [Tooltip("Raises or lowers the ocean macro layer before clamping. Usually leave near 0.")]
    [Range(-0.15f, 0.15f)]
    [SerializeField] private float oceanMacroBias = 0f;

    [Header("Ocean Macro Directional Bands")]
    [Tooltip("Subtle long ridge/trough banding strength. Keep low; this is texture, not knife wounds.")]
    [Range(0f, 0.15f)]
    [SerializeField] private float oceanMacroDirectionalStrength = 0.018f;

    [Tooltip("How many broad directional bands cross the map.")]
    [Range(0.1f, 12f)]
    [SerializeField] private float oceanMacroDirectionalFrequency = 2.4f;

    [Tooltip("Warps directional bands so they do not look ruler-straight.")]
    [Range(0f, 2f)]
    [SerializeField] private float oceanMacroDirectionalWarpStrength = 0.45f;

    [Tooltip("Noise scale used to warp directional bands.")]
    [Min(0.0001f)]
    [SerializeField] private float oceanMacroDirectionalWarpNoiseScale = 0.0055f;

    [Header("Runtime State")]
    [SerializeField] private Texture2D previewTexture;

    #endregion

    #region Runtime State

    private readonly List<string> _log = new();
    private Coroutine _runningRoutine;

    public WorldMapTopographySettings Settings => settings;
    public WorldGenerationWorkingSet WorkingSet { get; private set; }
    public WorldGenerationProgress Progress { get; } = new();
    public Texture2D PreviewTexture => previewTexture;
    public IReadOnlyList<string> Log => _log;

    public bool HasWorkingSet => WorkingSet != null && WorkingSet.IsValid;
    public bool HasPreviewTexture => previewTexture != null;
    public bool PreviewDrawContours => previewDrawContours;

    public void SetPreviewDrawContours(bool value, bool rebuildPreview = true)
    {
        EnsurePreviewDefaults();

        if (previewDrawContours == value)
            return;

        previewDrawContours = value;

        if (rebuildPreview && HasWorkingSet)
            PreviewFinalHeight();
    }

    #endregion

    #region Unity

    private void Reset()
    {
        settings = FindAnyObjectByType<WorldMapTopographyDebugSource>()?.Settings;
        EnsurePreviewDefaults();
    }

    private void OnValidate()
    {
        worldSize.x = Mathf.Max(1f, worldSize.x);
        worldSize.y = Mathf.Max(1f, worldSize.y);

        gridWidth = Mathf.Max(16, gridWidth);
        gridHeight = Mathf.Max(16, gridHeight);

        rowsPerYield = Mathf.Max(1, rowsPerYield);

        baseOceanNoiseScale = Mathf.Max(0.0001f, baseOceanNoiseScale);
        baseOceanMaxHeight01 = Mathf.Max(baseOceanMinHeight01 + 0.001f, baseOceanMaxHeight01);

        oceanMacroNoiseScale = Mathf.Max(0.0001f, oceanMacroNoiseScale);
        oceanMacroDirectionalWarpNoiseScale = Mathf.Max(0.0001f, oceanMacroDirectionalWarpNoiseScale);

        EnsurePreviewDefaults();
    }

    private void OnDestroy()
    {
        DestroyPreviewTexture();
    }

    private void EnsurePreviewDefaults()
    {
        if (previewDefaultsInitialized)
            return;

        previewDrawContours = true;
        previewDefaultsInitialized = true;
    }

    #endregion

    #region Public Commands

    public void RequestCancel()
    {
        Progress.RequestCancel();
        AddLog("Cancel requested.");
    }

    public void ClearLog()
    {
        _log.Clear();
    }

    [ContextMenu("Generate Base Ocean")]
    public void GenerateBaseOcean()
    {
        EnsurePreviewDefaults();
        StartRoutine(GenerateBaseOceanRoutine(buildPreviewAfter: true));
    }

    [ContextMenu("Generate Ocean Features")]
    public void GenerateOceanFeatures()
    {
        EnsurePreviewDefaults();
        StartRoutine(GenerateOceanFeaturesRoutine(composeAndPreviewAfter: true));
    }

    [ContextMenu("Compose Final Height")]
    public void ComposeFinalHeight()
    {
        EnsurePreviewDefaults();
        StartRoutine(ComposeFinalRoutine(markComplete: true));
    }

    [ContextMenu("Run Current V1 Pipeline")]
    public void RunCurrentV1Pipeline()
    {
        EnsurePreviewDefaults();
        StartRoutine(RunCurrentV1PipelineRoutine());
    }

    [ContextMenu("Preview Base Ocean")]
    public void PreviewBaseOcean()
    {
        EnsurePreviewDefaults();
        StartRoutine(BuildPreviewTextureRoutine(WorldGenerationPreviewLayer.BaseOcean, markComplete: true));
    }

    [ContextMenu("Preview Ocean Features")]
    public void PreviewOceanFeatures()
    {
        EnsurePreviewDefaults();
        StartRoutine(BuildPreviewTextureRoutine(WorldGenerationPreviewLayer.OceanFeatures, markComplete: true));
    }

    [ContextMenu("Preview Final Height")]
    public void PreviewFinalHeight()
    {
        EnsurePreviewDefaults();
        StartRoutine(BuildPreviewTextureRoutine(WorldGenerationPreviewLayer.FinalHeight, markComplete: true));
    }

    [ContextMenu("Build Preview Texture")]
    public void BuildPreviewTexture()
    {
        EnsurePreviewDefaults();
        PreviewFinalHeight();
    }

    #endregion

    #region Routine Plumbing

    private void StartRoutine(IEnumerator routine)
    {
        if (_runningRoutine != null)
        {
            StopCoroutine(_runningRoutine);
            _runningRoutine = null;
        }

        _runningRoutine = StartCoroutine(RunWrapped(routine));
    }

    private IEnumerator RunWrapped(IEnumerator routine)
    {
        yield return routine;
        _runningRoutine = null;
    }

    private IEnumerator RunCurrentV1PipelineRoutine()
    {
        yield return GenerateBaseOceanRoutine(buildPreviewAfter: false);

        if (IsCancelledOrFailed())
            yield break;

        yield return GenerateOceanFeaturesRoutine(composeAndPreviewAfter: false);

        if (IsCancelledOrFailed())
            yield break;

        yield return ComposeFinalRoutine(markComplete: false);

        if (IsCancelledOrFailed())
            yield break;

        yield return BuildPreviewTextureRoutine(WorldGenerationPreviewLayer.FinalHeight, markComplete: false);

        if (!IsCancelledOrFailed())
        {
            Progress.Complete("V1 layered pipeline complete.");
            AddLog("V1 pipeline complete.");
        }
    }

    private bool IsCancelledOrFailed()
    {
        return Progress.phase == WorldGenerationPhase.Cancelled ||
               Progress.phase == WorldGenerationPhase.Failed;
    }

    #endregion

    #region Base Ocean

    private IEnumerator GenerateBaseOceanRoutine(bool buildPreviewAfter)
    {
        if (settings == null)
        {
            Progress.Fail("Missing WorldMapTopographySettings.");
            AddLog("Failed: missing settings.");
            yield break;
        }

        int seed = GetSeed();

        Vector2 resolvedWorldSize = ResolveWorldSize();
        int width = Mathf.Max(16, gridWidth);
        int height = Mathf.Max(16, gridHeight);

        Rect bounds = new Rect(
            -resolvedWorldSize.x * 0.5f,
            -resolvedWorldSize.y * 0.5f,
            resolvedWorldSize.x,
            resolvedWorldSize.y
        );

        WorkingSet = new WorldGenerationWorkingSet(seed, width, height, bounds);

        Progress.Begin(WorldGenerationPhase.BaseOcean, "Generating smooth base ocean...");
        AddLog($"Generate Base Ocean: seed={seed}, grid={width}x{height}, bounds={bounds.width:0.#}x{bounds.height:0.#}");

        var rng = new System.Random(unchecked(seed ^ settings.seedSalt ^ 0x451AA771));
        Vector2 offsetA = RandomOffset(rng);
        Vector2 offsetB = RandomOffset(rng);

        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;

        int rows = Mathf.Max(1, rowsPerYield);

        for (int y = 0; y < height; y++)
        {
            if (Progress.cancelRequested)
            {
                Progress.Cancel();
                AddLog("Base ocean cancelled.");
                yield break;
            }

            float v = height <= 1 ? 0f : y / (float)(height - 1);

            for (int x = 0; x < width; x++)
            {
                float u = width <= 1 ? 0f : x / (float)(width - 1);

                Vector2 world = new Vector2(
                    Mathf.Lerp(bounds.xMin, bounds.xMax, u),
                    Mathf.Lerp(bounds.yMin, bounds.yMax, v)
                );

                float broad = Mathf.PerlinNoise(
                    (world.x + offsetA.x) * baseOceanNoiseScale,
                    (world.y + offsetA.y) * baseOceanNoiseScale
                );

                float secondary = Mathf.PerlinNoise(
                    (world.x + offsetB.x) * baseOceanNoiseScale * 2.15f,
                    (world.y + offsetB.y) * baseOceanNoiseScale * 2.15f
                );

                float h = Mathf.Lerp(broad, secondary, baseOceanSecondaryBlend);

                h = (h - 0.5f) * baseOceanContrast + 0.5f;
                h -= EvaluateEdgeFalloff(u, v) * baseOceanEdgeFalloffStrength;

                int index = WorkingSet.Index(x, y);
                WorkingSet.baseOceanHeight[index] = h;

                if (h < min) min = h;
                if (h > max) max = h;
            }

            if (y % rows == 0)
            {
                Progress.Update(
                    y / (float)Mathf.Max(1, height - 1),
                    $"Generating smooth base ocean... row {y}/{height}"
                );

                yield return null;
            }
        }

        NormalizeArray01(WorkingSet.baseOceanHeight, min, max);
        RemapBaseOceanBelowSeaLevel(WorkingSet.baseOceanHeight);

        System.Array.Clear(WorkingSet.oceanFeatureDelta, 0, WorkingSet.Length);
        System.Array.Clear(WorkingSet.islandMassDelta, 0, WorkingSet.Length);
        System.Array.Clear(WorkingSet.islandDetailDelta, 0, WorkingSet.Length);
        System.Array.Copy(WorkingSet.baseOceanHeight, WorkingSet.finalHeight01, WorkingSet.Length);

        Progress.Update(1f, "Base ocean complete.");
        AddLog($"Base ocean complete. Raw={min:0.000}..{max:0.000}");

        if (buildPreviewAfter)
        {
            yield return BuildPreviewTextureRoutine(WorldGenerationPreviewLayer.BaseOcean, markComplete: false);

            if (!IsCancelledOrFailed())
            {
                Progress.Complete("Base ocean preview complete.");
                AddLog("Base ocean preview complete.");
            }
        }
    }

    #endregion

    #region Ocean Macro

    private IEnumerator GenerateOceanFeaturesRoutine(bool composeAndPreviewAfter)
    {
        if (settings == null)
        {
            Progress.Fail("Missing WorldMapTopographySettings.");
            AddLog("Failed: missing settings.");
            yield break;
        }

        if (!HasWorkingSet)
        {
            Progress.Fail("Generate Base Ocean before Ocean Macro.");
            AddLog("Ocean macro failed: no working set. Generate Base Ocean first.");
            yield break;
        }

        Progress.Begin(WorldGenerationPhase.OceanFeatures, "Generating ocean macro...");
        AddLog("Generate Ocean Macro.");

        System.Array.Clear(WorkingSet.oceanFeatureDelta, 0, WorkingSet.Length);

        int seed = WorkingSet.seed;
        var rng = new System.Random(unchecked(seed ^ settings.seedSalt ^ 0x1A2B3C4D));

        Vector2 offsetA = RandomOffset(rng);
        Vector2 offsetB = RandomOffset(rng);
        Vector2 warpOffset = RandomOffset(rng);

        float directionAngle = RandRange(rng, 0f, Mathf.PI * 2f);
        Vector2 bandDir = new Vector2(Mathf.Cos(directionAngle), Mathf.Sin(directionAngle));
        float bandPhase = RandRange(rng, 0f, Mathf.PI * 2f);
        float maxDimension = Mathf.Max(1f, Mathf.Max(WorkingSet.worldBounds.width, WorkingSet.worldBounds.height));

        int width = WorkingSet.width;
        int height = WorkingSet.height;
        int rows = Mathf.Max(1, rowsPerYield);

        float seaMax = GetOceanFeatureMaxHeight01();

        float minDelta = float.PositiveInfinity;
        float maxDelta = float.NegativeInfinity;

        for (int y = 0; y < height; y++)
        {
            if (Progress.cancelRequested)
            {
                Progress.Cancel();
                AddLog("Ocean macro cancelled.");
                yield break;
            }

            float v = height <= 1 ? 0f : y / (float)(height - 1);

            for (int x = 0; x < width; x++)
            {
                float u = width <= 1 ? 0f : x / (float)(width - 1);

                Vector2 world = new Vector2(
                    Mathf.Lerp(WorkingSet.worldBounds.xMin, WorkingSet.worldBounds.xMax, u),
                    Mathf.Lerp(WorkingSet.worldBounds.yMin, WorkingSet.worldBounds.yMax, v)
                );

                float primary = Mathf.PerlinNoise(
                    (world.x + offsetA.x) * oceanMacroNoiseScale,
                    (world.y + offsetA.y) * oceanMacroNoiseScale
                );

                float secondary = Mathf.PerlinNoise(
                    (world.x + offsetB.x) * oceanMacroNoiseScale * oceanMacroSecondaryScaleMultiplier,
                    (world.y + offsetB.y) * oceanMacroNoiseScale * oceanMacroSecondaryScaleMultiplier
                );

                float macro = Mathf.Lerp(primary, secondary, oceanMacroSecondaryBlend);
                macro = (macro - 0.5f) * 2f;

                float warp = Mathf.PerlinNoise(
                    (world.x + warpOffset.x) * oceanMacroDirectionalWarpNoiseScale,
                    (world.y + warpOffset.y) * oceanMacroDirectionalWarpNoiseScale
                );
                warp = (warp - 0.5f) * 2f * oceanMacroDirectionalWarpStrength;

                float bandCoord = Vector2.Dot(world, bandDir) / maxDimension;
                float band = Mathf.Sin((bandCoord * oceanMacroDirectionalFrequency * Mathf.PI * 2f) + bandPhase + warp);

                // Soften the banding. We want broad ridges/troughs, not zebra noodles.
                band *= 0.5f + 0.5f * Mathf.Abs(band);

                float delta =
                    macro * oceanMacroStrength +
                    band * oceanMacroDirectionalStrength +
                    oceanMacroBias;

                delta = Mathf.Clamp(delta, -oceanMacroDeltaClamp, oceanMacroDeltaClamp);

                int index = WorkingSet.Index(x, y);

                // Ocean macro should not create land. Islands get their own layer later.
                float baseHeight = WorkingSet.baseOceanHeight[index];
                if (baseHeight + delta > seaMax)
                    delta = seaMax - baseHeight;

                WorkingSet.oceanFeatureDelta[index] = delta;

                if (delta < minDelta) minDelta = delta;
                if (delta > maxDelta) maxDelta = delta;
            }

            if (y % rows == 0)
            {
                Progress.Update(
                    y / (float)Mathf.Max(1, height - 1),
                    $"Generating ocean macro... row {y}/{height}"
                );

                yield return null;
            }
        }

        Progress.Update(1f, "Ocean macro complete.");
        AddLog($"Ocean macro complete. Delta={minDelta:0.000}..{maxDelta:0.000}");

        if (composeAndPreviewAfter)
        {
            yield return ComposeFinalRoutine(markComplete: false);

            if (IsCancelledOrFailed())
                yield break;

            yield return BuildPreviewTextureRoutine(WorldGenerationPreviewLayer.FinalHeight, markComplete: false);

            if (!IsCancelledOrFailed())
            {
                Progress.Complete("Ocean macro preview complete.");
                AddLog("Ocean macro preview complete.");
            }
        }
    }

    #endregion

    #region Compose and Preview

    private IEnumerator ComposeFinalRoutine(bool markComplete)
    {
        if (!HasWorkingSet)
        {
            Progress.Fail("No working set to compose.");
            AddLog("Compose failed: no working set.");
            yield break;
        }

        Progress.Begin(WorldGenerationPhase.ComposeFinal, "Composing final height...");
        yield return null;

        for (int i = 0; i < WorkingSet.Length; i++)
        {
            WorkingSet.finalHeight01[i] = Mathf.Clamp01(
                WorkingSet.baseOceanHeight[i] +
                WorkingSet.oceanFeatureDelta[i] +
                WorkingSet.islandMassDelta[i] +
                WorkingSet.islandDetailDelta[i]
            );
        }

        Progress.Update(1f, "Final height composed.");
        AddLog("Final height composed.");

        if (markComplete)
            Progress.Complete("Final height composed.");
    }

    private IEnumerator BuildPreviewTextureRoutine(
        WorldGenerationPreviewLayer layer,
        bool markComplete)
    {
        if (settings == null)
        {
            Progress.Fail("Missing settings for preview.");
            AddLog("Preview failed: missing settings.");
            yield break;
        }

        if (!HasWorkingSet)
        {
            Progress.Fail("No working set for preview.");
            AddLog("Preview failed: no working set.");
            yield break;
        }

        Progress.Begin(WorldGenerationPhase.BuildPreview, $"Building {layer} preview...");
        yield return null;

        DestroyPreviewTexture();

        WorldMapTopographyField field = WorkingSet.ToTopographyField(layer);
        WorldMapTopographySettings previewSettings = CreatePreviewSettings();

        try
        {
            previewTexture = WorldGenerationPreviewTextureBuilder.Build(
                field,
                previewSettings,
                previewDrawContours
            );
        }
        finally
        {
            DestroyPreviewSettings(previewSettings);
        }

        if (previewTexture == null)
        {
            Progress.Fail("Preview texture build failed.");
            AddLog("Preview failed: texture was null.");
            yield break;
        }

        Progress.Update(1f, "Preview texture built.");
        AddLog($"Preview built: {layer}, {previewTexture.width}x{previewTexture.height}");

        if (markComplete)
            Progress.Complete($"{layer} preview complete.");
    }

    #endregion

    #region Settings Helpers

    private Vector2 ResolveWorldSize()
    {
        if (syncWorldSizeFromTopographySettings && settings != null)
        {
            return new Vector2(
                Mathf.Max(1f, settings.worldSize.x),
                Mathf.Max(1f, settings.worldSize.y)
            );
        }

        return new Vector2(
            Mathf.Max(1f, worldSize.x),
            Mathf.Max(1f, worldSize.y)
        );
    }

    private int GetSeed()
    {
        return fallbackSeed;
    }

    private WorldMapTopographySettings CreatePreviewSettings()
    {
        if (settings == null)
            return null;

        WorldMapTopographySettings previewSettings = Instantiate(settings);

        previewSettings.drawContours = previewDrawContours;
        previewSettings.drawContoursIntoBaseTexture = previewDrawContours;
        previewSettings.worldSize = ResolveWorldSize();

        if (previewOverrideContourSettings)
        {
            previewSettings.contourCount = Mathf.Max(1, previewContourCount);
            previewSettings.contourThickness = Mathf.Max(0.0001f, previewContourThickness);
            previewSettings.majorContourEvery = Mathf.Max(1, previewMajorContourEvery);
        }

        return previewSettings;
    }

    private static void DestroyPreviewSettings(WorldMapTopographySettings previewSettings)
    {
        if (previewSettings == null)
            return;

        if (Application.isPlaying)
            Destroy(previewSettings);
        else
            DestroyImmediate(previewSettings);
    }

    private float GetOceanFeatureMaxHeight01()
    {
        float sea = settings != null
            ? Mathf.Clamp01(settings.seaLevel01)
            : 0.56f;

        return Mathf.Clamp01(sea - oceanMacroSeaClearance01);
    }

    private void RemapBaseOceanBelowSeaLevel(float[] values)
    {
        if (values == null || values.Length == 0)
            return;

        float sea = settings != null
            ? Mathf.Clamp01(settings.seaLevel01)
            : 0.56f;

        float max = Mathf.Min(baseOceanMaxHeight01, sea - baseOceanSeaClearance01);
        max = Mathf.Clamp01(max);

        float min = Mathf.Min(baseOceanMinHeight01, max - 0.01f);
        min = Mathf.Clamp01(min);

        if (max <= min + 0.001f)
        {
            min = 0.02f;
            max = Mathf.Max(min + 0.01f, sea - 0.02f);
            max = Mathf.Clamp01(max);
        }

        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Lerp(min, max, Mathf.Clamp01(values[i]));
    }

    #endregion

    #region Utility

    private void AddLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string line = $"[{System.DateTime.Now:HH:mm:ss}] {message}";
        _log.Add(line);

        const int maxLines = 80;
        while (_log.Count > maxLines)
            _log.RemoveAt(0);
    }

    private static void NormalizeArray01(float[] values, float min, float max)
    {
        if (values == null || values.Length == 0)
            return;

        float range = Mathf.Max(0.0001f, max - min);

        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Clamp01((values[i] - min) / range);
    }

    private static float EvaluateEdgeFalloff(float u, float v)
    {
        float edgeDist = Mathf.Min(
            Mathf.Min(u, 1f - u),
            Mathf.Min(v, 1f - v)
        );

        float t = 1f - Mathf.Clamp01(edgeDist / 0.22f);
        return Mathf.SmoothStep(0f, 1f, t);
    }

    private static Vector2 RandomOffset(System.Random rng)
    {
        return new Vector2(
            rng.Next(-100000, 100000),
            rng.Next(-100000, 100000)
        );
    }

    private static float RandRange(System.Random rng, float min, float max)
    {
        if (max < min)
            (min, max) = (max, min);

        return Mathf.Lerp(min, max, (float)rng.NextDouble());
    }

    private static Vector2 RandomPointInBounds(System.Random rng, Rect bounds, float inset01)
    {
        inset01 = Mathf.Clamp01(inset01);

        float x = Mathf.Lerp(bounds.xMin, bounds.xMax, Mathf.Lerp(inset01, 1f - inset01, (float)rng.NextDouble()));
        float y = Mathf.Lerp(bounds.yMin, bounds.yMax, Mathf.Lerp(inset01, 1f - inset01, (float)rng.NextDouble()));

        return new Vector2(x, y);
    }

    private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denom = Vector2.Dot(ab, ab);

        if (denom <= 0.000001f)
            return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
        Vector2 closest = a + ab * t;

        return Vector2.Distance(p, closest);
    }

    private void DestroyPreviewTexture()
    {
        if (previewTexture == null)
            return;

        if (Application.isPlaying)
            Destroy(previewTexture);
        else
            DestroyImmediate(previewTexture);

        previewTexture = null;
    }

    #endregion
}
