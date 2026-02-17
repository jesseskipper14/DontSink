using System;
using UnityEngine;

/// <summary>
/// Controls the wave system using subscription to WeatherManager.
/// Linearly transitions wave amplitude, frequency, and speed.
/// </summary>
public class WaveManager : MonoBehaviour, IWaveService
{
    [Header("Wave State")]
    [SerializeField] private float amplitude = 0.1f;
    [SerializeField] private float frequency = 0.3f;
    [SerializeField] private float speed = 0.5f;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugSetWave;
    [SerializeField] private float debugAmplitude = 0.5f;
    [SerializeField] private float debugFrequency = 0.1f;
    [SerializeField] private float debugSpeed = 0.8f;

    // =========================
    // Linear transition state
    // =========================
    private float ampStart, ampTarget, ampElapsed;
    private float freqStart, freqTarget, freqElapsed;
    private float speedStart, speedTarget, speedElapsed;

    private bool ampTransitioning;
    private bool freqTransitioning;
    private bool speedTransitioning;

    private float lastSentAmplitude = -1f;
    private float lastSentFrequency = -1f;
    private float lastSentSpeed = -1f;

    private IWeatherService weatherService;
    private WaveField waveField;

    // =========================
    // IWaveService
    // =========================
    public float Amplitude => amplitude;
    public float Frequency => frequency;
    public float Speed => speed;

    private Action<float> onAmplitudeChanged;
    private Action<float> onFrequencyChanged;
    private Action<float> onSpeedChanged;

    public event Action<float> OnAmplitudeChanged
    {
        add
        {
            onAmplitudeChanged += value;
            value?.Invoke(amplitude);
        }
        remove { onAmplitudeChanged -= value; }
    }

    public event Action<float> OnFrequencyChanged
    {
        add
        {
            onFrequencyChanged += value;
            value?.Invoke(frequency);
        }
        remove { onFrequencyChanged -= value; }
    }

    public event Action<float> OnSpeedChanged
    {
        add
        {
            onSpeedChanged += value;
            value?.Invoke(speed);
        }
        remove { onSpeedChanged -= value; }
    }

    // =========================
    // Initialization
    // =========================
    public void Initialize(IWeatherService weather, WaveField wave)
    {
        weatherService = weather;
        waveField = wave;

        weatherService.OnWaveAmplitudeChanged += OnWeatherAmplitudeChanged;
        weatherService.OnWaveFrequencyChanged += OnWeatherFrequencyChanged;
        weatherService.OnWaveSpeedChanged += OnWeatherSpeedChanged;

        // Sync immediately
        SetAmplitudeImmediate(weatherService.WaveAmplitude);
        SetFrequencyImmediate(weatherService.WaveFrequency);
        SetSpeedImmediate(weatherService.WaveSpeed);
    }

    private void OnDestroy()
    {
        if (weatherService != null)
        {
            weatherService.OnWaveAmplitudeChanged -= OnWeatherAmplitudeChanged;
            weatherService.OnWaveFrequencyChanged -= OnWeatherFrequencyChanged;
            weatherService.OnWaveSpeedChanged -= OnWeatherSpeedChanged;
        }
    }

    private void Awake()
    {
        ampTarget = amplitude;
        freqTarget = frequency;
        speedTarget = speed;
    }

    private void Update()
    {
        UpdateAmplitude();
        UpdateFrequency();
        UpdateSpeed();

        // Apply to wave field
        if (waveField != null)
        {
            waveField.amplitude = amplitude;
            waveField.frequency = frequency;
            waveField.speed = speed;
        }

        // Debug triggers
        if (debugSetWave)
        {
            debugSetWave = false;
            SetAmplitude(debugAmplitude, transitionDuration);
            SetFrequency(debugFrequency, transitionDuration);
            SetSpeed(debugSpeed, transitionDuration);
        }
    }

    // =========================
    // Update helpers
    // =========================
    private void UpdateAmplitude()
    {
        if (!ampTransitioning) return;

        ampElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(ampElapsed / transitionDuration);
        amplitude = Mathf.Lerp(ampStart, ampTarget, t);

        if (!Mathf.Approximately(amplitude, lastSentAmplitude))
        {
            lastSentAmplitude = amplitude;
            onAmplitudeChanged?.Invoke(amplitude);
        }

        if (t >= 1f)
            ampTransitioning = false;
    }

