using UnityEngine;

/// <summary>
/// Runtime-only rasterized visualization of a CelestialField for the world-map cartridge.
/// The textures are merely presentation cache; celestial truth remains in CelestialField.
/// </summary>
public sealed class CelestialMapTextureSet
{
    public Rect WorldBounds { get; }
    public int Width { get; }
    public int Height { get; }

    public Texture2D AmbientStars { get; }
    public Texture2D LandmarkStars { get; }
    public Texture2D Nebulae { get; }
    public Texture2D DeepSkyObjects { get; }

    public int AmbientCount { get; }
    public int LandmarkCount { get; }
    public int NebulaCount { get; }
    public int DeepSkyCount { get; }

    public int TotalCount => AmbientCount + LandmarkCount + NebulaCount + DeepSkyCount;

    public bool IsValid =>
        Width > 0 &&
        Height > 0 &&
        WorldMapCoordinateSpace.IsValidBounds(WorldBounds) &&
        AmbientStars != null &&
        LandmarkStars != null &&
        Nebulae != null &&
        DeepSkyObjects != null;

    public CelestialMapTextureSet(
        Rect worldBounds,
        int width,
        int height,
        Texture2D ambientStars,
        Texture2D landmarkStars,
        Texture2D nebulae,
        Texture2D deepSkyObjects,
        int ambientCount,
        int landmarkCount,
        int nebulaCount,
        int deepSkyCount)
    {
        WorldBounds = worldBounds;
        Width = width;
        Height = height;
        AmbientStars = ambientStars;
        LandmarkStars = landmarkStars;
        Nebulae = nebulae;
        DeepSkyObjects = deepSkyObjects;
        AmbientCount = ambientCount;
        LandmarkCount = landmarkCount;
        NebulaCount = nebulaCount;
        DeepSkyCount = deepSkyCount;
    }
}
