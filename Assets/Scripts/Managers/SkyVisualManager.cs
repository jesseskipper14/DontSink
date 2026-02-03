using UnityEngine;

public class SkyVisualManager : MonoBehaviour, ISkyVisualService
{
    [Header("Sky Settings")]
    [SerializeField] private Color horizonTint = new Color(1f, 0.6f, 0.3f);
    [SerializeField] private float skyVariationStrength = 0.05f;
    [SerializeField] private Material skyMaterial;

    [Header("Stars Settings")]
    [SerializeField] private SpriteRenderer starsRenderer;
    [SerializeField] private Material starsMaterial;

    [SerializeField] private float starsFadeInStart = 18f;
    [SerializeField] private float starsFadeInEnd = 19f;
    [SerializeField] private float starsFadeOutStart = 4f;
    [SerializeField] private float starsFadeOutEnd = 6f;

    [SerializeField, Range(0f, 1f)] private float starsMinAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float starsMaxAlpha = 1f;

    private ITimeOfDayService timeService;
    private IBrightnessService brightnessService;

    /// <summary>
    /// Called by EnvironmentManager on initialization
    /// </summary>
    public void Initialize(ITimeOfDayService time, IBrightnessService brightness)
    {
        timeService = time;
        timeService.OnTimeChanged += UpdateStars;

        brightnessService = brightness;
        brightnessService.OnBrightnessChanged += UpdateSky;

        // Force initial stars update
        UpdateStars(timeService.CurrentTime);
    }

    private void OnDestroy()
    {
        if (timeService != null)
            timeService.OnTimeChanged -= UpdateStars;
        if (brightnessService != null)
            brightnessService.OnBrightnessChanged -= UpdateSky;
    }

    /// <summary>
    /// Called by BrightnessReceiver whenever global brightness changes
    /// </summary>
    /// <param name="brightness">0-1 global brightness</param>
    public void UpdateSky(float brightness)
    {
        if (!skyMaterial)
        {
            Debug.Log("No material found");
            return;
        }

        skyMaterial.SetFloat("_Brightness", brightness);
        skyMaterial.SetColor("_HorizonTint", horizonTint);
        skyMaterial.SetFloat("_VariationStrength", skyVariationStrength);
    }

    /// <summary>
    /// Stars fade logic, purely time-of-day based
    /// </summary>
    private void UpdateStars(float hour)
    {
        if (!starsMaterial) return;

        float alpha;

        if (hour >= starsFadeInStart && hour <= starsFadeInEnd)
        {
            float t = Mathf.InverseLerp(starsFadeInStart, starsFadeInEnd, hour);
            alpha = Mathf.Lerp(starsMinAlpha, starsMaxAlpha, t);
        }
        else if (hour >= starsFadeOutStart && hour <= starsFadeOutEnd)
        {
            float t = Mathf.InverseLerp(starsFadeOutStart, starsFadeOutEnd, hour);
            alpha = Mathf.Lerp(starsMaxAlpha, starsMinAlpha, t);
        }
        else if (hour > starsFadeInEnd || hour < starsFadeOutStart)
        {
            alpha = starsMaxAlpha;
        }
        else
        {
            alpha = starsMinAlpha;
        }

        starsMaterial.SetFloat("_StarAlpha", alpha);
    }
}
