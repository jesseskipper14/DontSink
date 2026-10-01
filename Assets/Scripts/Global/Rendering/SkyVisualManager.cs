using System;
using UnityEngine;

public class SkyVisualManager : MonoBehaviour, ISkyVisualService
{
    [Header("Sky Settings")]
    [SerializeField] private Color horizonTint = new Color(1f, 0.6f, 0.3f);
    // Variation is now owned by the sky material itself. The old manager-level
    // control produced coarse blocks when applied to the new screen-space shader.
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

    // Phase 3 public visibility seam. SkyVisualManager remains the owner of
    // time-of-day star visibility; renderers can observe the result without
    // duplicating clock/fade rules.
    public float StarVisibility01 { get; private set; }
    public event Action<float> OnStarVisibilityChanged;

    // Warn-once flags
    private bool warnedMissingSkyMaterial;

    /// <summary>
    /// Called by EnvironmentManager on initialization
    /// </summary>
    public void Initialize(ITimeOfDayService time, IBrightnessService brightness)
    {
        if (timeService != null)
            timeService.OnTimeChanged -= UpdateStars;

        if (brightnessService != null)
            brightnessService.OnBrightnessChanged -= UpdateSky;

        timeService = time;
        if (timeService != null)
            timeService.OnTimeChanged += UpdateStars;

        brightnessService = brightness;
        if (brightnessService != null)
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
    }

    /// <summary>
    /// Stars fade logic, purely time-of-day based.
    /// Phase 3 computes/exposes visibility even if the legacy stars material is absent,
    /// allowing deterministic celestial renderers to reuse the same time rules.
    /// </summary>
    private void UpdateStars(float hour)
    {
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

        alpha = Mathf.Clamp01(alpha);

        if (!Mathf.Approximately(alpha, StarVisibility01))
        {
            StarVisibility01 = alpha;
            OnStarVisibilityChanged?.Invoke(StarVisibility01);
        }
        else
        {
            StarVisibility01 = alpha;
        }

        // Legacy compatibility only. Deterministic celestial stars are now allowed
        // to coexist with a sky material that has no _StarAlpha property at all.
        // If an old stars material is still present, keep driving it exactly as before.
        if (!starsMaterial)
            CacheMaterials();

        if (starsMaterial != null && starsMaterial.HasProperty("_StarAlpha"))
            starsMaterial.SetFloat("_StarAlpha", alpha);
    }
}
