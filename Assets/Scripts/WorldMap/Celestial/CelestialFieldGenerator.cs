using System.Collections.Generic;
using UnityEngine;

public static class CelestialFieldGenerator
{
    public const int CurrentGeneratorVersion = 1;

    private const float AreaDensityScale = 1000f;

    private const int AmbientChannel = 101;
    private const int LandmarkChannel = 211;
    private const int NebulaChannel = 307;
    private const int DeepSkyChannel = 401;

    public static CelestialField Create(
        int worldSeed,
        Rect worldBounds,
        CelestialGenerationSettings settings)
    {
        CelestialGenerationConfig config = settings != null
            ? settings.CreateConfigSnapshot()
            : null;

        CelestialField field = Create(worldSeed, worldBounds, config);
        if (field != null) field.ConfigureConstellations(settings.constellations);
        return field;
    }

    public static CelestialField Create(
        int worldSeed,
        Rect worldBounds,
        CelestialGenerationConfig config)
    {
        if (!WorldMapCoordinateSpace.IsValidBounds(worldBounds))
        {
            Debug.LogError($"[CelestialFieldGenerator] Invalid world bounds: {worldBounds}.");
            return null;
        }

        if (config == null)
        {
            Debug.LogError("[CelestialFieldGenerator] Missing celestial generation config.");
            return null;
        }

        CelestialGenerationConfig frozenConfig = config.Clone();
        if (frozenConfig == null)
        {
            Debug.LogError("[CelestialFieldGenerator] Failed to clone celestial generation config.");
            return null;
        }

        frozenConfig.Sanitize();

        CelestialGenerationIdentity identity =
            CelestialGenerationIdentity.Create(worldSeed, worldBounds, frozenConfig);

        return new CelestialField(worldSeed, worldBounds, frozenConfig, identity);
    }

    public static void GenerateCell(
        CelestialField field,
        int cellX,
        int cellY,
        List<CelestialObject> results,
        bool clearResults = true)
    {
        if (results == null)
            return;

        if (clearResults)
            results.Clear();

        if (field == null || !field.IsValid)
            return;

        if (cellX < 0 || cellY < 0 || cellX >= field.CellCountX || cellY >= field.CellCountY)
            return;

        Rect cellRect = field.GetCellWorldRect(cellX, cellY);
        float cellArea = Mathf.Max(0f, cellRect.width * cellRect.height);

        GenerateKind(
            field,
            cellX,
            cellY,
            cellRect,
            cellArea,
            CelestialObjectKind.AmbientStar,
            AmbientChannel,
            field.Config.ambientStarsPer1000WorldArea,
            field.Config.ambientBrightnessMin,
            field.Config.ambientBrightnessMax,
            field.Config.ambientProminenceMin,
            field.Config.ambientProminenceMax,
            field.Config.ambientVisualVariantCount,
            0f,
            0f,
            results);

        GenerateKind(
            field,
            cellX,
            cellY,
            cellRect,
            cellArea,
            CelestialObjectKind.LandmarkStar,
            LandmarkChannel,
            field.Config.landmarkStarsPer1000WorldArea,
            field.Config.landmarkBrightnessMin,
            field.Config.landmarkBrightnessMax,
            field.Config.landmarkProminenceMin,
            field.Config.landmarkProminenceMax,
            field.Config.landmarkVisualVariantCount,
            0f,
            0f,
            results);

        GenerateKind(
            field,
            cellX,
            cellY,
            cellRect,
            cellArea,
            CelestialObjectKind.Nebula,
            NebulaChannel,
            field.Config.nebulaePer1000WorldArea,
            field.Config.nebulaBrightnessMin,
            field.Config.nebulaBrightnessMax,
            field.Config.nebulaProminenceMin,
            field.Config.nebulaProminenceMax,
            field.Config.nebulaVisualVariantCount,
            field.Config.nebulaRadiusWorldMin,
            field.Config.nebulaRadiusWorldMax,
            results);

        GenerateKind(
            field,
            cellX,
            cellY,
            cellRect,
            cellArea,
            CelestialObjectKind.DeepSkyObject,
            DeepSkyChannel,
            field.Config.deepSkyObjectsPer1000WorldArea,
            field.Config.deepSkyBrightnessMin,
            field.Config.deepSkyBrightnessMax,
            field.Config.deepSkyProminenceMin,
            field.Config.deepSkyProminenceMax,
            field.Config.deepSkyVisualVariantCount,
            field.Config.deepSkyRadiusWorldMin,
            field.Config.deepSkyRadiusWorldMax,
            results);
    }

    private static void GenerateKind(
        CelestialField field,
        int cellX,
        int cellY,
        Rect cellRect,
        float cellArea,
        CelestialObjectKind kind,
        int channel,
        float densityPer1000WorldArea,
        float brightnessMin,
        float brightnessMax,
        float prominenceMin,
        float prominenceMax,
        int visualVariantCount,
        float radiusMin,
        float radiusMax,
        List<CelestialObject> results)
    {
        float expectedCount = Mathf.Max(0f, densityPer1000WorldArea) * cellArea / AreaDensityScale;
        int count = DeterministicCount(field, cellX, cellY, channel, expectedCount);

        for (int localIndex = 0; localIndex < count; localIndex++)
        {
            CelestialDeterministicRandom rng = CreateObjectRng(
                field,
                cellX,
                cellY,
                channel,
                localIndex);

            Vector2 position = new Vector2(
                Mathf.Lerp(cellRect.xMin, cellRect.xMax, rng.Next01()),
                Mathf.Lerp(cellRect.yMin, cellRect.yMax, rng.Next01()));

            float brightness = rng.Range(brightnessMin, brightnessMax);
            float prominence = rng.Range(prominenceMin, prominenceMax);
            CelestialColorClass colorClass = PickColorClass(kind, ref rng);
            int visualVariant = rng.RangeInt(0, Mathf.Max(1, visualVariantCount));
            float rotation = rng.Range(0f, 360f);
            float radius = radiusMax > 0f
                ? rng.Range(Mathf.Max(0f, radiusMin), Mathf.Max(radiusMin, radiusMax))
                : 0f;

            string stableId = CelestialStableId.Build(
                CurrentGeneratorVersion,
                field.WorldSeed,
                cellX,
                cellY,
                kind,
                localIndex);

            results.Add(new CelestialObject(
                stableId,
                kind,
                position,
                brightness,
                prominence,
                colorClass,
                visualVariant,
                rotation,
                radius,
                cellX,
                cellY,
                localIndex));
        }
    }

