using UnityEngine;

public static class WorldMapTopographyAnalysis
{
    public static WorldMapTopographyClass Classify(
        float height01,
        WorldMapTopographySettings settings)
    {
        float h = Mathf.Clamp01(height01);
        float sea = Mathf.Clamp01(settings.seaLevel01);

        if (h < sea)
        {
            float depth = sea - h;

            if (depth <= settings.shallowDepth01 * 0.35f)
                return WorldMapTopographyClass.ShallowWater;

            if (depth <= settings.shallowDepth01)
                return WorldMapTopographyClass.ShelfWater;

            if (depth <= settings.openOceanDepth01)
                return WorldMapTopographyClass.OpenOcean;

            return WorldMapTopographyClass.DeepOcean;
        }

        float landHeight = h - sea;

        if (landHeight <= settings.beachHeight01)
            return WorldMapTopographyClass.Beach;

        if (landHeight <= settings.lowlandHeight01)
            return WorldMapTopographyClass.Lowland;

        if (landHeight <= settings.highlandHeight01)
            return WorldMapTopographyClass.Highland;

        return WorldMapTopographyClass.Mountain;
    }

    public static WorldMapTopographyStats Analyze(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings)
    {
        var stats = new WorldMapTopographyStats();

        if (field == null || !field.IsValid || settings == null)
            return stats;

        stats.totalSamples = field.Width * field.Height;

        for (int y = 0; y < field.Height; y++)
        {
            for (int x = 0; x < field.Width; x++)
            {
                float h = field.Get01(x, y);
                AddClass(ref stats, Classify(h, settings));
            }
        }

        return stats;
    }

    public static float CalculateWaterPercent(
        WorldMapTopographyField field,
        float seaLevel01)
    {
        if (field == null || !field.IsValid)
            return 0f;

        int water = 0;
        int total = field.Width * field.Height;

        for (int y = 0; y < field.Height; y++)
        {
            for (int x = 0; x < field.Width; x++)
            {
                if (field.Get01(x, y) < seaLevel01)
                    water++;
            }
        }

        return total <= 0 ? 0f : water / (float)total;
    }

    public static float FindSeaLevelForTargetWaterPercent(
        WorldMapTopographyField field,
        float targetWater01,
        int iterations = 24)
    {
        if (field == null || !field.IsValid)
            return 0.5f;

        targetWater01 = Mathf.Clamp01(targetWater01);

        float lo = 0f;
        float hi = 1f;

        for (int i = 0; i < Mathf.Max(1, iterations); i++)
        {
            float mid = (lo + hi) * 0.5f;
            float water = CalculateWaterPercent(field, mid);

            if (water < targetWater01)
                lo = mid;
            else
                hi = mid;
        }

        return (lo + hi) * 0.5f;
    }

    private static void AddClass(
        ref WorldMapTopographyStats stats,
        WorldMapTopographyClass cls)
    {
        switch (cls)
        {
            case WorldMapTopographyClass.DeepOcean:
                stats.deepOcean++;
                break;

            case WorldMapTopographyClass.OpenOcean:
                stats.openOcean++;
                break;

            case WorldMapTopographyClass.ShelfWater:
                stats.shelfWater++;
                break;

            case WorldMapTopographyClass.ShallowWater:
                stats.shallowWater++;
                break;

            case WorldMapTopographyClass.Beach:
                stats.beach++;
                break;

            case WorldMapTopographyClass.Lowland:
                stats.lowland++;
                break;

            case WorldMapTopographyClass.Highland:
                stats.highland++;
                break;

            case WorldMapTopographyClass.Mountain:
                stats.mountain++;
                break;
        }
    }
}