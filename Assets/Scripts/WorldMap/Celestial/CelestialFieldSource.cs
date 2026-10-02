using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level provider for deterministic celestial world truth.
/// This owns no player knowledge. It simply recreates the same CelestialField
/// from the current world's topography identity plus the assigned generation settings.
/// </summary>
[DisallowMultipleComponent]
public sealed class CelestialFieldSource : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapTopographyDebugSource topographySource;
    [SerializeField] private CelestialGenerationSettings generationSettings;

    [Header("Startup")]
    [SerializeField] private bool buildOnAwake = false;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    [Tooltip("Presentation only. Never grants evidence, validation, or annotations.")]
    public bool debugShowAllConstellations;
    [SerializeField] private GameObject debugRequester;
    public string debugSelectedConstellationId;

    [ContextMenu("Constellations / DEBUG Validate Selected")]
    public void DebugValidateSelected()
    {
        if (!EnsureField()) return;
        CelestialKnowledgeAuthority.TryValidateConstellation(debugRequester, Field, debugSelectedConstellationId, out string reason);
        Debug.Log(reason, this);
    }

    [ContextMenu("Constellations / DEBUG Validate All Eligible")]
    public void DebugValidateAllEligible()
    {
        if (!EnsureField()) return;
        CelestialKnowledgeAuthority.ValidateAllEligibleForDebug(debugRequester, Field, out string reason);
        Debug.Log(reason, this);
    }

    [ContextMenu("Constellations / DEBUG Reset Selected To Hidden")]
    public void DebugResetSelected()
    {
        if (!EnsureField()) return;
        CelestialKnowledgeAuthority.TryResetConstellationForDebug(debugRequester, Field, debugSelectedConstellationId, out string reason);
        Debug.Log(reason, this);
    }

    public CelestialGenerationSettings GenerationSettings => generationSettings;
    public CelestialField Field { get; private set; }
    public bool HasField => Field != null && Field.IsValid;

    private static readonly HashSet<CelestialFieldSource> ActiveSources = new();

    // Scene sky and map tools can have separate providers for the same celestial truth.
    public bool ShowAllConstellationsForField
    {
        get
        {
            if (debugShowAllConstellations) return true;
            foreach (var source in ActiveSources)
                if (source != null && source.debugShowAllConstellations && SharesField(source)) return true;
            return false;
        }
    }

    public void SetConstellationDebugVisibleForField(bool visible)
    {
        debugShowAllConstellations = visible;
        foreach (var source in ActiveSources)
            if (source != null && SharesField(source)) source.debugShowAllConstellations = visible;
    }

    private bool SharesField(CelestialFieldSource other) => HasField && other.HasField &&
        Field.WorldSeed == other.Field.WorldSeed &&
        Field.Identity.generatorVersion == other.Field.Identity.generatorVersion &&
        Field.Identity.configHash == other.Field.Identity.configHash &&
        RectApproximatelyEqual(Field.WorldBounds, other.Field.WorldBounds) &&
        Field.ConstellationConfig.Fingerprint == other.Field.ConstellationConfig.Fingerprint;

    private void OnEnable() => ActiveSources.Add(this);
    private void OnDisable() => ActiveSources.Remove(this);

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();

        if (buildOnAwake)
            EnsureField();
    }

    /// <summary>
    /// Ensures the source has a field matching the current world seed/bounds and
    /// generation settings. Returns false when the required world/config inputs
    /// are not available yet.
    /// </summary>
    public bool EnsureField()
    {
        AutoWire();

        if (generationSettings == null)
        {
            if (verboseLogging)
            {
                Debug.LogWarning(
                    "[CelestialFieldSource] Missing CelestialGenerationSettings. " +
                    "Assign the same settings asset used by the Phase 1 verifier.",
                    this);
            }

            return false;
        }

        if (!TryResolveWorldIdentity(out int worldSeed, out Rect worldBounds))
            return false;

        CelestialGenerationConfig desiredConfig = generationSettings.CreateConfigSnapshot();
        if (desiredConfig == null)
            return false;

        string desiredHash = CelestialGenerationFingerprint.Build(desiredConfig);

        if (FieldMatches(worldSeed, worldBounds, desiredHash))
        {
            ApplyConstellationConfig();
            return true;
        }

        Field = CelestialFieldGenerator.Create(worldSeed, worldBounds, desiredConfig);
        ApplyConstellationConfig();

        if (!HasField)
        {
            Debug.LogError("[CelestialFieldSource] Failed to create a valid CelestialField.", this);
            Field = null;
            return false;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[CelestialFieldSource] Built celestial field. " +
                $"Seed={Field.WorldSeed}, Bounds={Field.WorldBounds}, " +
                $"Cells={Field.CellCountX}x{Field.CellCountY}, " +
                $"GeneratorV={Field.Identity.generatorVersion}, ConfigHash={Field.Identity.configHash}",
                this);
        }

        return true;
    }

    [ContextMenu("Rebuild Celestial Field")]
    public void RebuildField()
    {
        Field = null;
        EnsureField();
    }

    [ContextMenu("Log Celestial Field Source")]
    private void LogSummary()
    {
        if (!EnsureField())
        {
            Debug.LogWarning("[CelestialFieldSource] No valid field to summarize.", this);
            return;
        }

        Debug.Log(
            $"[CelestialFieldSource] Seed={Field.WorldSeed}, Bounds={Field.WorldBounds}, " +
            $"Cells={Field.CellCountX}x{Field.CellCountY}, " +
            $"GeneratorV={Field.Identity.generatorVersion}, ConfigHash={Field.Identity.configHash}",
            this);
    }

    private bool TryResolveWorldIdentity(out int worldSeed, out Rect worldBounds)
    {
        worldSeed = 0;
        worldBounds = default;

        WorldMapTopographyField topographyField = null;

        if (topographySource != null)
        {
            if (topographySource.Field == null || !topographySource.Field.IsValid)
                topographySource.LoadOrGenerate();

            if (topographySource.Field != null && topographySource.Field.IsValid)
                topographyField = topographySource.Field;
        }

        if (topographyField == null && WorldMapRuntimeCache.I != null && WorldMapRuntimeCache.I.HasTopography)
            topographyField = WorldMapRuntimeCache.I.Field;

        if (topographyField == null || !topographyField.IsValid)
        {
            if (verboseLogging)
            {
                Debug.LogWarning(
                    "[CelestialFieldSource] No valid topography field/runtime cache is available yet.",
                    this);
            }

            return false;
        }

        worldSeed = topographyField.Seed;
        worldBounds = topographyField.WorldBounds;
        return WorldMapCoordinateSpace.IsValidBounds(worldBounds);
    }

    private bool FieldMatches(int worldSeed, Rect worldBounds, string configHash)
    {
        if (!HasField || Field.Identity == null)
            return false;

        return Field.WorldSeed == worldSeed &&
               RectApproximatelyEqual(Field.WorldBounds, worldBounds) &&
               string.Equals(Field.Identity.configHash, configHash, System.StringComparison.Ordinal) &&
               Field.Identity.generatorVersion == CelestialFieldGenerator.CurrentGeneratorVersion;
    }

    private void ApplyConstellationConfig()
    {
        if (!HasField) return;
        CelestialChartStateSnapshot state = GameState.I != null ? GameState.I.celestialCharts : null;
        bool sameWorld = state != null && state.constellationWorldSeed == Field.WorldSeed &&
            state.constellationFieldHash == Field.Identity.configHash;
        // Old chart saves retain the pre-Phase-7 derivative truth defaults.
        var config = sameWorld && state.constellationGeneration != null ? state.constellationGeneration :
            state != null && state.fragments != null && state.fragments.Count > 0 && state.constellationGeneration == null
                ? new CelestialConstellationGenerationConfig() : generationSettings.CreateConstellationConfigSnapshot();
        Field.ConfigureConstellations(config);
    }

    private void AutoWire()
    {
        if (topographySource == null)
        {
            topographySource = FindAnyObjectByType<WorldMapTopographyDebugSource>(
                FindObjectsInactive.Include);
        }
    }

    private static bool RectApproximatelyEqual(Rect a, Rect b)
    {
        const float epsilon = 0.0001f;

        return Mathf.Abs(a.x - b.x) <= epsilon &&
               Mathf.Abs(a.y - b.y) <= epsilon &&
               Mathf.Abs(a.width - b.width) <= epsilon &&
               Mathf.Abs(a.height - b.height) <= epsilon;
    }
}
