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


    // Warn-once flags
    private bool warnedMissingSkyMaterial;
    private bool warnedMissingStarsMaterial;
    /// <summary>
    /// Called by EnvironmentManager on initialization
    /// </summary>
    public void Initialize(ITimeOfDayService time, IBrightnessService brightness)
    {
        timeService = time;
        timeService.OnTimeChanged += UpdateStars;

        brightnessService = brightness;
        brightnessService.OnBrightnessChanged += UpdateSky;

        // Cache materials (scene refs may be injected later via RebindSceneAnchors)
        CacheMaterials();

        // Initial sync
        if (brightnessService != null) UpdateSky(brightnessService.Brightness01);
        if (timeService != null) UpdateStars(timeService.CurrentTime);
    }


    private void CacheMaterials()
    {
        if (starsMaterial == null && starsRenderer != null)
            starsMaterial = starsRenderer.sharedMaterial;

        // skyMaterial is expected to be an asset reference in many setups, but allow overrides.
    }

    /// <summary>
    /// Rebind scene-only visual anchors. Safe to call multiple times (e.g., every scene load).
    /// Does NOT resubscribe events; Initialize owns subscriptions.
    /// </summary>
    public void RebindSceneAnchors(SpriteRenderer stars, Material skyOverride = null, Material starsOverride = null)
    {
        starsRenderer = stars;

        if (skyOverride != null)
            skyMaterial = skyOverride;

        starsMaterial = starsOverride; // can be null; CacheMaterials will pick up from renderer

        // reset warn flags per scene
        warnedMissingSkyMaterial = false;
        warnedMissingStarsMaterial = false;

        CacheMaterials();

        // Refresh using current service state
        if (brightnessService != null) UpdateSky(brightnessService.Brightness01);
        if (timeService != null) UpdateStars(timeService.CurrentTime);
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
            if (!warnedMissingSkyMaterial)
            {
                warnedMissingSkyMaterial = true;
                Debug.LogWarning("SkyVisualManager: skyMaterial missing (sky visuals disabled for this scene).");
            }
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
        if (!starsMaterial)
        {
            CacheMaterials();
            if (!starsMaterial)
            {
                if (!warnedMissingStarsMaterial)
                {
                    warnedMissingStarsMaterial = true;
                    Debug.LogWarning("SkyVisualManager: stars material missing (stars disabled for this scene).");
                }
                return;
            }
        }
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
