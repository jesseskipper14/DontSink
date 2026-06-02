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

    [Header("Packed Height Data")]
    [SerializeField] private string heightEncoding;
    [SerializeField] private string heightU16Base64;
    [SerializeField] private int heightQuantizationBits;
    [SerializeField] private int heightSampleCount;

    [Header("Legacy Height Data")]
    [Tooltip("Legacy fallback only. New bakes use packed ushort Base64 instead of raw float arrays.")]
    [SerializeField] private float[] height01;

    [Header("Optional Cached Textures")]
    [Tooltip("Player-facing base map texture. Keep this on for fast map display.")]
    [SerializeField] private Texture2D baseTexture;

    [Tooltip("Optional overlay cache. Usually leave off and rebuild lazily.")]
    [SerializeField] private Texture2D contourTexture;

    [Header("Resolved Interpretation")]
    public float effectiveSeaLevel01;
    public WorldMapTopographyStats stats;

    [Header("Optional Classification Cache")]
    [Tooltip("Optional debug overlay cache. Usually leave off and rebuild lazily.")]
    [SerializeField] private Texture2D classificationTexture;

    [Header("Optional Debug Texture Cache")]
    [Tooltip("Legacy/full debug texture with contours baked in. Usually do not bake this.")]
    [SerializeField] private Texture2D debugTexture;

    public string HeightEncoding => heightEncoding;
    public int HeightQuantizationBits => heightQuantizationBits;
    public int HeightSampleCount => heightSampleCount;
    public bool HasPackedHeightData =>
        width > 0 &&
        height > 0 &&
        heightSampleCount == width * height &&
        !string.IsNullOrWhiteSpace(heightU16Base64);

    public bool HasLegacyHeightData =>
        width > 0 &&
        height > 0 &&
        height01 != null &&
        height01.Length == width * height;

    public Texture2D BaseTexture => baseTexture != null ? baseTexture : debugTexture;
    public Texture2D ContourTexture => contourTexture;
    public Texture2D DebugTexture => debugTexture;
    public Texture2D ClassificationTexture => classificationTexture;

    public bool HasBaseTexture => BaseTexture != null;
    public bool HasContourTexture => contourTexture != null;
    public bool HasDebugTexture => debugTexture != null;
    public bool HasClassificationTexture => classificationTexture != null;

    public bool IsValid =>
        width > 0 &&
        height > 0 &&
        (HasPackedHeightData || HasLegacyHeightData);

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

        float[] heights = CopyHeight01();
        if (heights == null || heights.Length != width * height)
            return null;

        return new WorldMapTopographyField(
            worldSeed,
            width,
            height,
            worldBounds,
            heights,
            minRaw,
            maxRaw
        );
    }

    public float[] CopyHeight01()
    {
        int expected = width * height;

        if (HasPackedHeightData)
            return WorldMapTopographyHeightCodec.DecodeUShortBase64(heightU16Base64, expected);

        if (HasLegacyHeightData)
        {
            var copy = new float[height01.Length];
            System.Array.Copy(height01, copy, height01.Length);
            return copy;
        }

        return null;
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

        StorePackedHeightData(field.CopyHeight01());

        baseTexture = baseTex;
        contourTexture = contourTex;
        debugTexture = debugTex;
        classificationTexture = classificationTex;

        effectiveSeaLevel01 = effectiveSeaLevel;
        stats = computedStats;
    }

    private void StorePackedHeightData(float[] heights)
    {
        int expected = width * height;

        // Never keep the legacy float array on new bakes. It is save-file goblin food.
        height01 = null;

        if (heights == null || heights.Length != expected)
        {
            heightEncoding = null;
            heightU16Base64 = null;
            heightQuantizationBits = 0;
            heightSampleCount = 0;
            return;
        }

        heightEncoding = WorldMapTopographyHeightCodec.UShortBase64Encoding;
        heightQuantizationBits = 16;
        heightSampleCount = heights.Length;
        heightU16Base64 = WorldMapTopographyHeightCodec.EncodeUShortBase64(heights);
    }
}
