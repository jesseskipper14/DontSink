using UnityEngine;
using UnityEngine.SceneManagement;

public class ServiceRoot : MonoBehaviour
{
    public static ServiceRoot Instance { get; private set; }


    [Header("Managers (scene or prefab wired)")]
    [SerializeField] private CelestialBodyManager celestialManager;
    [SerializeField] private CloudManager cloudManager;
    [SerializeField] private FogManager fogManager;
    [SerializeField] private GlobalBrightnessManager globalBrightnessManager;
    [SerializeField] private RainManager rainManager;
    [SerializeField] private SkyVisualManager skyVisualManager;
    [SerializeField] private SunriseSunsetOverlayManager sunriseSunsetManager;
    [SerializeField] private TimeOfDayManager timeOfDayManager;
    [SerializeField] private WaterVisualManager waterVisualManager;
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private WindManager windManager;
    [SerializeField] private WaveManager waveManager;

    [Header("Scene-only deps (optional)")]
    [SerializeField] private WaveField waveField;

    [Header("Behavior")]
    [Tooltip("If true, ServiceRoot ticks time. If false, an external driver (e.g. TimeOfDayDriver) must tick it.")]
    [SerializeField] private bool tickTimeInRoot = false;

    public CelestialBodyManager CelestialBodyManager => celestialManager;
    public WeatherManager WeatherManager => weatherManager;

    public ITimeOfDayService Time { get; private set; }
    public IBrightnessService Brightness { get; private set; }
    public ISunriseSunsetService SunriseSunset { get; private set; }
    public IWeatherService Weather { get; private set; }
    public ICloudService Cloud { get; private set; }

    private bool _initialized;

    private void Awake()
    {
        // Singleton guard
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Try auto-wire missing refs from children (helps prefab + scene variants)
        AutoWireIfMissing();

        // Define what can be subscribed TO (services)
        Time = timeOfDayManager;
        Brightness = globalBrightnessManager;
        SunriseSunset = sunriseSunsetManager;
        Weather = weatherManager;
        Cloud = cloudManager;

        // Hard requirements (services). If missing, we can't safely proceed.
        if (Time == null) { WarnOnce("TimeOfDayManager missing (Time service)."); return; }
        if (Brightness == null) { WarnOnce("GlobalBrightnessManager missing (Brightness service)."); return; }
        if (SunriseSunset == null) { WarnOnce("SunriseSunsetOverlayManager missing (SunriseSunset service)."); return; }
        if (Weather == null) { WarnOnce("WeatherManager missing (Weather service)."); return; }
        if (Cloud == null) { WarnOnce("CloudManager missing (Cloud service)."); return; }

        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        // Subscribers: only initialize if the component exists.
        // This makes map-view scenes safe when visuals aren't present.

        if (globalBrightnessManager != null)
            globalBrightnessManager.Initialize(Time);

        if (sunriseSunsetManager != null)
            sunriseSunsetManager.Initialize(Time, Cloud);

        if (cloudManager != null)
            cloudManager.Initialize(Brightness, SunriseSunset, Weather);

        if (celestialManager != null)
            celestialManager.Initialize(Time);

        if (fogManager != null)
            fogManager.Initialize(Weather);

        if (skyVisualManager != null)
            skyVisualManager.Initialize(Time, Brightness);

        if (waterVisualManager != null)
            waterVisualManager.Initialize(Brightness);

        if (rainManager != null)
            rainManager.Initialize(Weather);

        if (windManager != null)
            windManager.Initialize(Weather);

        // WaveField is scene-only; wave manager should no-op if absent
        if (waveManager != null)
        {
            if (waveField != null)
                waveManager.Initialize(Weather, waveField);
            else
                WarnOnce("WaveField missing (waves will be disabled in this scene).");
        }
    }

    private void Update()
    {
        if (!tickTimeInRoot) return;

        if (Time != null)
            Time.Tick(UnityEngine.Time.deltaTime);
    }

