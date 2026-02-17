using System;
using UnityEngine;

public class RainManager : MonoBehaviour, IRainService
{
    [Header("Rain State")]
    [Range(0f, 1f)][SerializeField] private float currentDropDensity;
    [Range(0f, 1f)][SerializeField] private float currentFallSpeed;

    // Transition state (separate!)
    private float startDropDensity;
    private float targetDropDensity;
    private float dropDensityElapsed;
    private float dropDensityDuration;
    private bool transitioningDropDensity;

    private float startFallSpeed;
    private float targetFallSpeed;
    private float fallSpeedElapsed;
    private float fallSpeedDuration;
    private bool transitioningFallSpeed;

    private IWeatherService weatherService;

    // =========================
    // IRainService
    // =========================
    public float RainDropDensity => currentDropDensity;
    public float RainFallSpeed => currentFallSpeed;

    public event Action<float> OnRainDropDensityChanged;
    public event Action<float> OnRainFallSpeedChanged;

    // =========================
    // Initialization
    // =========================
    public void Initialize(IWeatherService weather)
    {
        weatherService = weather;

        weatherService.OnRainDropDensityChanged += HandleDropDensityFromWeather;
        weatherService.OnRainFallSpeedChanged += HandleFallSpeedFromWeather;

        // Sync initial state
        SetDropDensityImmediate(weather.RainDropDensity);
        SetFallSpeedImmediate(weather.RainFallSpeed);
    }

    private void OnDestroy()
    {
        if (weatherService == null) return;

        weatherService.OnRainDropDensityChanged -= HandleDropDensityFromWeather;
        weatherService.OnRainFallSpeedChanged -= HandleFallSpeedFromWeather;
    }

    private void Update()
    {
        UpdateDropDensity();
        UpdateFallSpeed();
    }

    // =========================
    // Weather hooks
    // =========================
    private void HandleDropDensityFromWeather(float value, float duration)
    {
        SetRainDropDensity(value, duration);
    }

    private void HandleFallSpeedFromWeather(float value, float duration)
    {
        SetRainFallSpeed(value, duration);
    }

    // =========================
    // Transitions
    // =========================
    private void UpdateDropDensity()
    {
        if (!transitioningDropDensity) return;

        dropDensityElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(dropDensityElapsed / dropDensityDuration);

        currentDropDensity = Mathf.Lerp(startDropDensity, targetDropDensity, t);
        OnRainDropDensityChanged?.Invoke(currentDropDensity);

        if (t >= 1f)
            transitioningDropDensity = false;
    }

    private void UpdateFallSpeed()
    {
        if (!transitioningFallSpeed) return;

        fallSpeedElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(fallSpeedElapsed / fallSpeedDuration);

        currentFallSpeed = Mathf.Lerp(startFallSpeed, targetFallSpeed, t);
        OnRainFallSpeedChanged?.Invoke(currentFallSpeed);

        if (t >= 1f)
            transitioningFallSpeed = false;
    }

    // =========================
    // API
    // =========================
    public void SetRainDropDensity(float value, float duration)
    {
        startDropDensity = currentDropDensity;
        targetDropDensity = Mathf.Clamp01(value);

        dropDensityDuration = Mathf.Max(0.001f, duration);
        dropDensityElapsed = 0f;
        transitioningDropDensity = true;
    }

    public void SetRainFallSpeed(float value, float duration)
    {
        startFallSpeed = currentFallSpeed;
        targetFallSpeed = Mathf.Clamp01(value);

        fallSpeedDuration = Mathf.Max(0.001f, duration);
        fallSpeedElapsed = 0f;
        transitioningFallSpeed = true;
    }

    public void SetDropDensityImmediate(float value)
    {
        currentDropDensity = Mathf.Clamp01(value);
        transitioningDropDensity = false;
        OnRainDropDensityChanged?.Invoke(currentDropDensity);
    }

    public void SetFallSpeedImmediate(float value)
    {
        currentFallSpeed = Mathf.Clamp01(value);
        transitioningFallSpeed = false;
        OnRainFallSpeedChanged?.Invoke(currentFallSpeed);
    }
}
