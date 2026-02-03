using UnityEngine;
using UnityEngine.UIElements;

public class EnvironmentManager : MonoBehaviour
{
    public static EnvironmentManager Instance { get; private set; }

    [Header("Managers")]
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
    [SerializeField] private WaveField waveField;

    public CelestialBodyManager CelestialBodyManager => celestialManager;
    public WeatherManager WeatherManager => weatherManager;

    public ITimeOfDayService Time { get; private set; }
    public IBrightnessService Brightness { get; private set; }
    public ISunriseSunsetService SunriseSunset { get; private set; }
    public IWeatherService Weather { get; private set; }
    public ICloudService Cloud { get; private set; }

    private void Awake()
    {
        // Singleton guard
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Null checks
        if (!timeOfDayManager) Debug.LogError("TimeOfDayManager not assigned in EnvironmentManager!");
        if (!globalBrightnessManager) Debug.LogError("GlobalBrightnessManager not assigned!");
        if (!sunriseSunsetManager) Debug.LogError("SunriseSunsetOverlayManager not assigned!");
        if (!cloudManager) Debug.LogError("CloudManager not assigned!");
        if (!celestialManager) Debug.LogError("CelestialBodyManager not assigned!");
        if (!fogManager) Debug.LogError("FogManager not assigned!");
        if (!skyVisualManager) Debug.LogError("SkyVisualManager not assigned!");
        if (!waterVisualManager) Debug.LogError("WaterVisualManager not assigned!");
        if (!rainManager) Debug.LogError("RainManager not assigned!");
        if (!windManager) Debug.LogError("WindManager not assigned!");
        if (!weatherManager) Debug.LogError("WeatherManager not assigned!");
        if (!waveManager) Debug.LogError("WaveManager not assigned!");
        if (!waveField) Debug.LogError("WaveField not assigned!");

        // Define what can be subscribed TO
        Time = timeOfDayManager;
        Brightness = globalBrightnessManager;
        SunriseSunset = sunriseSunsetManager;
        Weather = weatherManager;
        Cloud = cloudManager;

        // Initialize subscribers
        globalBrightnessManager.Initialize(Time);
        sunriseSunsetManager.Initialize(Time, Cloud);
        cloudManager.Initialize(Brightness, SunriseSunset, Weather);
        celestialManager.Initialize(Time);
        fogManager.Initialize(Weather);
        skyVisualManager.Initialize(Time, Brightness);
        waterVisualManager.Initialize(Brightness);
        rainManager.Initialize(Weather);
        windManager.Initialize(Weather);
        waveManager.Initialize(Weather, waveField);
    }

    private void Update()
    {
        if (Time != null)
            Time.Tick(UnityEngine.Time.deltaTime);
    }
}
