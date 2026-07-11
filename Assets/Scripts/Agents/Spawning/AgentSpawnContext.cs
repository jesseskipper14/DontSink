using System;
using UnityEngine;

public sealed class AgentSpawnContext
{
    public string SceneName { get; }
    public int Seed { get; }
    public System.Random Rng { get; }
    public Transform SpawnerRoot { get; }
    public Transform SpawnedRoot { get; }

    public AgentSpawnContext(
        string sceneName,
        int seed,
        Transform spawnerRoot,
        Transform spawnedRoot)
    {
        SceneName = sceneName ?? "";
        Seed = seed;
        Rng = new System.Random(seed);
        SpawnerRoot = spawnerRoot;
        SpawnedRoot = spawnedRoot;
    }

    public int NextSeed()
    {
        return Rng.Next(int.MinValue, int.MaxValue);
    }

    public float NextFloat(float minInclusive, float maxInclusive)
    {
        return Mathf.Lerp(minInclusive, maxInclusive, (float)Rng.NextDouble());
    }

    public int NextIntInclusive(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
            return minInclusive;

        return Rng.Next(minInclusive, maxInclusive + 1);
    }
}