    private void UpdateFrequency()
    {
        if (!freqTransitioning) return;

        freqElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(freqElapsed / transitionDuration);
        frequency = Mathf.Lerp(freqStart, freqTarget, t);

        if (!Mathf.Approximately(frequency, lastSentFrequency))
        {
            lastSentFrequency = frequency;
            onFrequencyChanged?.Invoke(frequency);
        }

        if (t >= 1f)
            freqTransitioning = false;
    }

    private void UpdateSpeed()
    {
        if (!speedTransitioning) return;

        speedElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(speedElapsed / transitionDuration);
        speed = Mathf.Lerp(speedStart, speedTarget, t);

        if (!Mathf.Approximately(speed, lastSentSpeed))
        {
            lastSentSpeed = speed;
            onSpeedChanged?.Invoke(speed);
        }

        if (t >= 1f)
            speedTransitioning = false;
    }

    // =========================
    // API - smooth setters
    // =========================
    public void SetAmplitude(float value, float time)
    {
        ampStart = amplitude;
        ampTarget = Mathf.Clamp(value, 0f, 20f);
        transitionDuration = Mathf.Max(0.001f, time);
        ampElapsed = 0f;
        ampTransitioning = true;
    }

    public void SetFrequency(float value, float time)
    {
        freqStart = frequency;
        freqTarget = Mathf.Clamp(value, 0f, 1f);
        transitionDuration = Mathf.Max(0.001f, time);
        freqElapsed = 0f;
        freqTransitioning = true;
    }

    public void SetSpeed(float value, float time)
    {
        speedStart = speed;
        speedTarget = Mathf.Clamp(value, 0f, 1f);
        transitionDuration = Mathf.Max(0.001f, time);
        speedElapsed = 0f;
        speedTransitioning = true;
    }

    // =========================
    // Immediate setters
    // =========================
    public void SetAmplitudeImmediate(float value)
    {
        amplitude = ampTarget = Mathf.Clamp(value, 0f, 20f);
        ampTransitioning = false;
        lastSentAmplitude = amplitude;
        onAmplitudeChanged?.Invoke(amplitude);
    }

    public void SetFrequencyImmediate(float value)
    {
        frequency = freqTarget = Mathf.Clamp(value, 0f, 1f);
        freqTransitioning = false;
        lastSentFrequency = frequency;
        onFrequencyChanged?.Invoke(frequency);
    }

    public void SetSpeedImmediate(float value)
    {
        speed = speedTarget = Mathf.Clamp(value, 0f, 1f);
        speedTransitioning = false;
        lastSentSpeed = speed;
        onSpeedChanged?.Invoke(speed);
    }

    // =========================
    // Weather callbacks
    // =========================
    private void OnWeatherAmplitudeChanged(float value, float duration) =>
        SetAmplitude(value, duration);

    private void OnWeatherFrequencyChanged(float value, float duration) =>
        SetFrequency(value, duration);

    private void OnWeatherSpeedChanged(float value, float duration) =>
        SetSpeed(value, duration);

    // =========================
    // Methods for Renderers
    // =========================
    public float SampleHeight(float worldX) =>
        waveField != null ? waveField.SampleHeight(worldX) : 0f;

    public float SampleHeightAtWorldXWrapped(float worldX) =>
        waveField != null ? waveField.SampleHeightAtWorldXWrapped(worldX) : 0f;

    public float SampleHorizontalVelocity(float worldX) =>
        waveField != null ? waveField.SampleHorizontalVelocity(worldX) : 0f;

    public float SampleSurfaceVelocity(float worldX) =>
        waveField != null ? waveField.SampleSurfaceVelocity(worldX) : 0f;

    public void AddImpulse(float worldX, float totalForce, float radius = 2f)
    {
        if (waveField != null)
            waveField.AddImpulse(worldX, totalForce, radius);
    }
}
