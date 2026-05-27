using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/Biomes/Biome Catalog",
    fileName = "WorldMapBiomeCatalog")]
public sealed class WorldMapBiomeCatalog : ScriptableObject
{
    [Header("Biomes")]
    public List<WorldMapBiomeDef> biomes = new();

    [Tooltip("Used if a biome index is invalid.")]
    public WorldMapBiomeDef fallbackBiome;

    public int Count => biomes != null ? biomes.Count : 0;

    public WorldMapBiomeDef GetBiome(int index)
    {
        if (biomes == null || index < 0 || index >= biomes.Count)
            return fallbackBiome;

        return biomes[index] != null ? biomes[index] : fallbackBiome;
    }

    public int IndexOf(WorldMapBiomeDef biome)
    {
        if (biomes == null || biome == null)
            return -1;

        for (int i = 0; i < biomes.Count; i++)
        {
            if (biomes[i] == biome)
                return i;
        }

        return -1;
    }

    public int IndexOfId(string biomeId)
    {
        if (biomes == null || string.IsNullOrWhiteSpace(biomeId))
            return -1;

        for (int i = 0; i < biomes.Count; i++)
        {
            var b = biomes[i];
            if (b != null && b.biomeId == biomeId)
                return i;
        }

        return -1;
    }
}