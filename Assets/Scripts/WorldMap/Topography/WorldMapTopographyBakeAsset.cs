using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/Topography/Baked Topography",
    fileName = "BakedWorldMapTopography")]
public sealed class WorldMapTopographyBakeAsset : ScriptableObject
{
    [Header("Identity")]
    public int worldSeed;
    public string settingsFingerprint;

    [Header("Source Info")]
    public WorldMapTopographySettings settingsSource;

    [Header("Field")]
    public int width;
    public int height;
    public Rect worldBounds;
    public float minRaw;
    public float maxRaw;

    [SerializeField] private float[] height01;

    [Header("Textures")]
    [SerializeField] private Texture2D baseTexture;
    [SerializeField] private Texture2D contourTexture;

    [Header("Resolved Interpretation")]
    public float effectiveSeaLevel01;
    public WorldMapTopographyStats stats;

    [Header("Classification")]
    [SerializeField] private Texture2D classificationTexture;
    public Texture2D ClassificationTexture => classificationTexture;
    public bool HasClassificationTexture => classificationTexture != null;

    [Header("Debug Texture")]
    [Tooltip("Legacy/full debug texture with contours baked in.")]
    [SerializeField] private Texture2D debugTexture;

    public Texture2D BaseTexture => baseTexture != null ? baseTexture : debugTexture;
    public Texture2D ContourTexture => contourTexture;
    public Texture2D DebugTexture => debugTexture;

    public bool HasBaseTexture => BaseTexture != null;
    public bool HasContourTexture => contourTexture != null;
    public bool HasDebugTexture => debugTexture != null;

    public bool IsValid =>
        width > 0 &&
        height > 0 &&
        height01 != null &&
        height01.Length == width * height;

    public bool Matches(int seed, WorldMapTopographySettings settings)
    {
        if (!IsValid)
            return false;

        if (worldSeed != seed)
            return false;

        string fp = WorldMapTopographyFingerprint.Build(seed, settings);
        return settingsFingerprint == fp;
    }

    public WorldMapTopographyField ToField()
    {
        if (!IsValid)
            return null;

        var copy = new float[height01.Length];
        System.Array.Copy(height01, copy, height01.Length);

        return new WorldMapTopographyField(
            worldSeed,
            width,
            height,
            worldBounds,
            copy,
            minRaw,
            maxRaw
        );
    }

    public void Store(
        int seed,
        WorldMapTopographySettings settings,
        WorldMapTopographyField field,
        float effectiveSeaLevel,
        WorldMapTopographyStats computedStats,
        Texture2D baseTex,
        Texture2D contourTex,
        Texture2D debugTex,
        Texture2D classificationTex)
    {
        if (field == null || !field.IsValid)
        {
            Debug.LogError("[WorldMapTopographyBakeAsset] Cannot store invalid field.", this);
            return;
        }

        worldSeed = seed;
        settingsSource = settings;
        settingsFingerprint = WorldMapTopographyFingerprint.Build(seed, settings);

        width = field.Width;
        height = field.Height;
        worldBounds = field.WorldBounds;
        minRaw = field.MinRaw;
        maxRaw = field.MaxRaw;

        height01 = field.CopyHeight01();

        baseTexture = baseTex;
        contourTexture = contourTex;
        debugTexture = debugTex;

        effectiveSeaLevel01 = effectiveSeaLevel;
        stats = computedStats;
        classificationTexture = classificationTex;
    }
}