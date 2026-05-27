using System;
using UnityEngine;

[Serializable]
public struct WorldMapBiomeMetrics
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

    public float minHeight01;
    public float maxHeight01;

    public float DeepOcean01 => Ratio(deepOcean);
    public float OpenOcean01 => Ratio(openOcean);
    public float ShelfWater01 => Ratio(shelfWater);
    public float ShallowWater01 => Ratio(shallowWater);
    public float Beach01 => Ratio(beach);
    public float Lowland01 => Ratio(lowland);
    public float Highland01 => Ratio(highland);
    public float Mountain01 => Ratio(mountain);

    public int WaterCount => deepOcean + openOcean + shelfWater + shallowWater;
    public int LandCount => beach + lowland + highland + mountain;

    public float Water01 => Ratio(WaterCount);
    public float Land01 => Ratio(LandCount);

    public float ShallowShelf01 => ShelfWater01 + ShallowWater01;
    public float DeepOpen01 => DeepOcean01 + OpenOcean01;
    public float HighlandMountain01 => Highland01 + Mountain01;

    public float Ruggedness01 => Mathf.Clamp01((maxHeight01 - minHeight01) * 2f);

    public float CoastPresence01
    {
        get
        {
            if (WaterCount <= 0 || LandCount <= 0)
                return 0f;

            // Highest when land/water mix is balanced, lower when nearly all one or the other.
            return 1f - Mathf.Abs(Water01 - Land01);
        }
    }

    private float Ratio(int count)
    {
        return totalSamples <= 0 ? 0f : count / (float)totalSamples;
    }
}