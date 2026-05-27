using System;
using UnityEngine;

[Serializable]
public struct WorldMapTopographyStats
{
    public int totalSamples;

    public int deepOcean;
    public int openOcean;
    public int shelfWater;
    public int shallowWater;

    public int beach;
    public int lowland;
    public int highland;
    public int mountain;

    public int WaterCount => deepOcean + openOcean + shelfWater + shallowWater;
    public int LandCount => beach + lowland + highland + mountain;

    public float Water01 => SafeRatio(WaterCount);
    public float Land01 => SafeRatio(LandCount);

    public float DeepOcean01 => SafeRatio(deepOcean);
    public float OpenOcean01 => SafeRatio(openOcean);
    public float ShelfWater01 => SafeRatio(shelfWater);
    public float ShallowWater01 => SafeRatio(shallowWater);

    public float Beach01 => SafeRatio(beach);
    public float Lowland01 => SafeRatio(lowland);
    public float Highland01 => SafeRatio(highland);
    public float Mountain01 => SafeRatio(mountain);

    public float ShallowOrShelfWater01 => SafeRatio(shallowWater + shelfWater);

    private float SafeRatio(int count)
    {
        return totalSamples <= 0 ? 0f : count / (float)totalSamples;
    }

    public string ToCompactString()
    {
        return
            $"Water {Water01:P0} / Land {Land01:P0}\n" +
            $"Deep {DeepOcean01:P0}, Open {OpenOcean01:P0}\n" +
            $"Shelf {ShelfWater01:P0}, Shallow {ShallowWater01:P0}";
    }
}