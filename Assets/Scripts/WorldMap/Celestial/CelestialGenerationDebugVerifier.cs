using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CelestialGenerationDebugVerifier : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapTopographyDebugSource topographySource;
    [SerializeField] private CelestialGenerationSettings settings;

    [Header("Fallback")]
    [Tooltip("Used only when no valid topography source/runtime cache is available.")]
    [SerializeField] private int fallbackWorldSeed = 12345;

    [SerializeField] private Rect fallbackWorldBounds = new Rect(-200f, -125f, 400f, 250f);

    [Header("Debug")]
    [SerializeField] private bool logSampleObjects;
    [SerializeField, Range(1, 20)] private int sampleObjectCount = 5;

    private void Reset()
    {
        topographySource = FindAnyObjectByType<WorldMapTopographyDebugSource>();
    }

    [ContextMenu("Verify Celestial Determinism")]
    public void VerifyDeterminism()
    {
        if (!TryCreateField(out CelestialField field))
            return;

        string forwardFingerprint = BuildCellTraversalFingerprint(field, reverse: false, out int forwardCount);
        string reverseFingerprint = BuildCellTraversalFingerprint(field, reverse: true, out int reverseCount);

        CelestialField recreated = CelestialFieldGenerator.Create(
            field.WorldSeed,
            field.WorldBounds,
            field.Config);

        string recreatedFingerprint = BuildCellTraversalFingerprint(recreated, reverse: false, out int recreatedCount);

        CelestialField differentSeed = CelestialFieldGenerator.Create(
            unchecked(field.WorldSeed + 1),
            field.WorldBounds,
            field.Config);

        string differentSeedFingerprint = BuildCellTraversalFingerprint(differentSeed, reverse: false, out int differentSeedCount);

        bool sameAcrossOrder =
            forwardCount == reverseCount &&
            string.Equals(forwardFingerprint, reverseFingerprint, StringComparison.Ordinal);

        bool sameAcrossRecreation =
            forwardCount == recreatedCount &&
            string.Equals(forwardFingerprint, recreatedFingerprint, StringComparison.Ordinal);

        bool differentAcrossSeed =
            forwardCount != differentSeedCount ||
            !string.Equals(forwardFingerprint, differentSeedFingerprint, StringComparison.Ordinal);

        bool stableIdResolution = VerifyStableIdResolution(field);

        Debug.Log(
            "[CelestialGenerationDebugVerifier] " +
            $"Objects={forwardCount}, Cells={field.CellCountX}x{field.CellCountY}, " +
            $"GeneratorV={field.Identity.generatorVersion}, ConfigHash={field.Identity.configHash}\n" +
            $"QueryOrderIndependent={sameAcrossOrder}, " +
            $"RecreationStable={sameAcrossRecreation}, " +
            $"DifferentSeedChangesSky={differentAcrossSeed}, " +
            $"StableIdResolution={stableIdResolution}\n" +
            $"Fingerprint={forwardFingerprint}",
            this);

        if (!sameAcrossOrder || !sameAcrossRecreation || !differentAcrossSeed || !stableIdResolution)
        {
            Debug.LogError(
                "[CelestialGenerationDebugVerifier] Determinism verification FAILED. " +
                "Do not proceed to celestial rendering until this is fixed.",
                this);
        }
        else
        {
            Debug.Log(
                "[CelestialGenerationDebugVerifier] Determinism verification PASSED.",
                this);
        }
    }

    [ContextMenu("Log Celestial Summary")]
    public void LogSummary()
    {
        if (!TryCreateField(out CelestialField field))
            return;

        var all = new List<CelestialObject>();
        field.QueryAll(all);

        int ambient = 0;
        int landmarks = 0;
        int nebulae = 0;
        int deepSky = 0;

        for (int i = 0; i < all.Count; i++)
        {
            switch (all[i].Kind)
            {
                case CelestialObjectKind.AmbientStar:
                    ambient++;
                    break;
                case CelestialObjectKind.LandmarkStar:
                    landmarks++;
                    break;
                case CelestialObjectKind.Nebula:
                    nebulae++;
                    break;
                case CelestialObjectKind.DeepSkyObject:
                    deepSky++;
                    break;
            }
        }

        Debug.Log(
            "[CelestialGenerationDebugVerifier] " +
            $"Seed={field.WorldSeed}, Bounds={field.WorldBounds}, Cells={field.CellCountX}x{field.CellCountY}, " +
            $"Total={all.Count}, Ambient={ambient}, Landmarks={landmarks}, " +
            $"Nebulae={nebulae}, DeepSky={deepSky}, ConfigHash={field.Identity.configHash}",
            this);

        if (!logSampleObjects)
            return;

        int count = Mathf.Min(sampleObjectCount, all.Count);
        for (int i = 0; i < count; i++)
        {
            CelestialObject obj = all[i];
            Debug.Log(
                $"[CelestialGenerationDebugVerifier] Sample[{i}] " +
                $"Id={obj.StableId}, Kind={obj.Kind}, Pos={obj.WorldPosition}, " +
                $"Brightness={obj.Brightness01:0.000}, Prominence={obj.Prominence01:0.000}, " +
                $"Color={obj.ColorClass}, Variant={obj.VisualVariant}, Radius={obj.FootprintRadiusWorld:0.00}",
                this);
        }
    }

    private bool TryCreateField(out CelestialField field)
    {
        field = null;

        if (settings == null)
        {
            Debug.LogError(
                "[CelestialGenerationDebugVerifier] Missing CelestialGenerationSettings.",
                this);
            return false;
        }

        int worldSeed = fallbackWorldSeed;
        Rect worldBounds = fallbackWorldBounds;

        WorldMapTopographyField topographyField = null;

        if (topographySource != null && topographySource.Field != null && topographySource.Field.IsValid)
        {
            topographyField = topographySource.Field;
        }
        else if (WorldMapRuntimeCache.I != null && WorldMapRuntimeCache.I.HasTopography)
        {
            topographyField = WorldMapRuntimeCache.I.Field;
        }

        if (topographyField != null)
        {
            worldSeed = topographyField.Seed;
            worldBounds = topographyField.WorldBounds;
        }

        field = CelestialFieldGenerator.Create(worldSeed, worldBounds, settings);

        if (field == null || !field.IsValid)
        {
            Debug.LogError(
                "[CelestialGenerationDebugVerifier] Failed to create a valid CelestialField.",
                this);
            field = null;
            return false;
        }

        return true;
    }

    private static string BuildCellTraversalFingerprint(
        CelestialField field,
        bool reverse,
        out int objectCount)
    {
        var objectSignatures = new List<string>();
        var cellObjects = new List<CelestialObject>();

        int startY = reverse ? field.CellCountY - 1 : 0;
        int endY = reverse ? -1 : field.CellCountY;
        int stepY = reverse ? -1 : 1;

        int startX = reverse ? field.CellCountX - 1 : 0;
        int endX = reverse ? -1 : field.CellCountX;
        int stepX = reverse ? -1 : 1;

        for (int y = startY; y != endY; y += stepY)
        {
            for (int x = startX; x != endX; x += stepX)
            {
                field.GetCellObjects(x, y, cellObjects);

                for (int i = 0; i < cellObjects.Count; i++)
                    objectSignatures.Add(BuildObjectSignature(cellObjects[i]));
            }
        }

        objectSignatures.Sort(StringComparer.Ordinal);
        objectCount = objectSignatures.Count;

        var combined = new StringBuilder(objectSignatures.Count * 48);
        for (int i = 0; i < objectSignatures.Count; i++)
            combined.Append(objectSignatures[i]).Append('\n');

        return Fnv1a64(combined.ToString()).ToString("X16");
    }

    private static bool VerifyStableIdResolution(CelestialField field)
    {
        var all = new List<CelestialObject>();
        field.QueryAll(all);

        if (all.Count == 0)
            return false;

        CelestialObject sample = all[all.Count / 2];
        if (!field.TryResolveObject(sample.StableId, out CelestialObject resolved))
            return false;

        return resolved != null &&
               resolved.StableId == sample.StableId &&
               resolved.Kind == sample.Kind &&
               Approximately(resolved.WorldPosition, sample.WorldPosition);
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) <= 0.000001f &&
               Mathf.Abs(a.y - b.y) <= 0.000001f;
    }

    private static string BuildObjectSignature(CelestialObject obj)
    {
        return string.Join(
            "|",
            obj.StableId,
            (int)obj.Kind,
            obj.WorldPosition.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            obj.WorldPosition.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            obj.Brightness01.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            obj.Prominence01.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            (int)obj.ColorClass,
            obj.VisualVariant,
            obj.RotationDegrees.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            obj.FootprintRadiusWorld.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static ulong Fnv1a64(string text)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offset;

            if (!string.IsNullOrEmpty(text))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= prime;
                }
            }

            return hash;
        }
    }
}