    private void AutoWireIfMissing()
    {
        // Prefer serialized refs, but allow prefab variants / scene variants.
        // This also makes it easier for mods to inject replacements in child hierarchy.

        if (timeOfDayManager == null) timeOfDayManager = GetComponentInChildren<TimeOfDayManager>(true);
        if (globalBrightnessManager == null) globalBrightnessManager = GetComponentInChildren<GlobalBrightnessManager>(true);
        if (sunriseSunsetManager == null) sunriseSunsetManager = GetComponentInChildren<SunriseSunsetOverlayManager>(true);
        if (cloudManager == null) cloudManager = GetComponentInChildren<CloudManager>(true);
        if (celestialManager == null) celestialManager = GetComponentInChildren<CelestialBodyManager>(true);
        if (fogManager == null) fogManager = GetComponentInChildren<FogManager>(true);
        if (skyVisualManager == null) skyVisualManager = GetComponentInChildren<SkyVisualManager>(true);
        if (waterVisualManager == null) waterVisualManager = GetComponentInChildren<WaterVisualManager>(true);
        if (rainManager == null) rainManager = GetComponentInChildren<RainManager>(true);
        if (windManager == null) windManager = GetComponentInChildren<WindManager>(true);
        if (weatherManager == null) weatherManager = GetComponentInChildren<WeatherManager>(true);
        if (waveManager == null) waveManager = GetComponentInChildren<WaveManager>(true);

        // WaveField is scene-only. If it's not set, we'll try find one in scene.
        if (waveField == null) waveField = FindAnyObjectByType<WaveField>();
    }

    private bool _warnedTime;
    private bool _warnedBrightness;
    private bool _warnedSunrise;
    private bool _warnedWeather;
    private bool _warnedCloud;
    private bool _warnedWaves;

    private void WarnOnce(string msg)
    {
        // Tiny “warn once” helper without introducing a full logging system.
        // Match messages to flags crudely.
        if (msg.Contains("Time") && _warnedTime) return;
        if (msg.Contains("Brightness") && _warnedBrightness) return;
        if (msg.Contains("Sunrise") && _warnedSunrise) return;
        if (msg.Contains("WeatherManager") && _warnedWeather) return;
        if (msg.Contains("Cloud") && _warnedCloud) return;
        if (msg.Contains("WaveField") && _warnedWaves) return;

        Debug.LogWarning($"[ServiceRoot] {msg}");

        if (msg.Contains("Time")) _warnedTime = true;
        if (msg.Contains("Brightness")) _warnedBrightness = true;
        if (msg.Contains("Sunrise")) _warnedSunrise = true;
        if (msg.Contains("WeatherManager")) _warnedWeather = true;
        if (msg.Contains("Cloud")) _warnedCloud = true;
        if (msg.Contains("WaveField")) _warnedWaves = true;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-find scene context
        var ctx = SceneContext.Current != null ? SceneContext.Current : FindAnyObjectByType<SceneContext>();

        if (ctx != null)
        {
            // WaveField
            waveField = ctx.waveField != null ? ctx.waveField : FindAnyObjectByType<WaveField>();
            if (waveManager != null)
            {
                if (waveField != null) waveManager.Initialize(Weather, waveField);
                else WarnOnce("WaveField missing (waves will be disabled in this scene).");
            }

            // Sun/Moon anchors
            if (celestialManager != null)
            {
                celestialManager.RebindSceneAnchors(
                    ctx.sunTransform, ctx.sunLight,
                    ctx.moonTransform, ctx.moonLight,
                    ctx.sunCorona, ctx.coronaMaterial);
            }

            if (waterVisualManager != null)
            {
                waterVisualManager.RebindSceneAnchors(
                    ctx.seaRenderer,
                    ctx.sideWaterRenderer
                );
            }


            // Clouds
            if (cloudManager != null)
            {
                cloudManager.RebindSceneAnchors(ctx.cloudRenderer, ctx.cloudMaterialOverride);
            }

            // Sky + stars
            if (skyVisualManager != null)
            {
                skyVisualManager.RebindSceneAnchors(ctx.starsRenderer, ctx.skyMaterialOverride, ctx.starsMaterialOverride);
            }

            // Sunrise/Sunset overlay
            if (sunriseSunsetManager != null)
            {
                sunriseSunsetManager.RebindSceneAnchors(ctx.sunriseOverlayRenderer, ctx.sunriseOverlayMaterialOverride);
            }
        }
        else
        {
            WarnOnce("SceneContext missing (scene bindings not applied).");
        }
    }
}
