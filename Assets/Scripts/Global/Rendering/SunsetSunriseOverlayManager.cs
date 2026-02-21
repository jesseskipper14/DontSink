using UnityEngine;

public class SunriseSunsetOverlayManager : MonoBehaviour, ISunriseSunsetService
{
    [Header("Renderer")]
    [SerializeField] private SpriteRenderer overlayRenderer;
    [SerializeField] private Material overlayMaterial;



    // Warn-once flags
    private bool warnedMissingRenderer;
    private bool warnedMissingMaterial;
    // =====================================================
    // Sunrise Times
    // =====================================================
    [Header("Sunrise Times")]
    [SerializeField] private float sunriseStart = 4.5f;
    [SerializeField] private float sunrisePeak = 6f;
    [SerializeField] private float sunriseFadeEnd = 7.5f;

    // =====================================================
    // Sunset Times
    // =====================================================
    [Header("Sunset Times")]
    [SerializeField] private float sunsetStart = 16.2f;
    [SerializeField] private float sunsetPeak = 17.8f;
    [SerializeField] private float sunsetFadeEnd = 19f;

    // =====================================================
    // Sunrise Values
    // =====================================================
    [Header("Sunrise Values")]
    [SerializeField] private float sunriseGradientMin = 0f;
    [SerializeField] private float sunriseGradientMax = 3f;
    [SerializeField] private float sunriseBrightnessMin = 0.5f;
    [SerializeField] private float sunriseBrightnessMax = 2f;
    [SerializeField, Range(0f, 1f)] private float sunriseAlphaMin = 0f;
    [SerializeField, Range(0f, 1f)] private float sunriseAlphaMax = 0.5f;
    private float originalSunriseAlphaMax;

    // =====================================================
    // Sunset Values
    // =====================================================
    [Header("Sunset Values")]
    [SerializeField] private float sunsetGradientMin = 5f;
    [SerializeField] private float sunsetGradientMax = 0f;
    [SerializeField] private float sunsetBrightnessMin = 2f;
    [SerializeField] private float sunsetBrightnessMax = 0.5f;
    [SerializeField, Range(0f, 1f)] private float sunsetAlphaMin = 0f;
    [SerializeField, Range(0f, 1f)] private float sunsetAlphaMax = 0.7f;
    private float originalSunsetAlphaMax;

    public float Tint01 { get; private set; }
    public event System.Action<float> OnTintChanged;

    private ITimeOfDayService time;
    private ICloudService cloud;


    private void CacheMaterial()
    {
        if (overlayMaterial != null) return; // explicit override already set
        if (!overlayRenderer)
        {
            if (!warnedMissingRenderer)
            {
                warnedMissingRenderer = true;
                Debug.LogWarning("SunriseSunsetOverlayManager: overlayRenderer missing (sunrise/sunset overlay disabled for this scene).");
            }
            return;
        }

        overlayMaterial = overlayRenderer.sharedMaterial;
        if (overlayMaterial == null && !warnedMissingMaterial)
        {
            warnedMissingMaterial = true;
            Debug.LogWarning("SunriseSunsetOverlayManager: overlayRenderer has no material (overlay disabled for this scene).");
        }
    }

    /// <summary>
    /// Rebind scene-only visual anchors. Safe to call multiple times (e.g., every scene load).
    /// Does NOT resubscribe events; Initialize owns subscriptions.
    /// </summary>
    public void RebindSceneAnchors(SpriteRenderer renderer, Material materialOverride = null)
    {
        overlayRenderer = renderer;
        overlayMaterial = materialOverride;

        warnedMissingRenderer = false;
        warnedMissingMaterial = false;

        CacheMaterial();

        // Refresh visuals
        if (time != null) OnTimeChanged(time.CurrentTime);
        if (cloud != null) onCloudCoverageChanged(cloud.CloudCoverage);
    }

    public void Initialize(ITimeOfDayService timeService, ICloudService cloudService)
    {
        time = timeService;
        time.OnTimeChanged += OnTimeChanged;

        cloud = cloudService;
        cloud.OnCloudCoverageChanged += onCloudCoverageChanged;

        originalSunriseAlphaMax = sunriseAlphaMax;
        originalSunsetAlphaMax = sunsetAlphaMax;


        CacheMaterial();
        OnTimeChanged(time.CurrentTime);
        onCloudCoverageChanged(cloud.CloudCoverage);
    }

    private void OnDestroy()
    {
        if (time != null)
            time.OnTimeChanged -= OnTimeChanged;
        if (cloud != null)
            cloud.OnCloudCoverageChanged -= onCloudCoverageChanged;
    }

    private void OnTimeChanged(float hour)
    {
        if (!overlayMaterial) return;

        float gradient = 0f;
        float brightness = 0f;
        float alpha = 0f;

        // 🌅 Sunrise
        if (hour >= sunriseStart && hour <= sunrisePeak)
        {
            float f = Mathf.InverseLerp(sunriseStart, sunrisePeak, hour);
            gradient = Mathf.Lerp(sunriseGradientMin, sunriseGradientMax, f);
            brightness = Mathf.Lerp(sunriseBrightnessMin, sunriseBrightnessMax, f);
            alpha = Mathf.Lerp(sunriseAlphaMin, sunriseAlphaMax, f);
        }
        else if (hour > sunrisePeak && hour <= sunriseFadeEnd)
        {
            float f = Mathf.InverseLerp(sunrisePeak, sunriseFadeEnd, hour);
            gradient = sunriseGradientMax;
            brightness = sunriseBrightnessMax;
            alpha = Mathf.Lerp(sunriseAlphaMax, sunriseAlphaMin, f);
        }
        // 🌇 Sunset
        else if (hour >= sunsetStart && hour <= sunsetPeak)
        {
            float f = Mathf.InverseLerp(sunsetStart, sunsetPeak, hour);
            gradient = Mathf.Lerp(sunsetGradientMin, sunsetGradientMax, f);
            brightness = Mathf.Lerp(sunsetBrightnessMin, sunsetBrightnessMax, f);
            alpha = Mathf.Lerp(sunsetAlphaMin, sunsetAlphaMax, f);
        }
        else if (hour > sunsetPeak && hour <= sunsetFadeEnd)
        {
            float f = Mathf.InverseLerp(sunsetPeak, sunsetFadeEnd, hour);
            gradient = sunsetGradientMax;
            brightness = sunsetBrightnessMax;
            alpha = Mathf.Lerp(sunsetAlphaMax, sunsetAlphaMin, f);
        }
        else
        {
            gradient = 0f;
            brightness = sunriseBrightnessMin;
            alpha = 0f;
        }

        Apply(gradient, brightness, alpha);
        UpdateTint(alpha);
    }

    private void onCloudCoverageChanged(float coverage01)
    {

        if (coverage01 <= 0.7f)
        {
            sunriseAlphaMax = 1 - coverage01;
            sunsetAlphaMax = 1 - coverage01;
            return;
        }

        sunriseAlphaMax = originalSunriseAlphaMax;
        sunsetAlphaMax = originalSunsetAlphaMax;
    }

    private void UpdateTint(float alpha)
    {
        // Alpha is already normalized and visually meaningful
        if (Mathf.Approximately(alpha, Tint01))
            return;

        Tint01 = alpha;
        OnTintChanged?.Invoke(Tint01);
    }

    private void Apply(float gradient, float brightness, float alpha)
    {
        overlayMaterial.SetFloat("_GradientPower", gradient);
        overlayMaterial.SetFloat("_Brightness", brightness);
        overlayMaterial.SetFloat("_Alpha", alpha);
    }
}
