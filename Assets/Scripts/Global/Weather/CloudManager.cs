using System;
using UnityEngine;

public class CloudManager : MonoBehaviour, ICloudService
{
    [Header("Cloud State")]
    [Range(0.4f, 1f)]
    [SerializeField] private float cloudCoverage = 0.4f;

    [Header("Renderer")]
    [SerializeField] private Renderer cloudRenderer;
    [SerializeField] private Material cloudMaterial;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 5f;

    // =========================
    // Linear transition state
    // =========================
    private float startCoverage;
    private float targetCoverage;
    private float elapsed;
    private bool transitioning;

    private float lastSentCoverage = -1f;

    private IBrightnessService brightnessService;
    private ISunriseSunsetService sunriseSunsetService;
    private IWeatherService weatherService;

    // =========================
    // ICloudService
    // =========================
    public float CloudCoverage => cloudCoverage;
    public event Action<float> OnCloudCoverageChanged;

    // =========================
    // Initialization
    // =========================
    public void Initialize(
        IBrightnessService brightness,
        ISunriseSunsetService sunriseSunset,
        IWeatherService weather)
    {
        brightnessService = brightness;
        sunriseSunsetService = sunriseSunset;
        weatherService = weather;

        CacheMaterial();

        // Subscriptions
        brightnessService.OnBrightnessChanged += UpdateBrightness;
        sunriseSunsetService.OnTintChanged += UpdateSunTint;
        weatherService.OnCloudCoverageChanged += OnWeatherCoverageChanged;

        // Initial sync
        UpdateBrightness(brightnessService.Brightness01);
        UpdateSunTint(sunriseSunsetService.Tint01);
        SetCoverage(weatherService.CloudCoverage, 0f);
    }

    private void OnDestroy()
    {
        if (brightnessService != null)
            brightnessService.OnBrightnessChanged -= UpdateBrightness;

        if (sunriseSunsetService != null)
            sunriseSunsetService.OnTintChanged -= UpdateSunTint;

        if (weatherService != null)
            weatherService.OnCloudCoverageChanged -= OnWeatherCoverageChanged;
    }

    private void CacheMaterial()
    {
        if (cloudRenderer && cloudMaterial == null)
            cloudMaterial = cloudRenderer.sharedMaterial;
    }

    private void Update()
    {
        if (!transitioning) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / transitionDuration);

        cloudCoverage = Mathf.Lerp(startCoverage, targetCoverage, t);
        ApplyCoverage(cloudCoverage);

        if (!Mathf.Approximately(cloudCoverage, lastSentCoverage))
        {
            lastSentCoverage = cloudCoverage;
            OnCloudCoverageChanged?.Invoke(cloudCoverage);
        }

        if (t >= 1f)
            transitioning = false;
    }

    // =========================
    // Weather hook
    // =========================
    private void OnWeatherCoverageChanged(float coverage, float duration)
    {
        SetCoverage(coverage, duration);
    }

    // =========================
    // ICloudService API
    // =========================
    public void SetCoverage(float coverage01, float duration)
    {
        coverage01 = Mathf.Clamp01(coverage01);

        startCoverage = cloudCoverage;
        targetCoverage = coverage01;
        transitionDuration = Mathf.Max(0.001f, duration);
        elapsed = 0f;
        transitioning = true;
    }

    public void UpdateBrightness(float brightness)
    {
        if (cloudMaterial)
            cloudMaterial.SetFloat("_Brightness", brightness);
    }

    public void UpdateSunTint(float sunrise)
    {
        if (cloudMaterial)
            cloudMaterial.SetFloat("_SunTint", sunrise);
    }

    // =========================
    // Visual application
    // =========================
    private void ApplyCoverage(float coverage01)
    {
        if (cloudMaterial)
            cloudMaterial.SetFloat("_Coverage", coverage01);
    }
}
