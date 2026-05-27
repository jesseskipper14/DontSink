using UnityEngine;

public static class WorldMapBiomeGenerator
{
    public static WorldMapBiomeLayer Generate(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings,
        WorldMapBiomeCatalog catalog)
    {
        if (field == null || !field.IsValid || settings == null || catalog == null || catalog.Count == 0)
            return null;

        if (!settings.generateBiomeLayer)
            return null;

        int w = Mathf.Max(1, settings.biomeGridWidth);
        int h = Mathf.Max(1, settings.biomeGridHeight);

        int[] indices = new int[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = h <= 1 ? 0f : y / (float)(h - 1);

            for (int x = 0; x < w; x++)
            {
                float u = w <= 1 ? 0f : x / (float)(w - 1);

                WorldMapBiomeMetrics metrics = CalculateMetrics(field, settings, u, v);
                int picked = PickBiome(field.Seed, x, y, metrics, settings, catalog);

                indices[y * w + x] = picked;
            }
        }

        SmoothBiomeGrid(indices, w, h, catalog.Count, settings.biomeSmoothingIterations);

        return new WorldMapBiomeLayer(
            w,
            h,
            field.WorldBounds,
            catalog,
            indices
        );
    }

    private static int PickBiome(
        int seed,
        int x,
        int y,
        WorldMapBiomeMetrics metrics,
        WorldMapTopographySettings settings,
        WorldMapBiomeCatalog catalog)
    {
        int best = -1;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < catalog.Count; i++)
        {
            WorldMapBiomeDef biome = catalog.GetBiome(i);
            if (biome == null)
                continue;

            float noise = SignedHash01(seed, x, y, i) * settings.biomeScoreNoise;
            float score = biome.Score(metrics, noise);

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private static WorldMapBiomeMetrics CalculateMetrics(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings,
        float centerU,
        float centerV)
    {
        var m = new WorldMapBiomeMetrics
        {
            minHeight01 = 1f,
            maxHeight01 = 0f
        };

        int grid = Mathf.Max(1, settings.biomeSampleGrid);

        // Prefer odd sample grids so there is a true center sample.
        if (grid % 2 == 0)
            grid++;

        float radiusU = settings.biomeSampleRadiusWorld / Mathf.Max(0.0001f, field.WorldBounds.width);
        float radiusV = settings.biomeSampleRadiusWorld / Mathf.Max(0.0001f, field.WorldBounds.height);

        for (int sy = 0; sy < grid; sy++)
        {
            float ty = grid <= 1 ? 0f : sy / (float)(grid - 1);
            float oy = Mathf.Lerp(-radiusV, radiusV, ty);

            for (int sx = 0; sx < grid; sx++)
            {
                float tx = grid <= 1 ? 0f : sx / (float)(grid - 1);
                float ox = Mathf.Lerp(-radiusU, radiusU, tx);

                float u = Mathf.Clamp01(centerU + ox);
                float v = Mathf.Clamp01(centerV + oy);

                float height = field.Sample01UV(u, v);

                if (height < m.minHeight01) m.minHeight01 = height;
                if (height > m.maxHeight01) m.maxHeight01 = height;

                WorldMapTopographyClass cls =
                    WorldMapTopographyAnalysis.Classify(height, settings);

                AddClass(ref m, cls);
                m.totalSamples++;
            }
        }

        return m;
    }

    private static void AddClass(ref WorldMapBiomeMetrics m, WorldMapTopographyClass cls)
    {
        switch (cls)
        {
            case WorldMapTopographyClass.DeepOcean:
                m.deepOcean++;
                break;

            case WorldMapTopographyClass.OpenOcean:
                m.openOcean++;
                break;

            case WorldMapTopographyClass.ShelfWater:
                m.shelfWater++;
                break;

            case WorldMapTopographyClass.ShallowWater:
                m.shallowWater++;
                break;

            case WorldMapTopographyClass.Beach:
                m.beach++;
                break;

            case WorldMapTopographyClass.Lowland:
                m.lowland++;
                break;

            case WorldMapTopographyClass.Highland:
                m.highland++;
                break;

            case WorldMapTopographyClass.Mountain:
                m.mountain++;
                break;
        }
    }

    private static void SmoothBiomeGrid(
        int[] indices,
        int width,
        int height,
        int biomeCount,
        int iterations)
    {
        if (indices == null || indices.Length != width * height)
            return;

        biomeCount = Mathf.Max(1, biomeCount);
        iterations = Mathf.Max(0, iterations);

        int[] src = indices;
        int[] dst = new int[indices.Length];
        int[] counts = new int[biomeCount];

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    System.Array.Clear(counts, 0, counts.Length);

                    int current = src[y * width + x];

                    // Self-weight prevents aggressive biome erosion.
                    if (current >= 0 && current < biomeCount)
                        counts[current] += 3;

                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int yy = y + oy;
                        if (yy < 0 || yy >= height)
                            continue;

                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0)
                                continue;

                            int xx = x + ox;
                            if (xx < 0 || xx >= width)
                                continue;

                            int idx = src[yy * width + xx];
                            if (idx >= 0 && idx < biomeCount)
                                counts[idx]++;
                        }
                    }

                    int best = current;
                    int bestCount = current >= 0 && current < biomeCount ? counts[current] : -1;

                    for (int i = 0; i < biomeCount; i++)
                    {
                        if (counts[i] > bestCount)
                        {
                            bestCount = counts[i];
                            best = i;
                        }
                    }

                    dst[y * width + x] = best;
                }
            }

            (src, dst) = (dst, src);
        }

        if (!ReferenceEquals(src, indices))
            System.Array.Copy(src, indices, indices.Length);
    }

    private static float SignedHash01(int seed, int x, int y, int biomeIndex)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            h = (h ^ (uint)(x * 73856093)) * 16777619u;
            h = (h ^ (uint)(y * 19349663)) * 16777619u;
            h = (h ^ (uint)(biomeIndex * 83492791)) * 16777619u;

            float v = (h & 0x00FFFFFF) / (float)0x00FFFFFF;
            return (v * 2f) - 1f;
        }
    }
}