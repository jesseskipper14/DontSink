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

    public CelestialGenerationSettings GenerationSettings => generationSettings;
    public CelestialField Field { get; private set; }
    public bool HasField => Field != null && Field.IsValid;

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
            return true;

        Field = CelestialFieldGenerator.Create(worldSeed, worldBounds, desiredConfig);

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
