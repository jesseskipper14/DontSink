using System;
using UnityEngine;

public class FogManager : MonoBehaviour, IFogService
{
    [Header("Fog State")]
    [Range(0f, 1f)]
    [SerializeField] private float currentIntensity = 0f;
    [SerializeField] private float currentBrightness = 0f;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugSetFog;
    [Range(0f, 1f)]
    [SerializeField] private float debugTargetIntensity = 0.8f;
    [SerializeField] private float debugTargetBrightness = 0.8f;

    // =========================
    // Linear transition state
    // =========================
    private float intStart, intTarget, intElapsed;
    private float brtStart, brtTarget, brtElapsed;
    private bool intTransitioning;
    private bool brtTransitioning;

    private float lastSentIntensity = -1f;
    private float lastSentBrightness = -1f;

    // =========================
    // IFogService
    // =========================
    public float FogIntensity => currentIntensity;
    public float FogBrightness => currentBrightness;

    public event Action<float> OnFogIntensityChanged;
    public event Action<float> OnFogBrightnessChanged;

    private IWeatherService weatherService;

    // =========================
    // Initialization
    // =========================
    public void Initialize(IWeatherService weather)
    {
        weatherService = weather;
        weatherService.OnFogIntensityChanged += OnWeatherFogIntensityChanged;
        weatherService.OnFogBrightnessChanged += OnWeatherFogBrightnessChanged;

        // Sync initial state
        SetFogIntensityImmediate(weatherService.FogIntensity);
        SetFogBrightnessImmediate(weatherService.FogBrightness);
    }

    private void OnDestroy()
    {
        if (weatherService != null)
        {
            weatherService.OnFogIntensityChanged -= OnWeatherFogIntensityChanged;
            weatherService.OnFogBrightnessChanged -= OnWeatherFogBrightnessChanged;
        }
    }

    private void Awake()
    {
        intTarget = currentIntensity;
        brtTarget = currentBrightness;
    }

    private void Update()
    {
        UpdateIntensity();
        UpdateBrightness();

        // Debug trigger (editor-only use)
        if (debugSetFog)
        {
            debugSetFog = false;
            SetFogInt(debugTargetIntensity, transitionDuration);
            SetFogBrt(debugTargetBrightness, transitionDuration);
        }
    }

    // =========================
    // Update helpers
    // =========================
    private void UpdateIntensity()
    {
        if (!intTransitioning) return;

        intElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(intElapsed / transitionDuration);
        currentIntensity = Mathf.Lerp(intStart, intTarget, t);

        if (!Mathf.Approximately(currentIntensity, lastSentIntensity))
        {
            lastSentIntensity = currentIntensity;
            OnFogIntensityChanged?.Invoke(currentIntensity);
        }

        if (t >= 1f)
            intTransitioning = false;
    }

    private void UpdateBrightness()
    {
        if (!brtTransitioning) return;

        brtElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(brtElapsed / transitionDuration);
        currentBrightness = Mathf.Lerp(brtStart, brtTarget, t);

        if (!Mathf.Approximately(currentBrightness, lastSentBrightness))
        {
            lastSentBrightness = currentBrightness;
            OnFogBrightnessChanged?.Invoke(currentBrightness);
        }

        if (t >= 1f)
            brtTransitioning = false;
    }

    // =========================
    // Weather hooks
    // =========================
    private void OnWeatherFogIntensityChanged(float newFogInt, float duration)
    {
        SetFogInt(newFogInt, duration);
    }

    private void OnWeatherFogBrightnessChanged(float newFogBrt, float duration)
    {
        SetFogBrt(newFogBrt, duration);
    }

    // =========================
    // API
    // =========================
    public void SetFogInt(float intensity, float timeToTarget)
    {
        intStart = currentIntensity;
        intTarget = Mathf.Clamp01(intensity);
        transitionDuration = Mathf.Max(0.001f, timeToTarget);
        intElapsed = 0f;
        intTransitioning = true;
    }

    public void SetFogBrt(float brightness, float timeToTarget)
    {
        brtStart = currentBrightness;
        brtTarget = Mathf.Clamp01(brightness);
        transitionDuration = Mathf.Max(0.001f, timeToTarget);
        brtElapsed = 0f;
        brtTransitioning = true;
    }

    public void SetFogIntensityImmediate(float intensity)
    {
        currentIntensity = intTarget = Mathf.Clamp01(intensity);
        intTransitioning = false;
        lastSentIntensity = currentIntensity;
        OnFogIntensityChanged?.Invoke(currentIntensity);
    }

    public void SetFogBrightnessImmediate(float brightness)
    {
        currentBrightness = brtTarget = Mathf.Clamp01(brightness);
        brtTransitioning = false;
        lastSentBrightness = currentBrightness;
        OnFogBrightnessChanged?.Invoke(currentBrightness);
    }
}
