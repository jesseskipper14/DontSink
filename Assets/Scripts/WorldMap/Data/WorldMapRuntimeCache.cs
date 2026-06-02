using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldMapRuntimeCache : MonoBehaviour
{
    public static WorldMapRuntimeCache I { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    [Header("Cached Topography")]
    [SerializeField] private bool hasTopography;
    [SerializeField] private string sourceReason;
    [SerializeField] private int seed;
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private Rect worldBounds;
    [SerializeField] private float effectiveSeaLevel01;

    [Header("Cached Runtime Textures")]
    [SerializeField] private Texture2D baseTexture;
    [SerializeField] private Texture2D contourTexture;
    [SerializeField] private Texture2D debugTexture;
    [SerializeField] private Texture2D classificationTexture;
    [SerializeField] private Texture2D biomeTexture;

    private bool _ownsBaseTexture;
    private bool _ownsContourTexture;
    private bool _ownsDebugTexture;
    private bool _ownsClassificationTexture;
    private bool _ownsBiomeTexture;

    public bool HasTopography => hasTopography && Field != null && Field.IsValid;

    public string SourceReason => sourceReason;
    public WorldMapTopographyField Field { get; private set; }
    public float EffectiveSeaLevel01 => effectiveSeaLevel01;
    public WorldMapTopographyStats Stats { get; private set; }

    public Texture2D BaseTexture => baseTexture;
    public Texture2D ContourTexture => contourTexture;
    public Texture2D DebugTexture => debugTexture;
    public Texture2D ClassificationTexture => classificationTexture;

    public WorldMapBiomeLayer BiomeLayer { get; private set; }
    public Texture2D BiomeTexture => biomeTexture;

    public static WorldMapRuntimeCache Ensure()
    {
        if (I != null)
            return I;

        WorldMapRuntimeCache existing =
            FindAnyObjectByType<WorldMapRuntimeCache>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.AcceptSingleton();
            return existing;
        }

        GameObject go = new GameObject("WorldMapRuntimeCache");
        WorldMapRuntimeCache cache = go.AddComponent<WorldMapRuntimeCache>();
        cache.AcceptSingleton();
        return cache;
    }

    private void Awake()
    {
        AcceptSingleton();
    }

    private void AcceptSingleton()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StoreTopography(
        WorldMapTopographyField field,
        float seaLevel01,
        WorldMapTopographyStats stats,
        Texture2D baseTex,
        bool ownsBase,
        Texture2D contourTex,
        bool ownsContour,
        Texture2D debugTex,
        bool ownsDebug,
        Texture2D classificationTex,
        bool ownsClassification,
        WorldMapBiomeLayer biomeLayer,
        Texture2D biomeTex,
        bool ownsBiome,
        string reason)
    {
        if (field == null || !field.IsValid)
        {
            Debug.LogWarning("[WorldMapRuntimeCache] Ignored invalid topography store request.", this);
            return;
        }

        ReplaceTexture(ref baseTexture, ref _ownsBaseTexture, baseTex, ownsBase);
        ReplaceTexture(ref contourTexture, ref _ownsContourTexture, contourTex, ownsContour);
        ReplaceTexture(ref debugTexture, ref _ownsDebugTexture, debugTex, ownsDebug);
        ReplaceTexture(ref classificationTexture, ref _ownsClassificationTexture, classificationTex, ownsClassification);
        ReplaceTexture(ref biomeTexture, ref _ownsBiomeTexture, biomeTex, ownsBiome);

        Field = field;
        Stats = stats;
        BiomeLayer = biomeLayer;

        effectiveSeaLevel01 = seaLevel01;
        sourceReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason;

        seed = field.Seed;
        width = field.Width;
        height = field.Height;
        worldBounds = field.WorldBounds;

        hasTopography = true;

        if (verboseLogging)
        {
            Debug.Log(
                $"[WorldMapRuntimeCache] Stored topography from '{sourceReason}'. " +
                $"Seed={seed}, Res={width}x{height}, Sea={effectiveSeaLevel01:0.000}, " +
                $"Base={(baseTexture != null)}, Contour={(contourTexture != null)}, " +
                $"Debug={(debugTexture != null)}, Class={(classificationTexture != null)}, Biome={(biomeTexture != null)}",
                this
            );
        }
    }

    public void Clear(bool destroyOwnedTextures = true)
    {
        if (destroyOwnedTextures)
        {
            DestroyOwnedTexture(ref baseTexture, ref _ownsBaseTexture);
            DestroyOwnedTexture(ref contourTexture, ref _ownsContourTexture);
            DestroyOwnedTexture(ref debugTexture, ref _ownsDebugTexture);
            DestroyOwnedTexture(ref classificationTexture, ref _ownsClassificationTexture);
            DestroyOwnedTexture(ref biomeTexture, ref _ownsBiomeTexture);
        }
        else
        {
            baseTexture = null;
            contourTexture = null;
            debugTexture = null;
            classificationTexture = null;
            biomeTexture = null;

            _ownsBaseTexture = false;
            _ownsContourTexture = false;
            _ownsDebugTexture = false;
            _ownsClassificationTexture = false;
            _ownsBiomeTexture = false;
        }

        hasTopography = false;
        sourceReason = null;
        Field = null;
        Stats = default;
        BiomeLayer = null;
        effectiveSeaLevel01 = 0f;
        seed = 0;
        width = 0;
        height = 0;
        worldBounds = default;

        if (verboseLogging)
            Debug.Log("[WorldMapRuntimeCache] Cleared.", this);
    }

    [ContextMenu("Log Cache Summary")]
    private void LogCacheSummary()
    {
        Debug.Log(
            $"[WorldMapRuntimeCache] HasTopography={HasTopography}, Source='{sourceReason}', " +
            $"Seed={seed}, Res={width}x{height}, Sea={effectiveSeaLevel01:0.000}, " +
            $"Base={(baseTexture != null)}, Contour={(contourTexture != null)}, Debug={(debugTexture != null)}, " +
            $"Class={(classificationTexture != null)}, Biome={(biomeTexture != null)}",
            this
        );
    }

    [ContextMenu("Clear Cache")]
    private void ContextClear()
    {
        Clear(destroyOwnedTextures: true);
    }

    private void OnDestroy()
    {
        if (I == this)
            I = null;

        Clear(destroyOwnedTextures: true);
    }

    private void ReplaceTexture(
        ref Texture2D current,
        ref bool ownsCurrent,
        Texture2D next,
        bool ownsNext)
    {
        if (current != null && current != next && ownsCurrent)
            DestroyTexture(current);

        current = next;
        ownsCurrent = next != null && ownsNext;
    }

    private static void DestroyOwnedTexture(ref Texture2D texture, ref bool owns)
    {
        if (texture != null && owns)
            DestroyTexture(texture);

        texture = null;
        owns = false;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture == null)
            return;

        if (Application.isPlaying)
            Destroy(texture);
        else
            DestroyImmediate(texture);
    }
}
