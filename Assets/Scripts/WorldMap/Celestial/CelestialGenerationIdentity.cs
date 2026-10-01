using System;
using UnityEngine;

/// <summary>
/// Persistable identity for a generated celestial field. The config itself should
/// also be persisted once celestial data is wired into the world save.
/// </summary>
[Serializable]
public sealed class CelestialGenerationIdentity
{
    public int identityVersion = 1;
    public int generatorVersion;
    public int worldSeed;
    public string configHash;

    public float worldBoundsX;
    public float worldBoundsY;
    public float worldBoundsWidth;
    public float worldBoundsHeight;

    public Rect WorldBounds => new Rect(
        worldBoundsX,
        worldBoundsY,
        worldBoundsWidth,
        worldBoundsHeight);

    public bool IsValid =>
        identityVersion > 0 &&
        generatorVersion > 0 &&
        worldBoundsWidth > 0f &&
        worldBoundsHeight > 0f &&
        !string.IsNullOrWhiteSpace(configHash);

    public static CelestialGenerationIdentity Create(
        int worldSeed,
        Rect worldBounds,
        CelestialGenerationConfig config)
    {
        return new CelestialGenerationIdentity
        {
            identityVersion = 1,
            generatorVersion = CelestialFieldGenerator.CurrentGeneratorVersion,
            worldSeed = worldSeed,
            configHash = CelestialGenerationFingerprint.Build(config),
            worldBoundsX = worldBounds.x,
            worldBoundsY = worldBounds.y,
            worldBoundsWidth = worldBounds.width,
            worldBoundsHeight = worldBounds.height
        };
    }
}