    private static int DeterministicCount(
        CelestialField field,
        int cellX,
        int cellY,
        int channel,
        float expectedCount)
    {
        if (expectedCount <= 0f)
            return 0;

        int whole = Mathf.FloorToInt(expectedCount);
        float fractional = expectedCount - whole;

        CelestialDeterministicRandom rng = CreateChannelRng(
            field,
            cellX,
            cellY,
            channel,
            0x5EEDC0DE);

        return whole + (rng.Next01() < fractional ? 1 : 0);
    }

    private static CelestialColorClass PickColorClass(
        CelestialObjectKind kind,
        ref CelestialDeterministicRandom rng)
    {
        float roll = rng.Next01();

        switch (kind)
        {
            case CelestialObjectKind.AmbientStar:
                if (roll < 0.68f) return CelestialColorClass.White;
                if (roll < 0.84f) return CelestialColorClass.BlueWhite;
                if (roll < 0.94f) return CelestialColorClass.Gold;
                if (roll < 0.98f) return CelestialColorClass.Orange;
                return CelestialColorClass.Red;

            case CelestialObjectKind.LandmarkStar:
                if (roll < 0.36f) return CelestialColorClass.White;
                if (roll < 0.52f) return CelestialColorClass.BlueWhite;
                if (roll < 0.64f) return CelestialColorClass.Gold;
                if (roll < 0.74f) return CelestialColorClass.Orange;
                if (roll < 0.84f) return CelestialColorClass.Red;
                if (roll < 0.92f) return CelestialColorClass.Cyan;
                return CelestialColorClass.Violet;

            case CelestialObjectKind.Nebula:
                if (roll < 0.22f) return CelestialColorClass.BlueWhite;
                if (roll < 0.40f) return CelestialColorClass.Cyan;
                if (roll < 0.62f) return CelestialColorClass.Violet;
                if (roll < 0.80f) return CelestialColorClass.Red;
                return CelestialColorClass.Gold;

            case CelestialObjectKind.DeepSkyObject:
                if (roll < 0.32f) return CelestialColorClass.White;
                if (roll < 0.56f) return CelestialColorClass.BlueWhite;
                if (roll < 0.74f) return CelestialColorClass.Gold;
                if (roll < 0.88f) return CelestialColorClass.Cyan;
                return CelestialColorClass.Violet;

            default:
                return CelestialColorClass.White;
        }
    }

    private static CelestialDeterministicRandom CreateObjectRng(
        CelestialField field,
        int cellX,
        int cellY,
        int channel,
        int localIndex)
    {
        ulong seed = Hash64(
            field.WorldSeed,
            field.Config.generationSalt,
            CurrentGeneratorVersion,
            cellX,
            cellY,
            channel,
            localIndex);

        ulong sequence = Hash64(
            field.Config.generationSalt,
            field.WorldSeed,
            channel,
            localIndex,
            cellY,
            cellX,
            CurrentGeneratorVersion);

        return new CelestialDeterministicRandom(seed, sequence);
    }

    private static CelestialDeterministicRandom CreateChannelRng(
        CelestialField field,
        int cellX,
        int cellY,
        int channel,
        int salt)
    {
        ulong seed = Hash64(
            field.WorldSeed,
            field.Config.generationSalt,
            CurrentGeneratorVersion,
            cellX,
            cellY,
            channel,
            salt);

        ulong sequence = Hash64(
            salt,
            channel,
            cellY,
            cellX,
            CurrentGeneratorVersion,
            field.Config.generationSalt,
            field.WorldSeed);

        return new CelestialDeterministicRandom(seed, sequence);
    }

    private static ulong Hash64(
        int a,
        int b,
        int c,
        int d,
        int e,
        int f,
        int g)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offset;
            hash = AppendInt(hash, a, prime);
            hash = AppendInt(hash, b, prime);
            hash = AppendInt(hash, c, prime);
            hash = AppendInt(hash, d, prime);
            hash = AppendInt(hash, e, prime);
            hash = AppendInt(hash, f, prime);
            hash = AppendInt(hash, g, prime);
            return hash;
        }
    }

    private static ulong AppendInt(ulong hash, int value, ulong prime)
    {
        unchecked
        {
            uint bits = (uint)value;

            hash ^= (byte)(bits & 0xFF);
            hash *= prime;
            hash ^= (byte)((bits >> 8) & 0xFF);
            hash *= prime;
            hash ^= (byte)((bits >> 16) & 0xFF);
            hash *= prime;
            hash ^= (byte)((bits >> 24) & 0xFF);
            hash *= prime;

            return hash;
        }
    }
}
