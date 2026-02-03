//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.Rendering.Universal;

//public class EnvironmentManager : MonoBehaviour
//{
//    // =====================================================
//    // Time Settings
//    // =====================================================
//    [Header("Time Settings")]
//    [Tooltip("Length of a full day in seconds")]
//    public float dayLength = 120f;
//    [Range(0f, 24f)]
//    [Tooltip("Current time of day (0-24h)")]
//    public float currentTime = 12f;

//    // =====================================================
//    // Sky Settings
//    // =====================================================
//    [Header("Sky Settings")]
//    [Tooltip("The SpriteRenderer for the sky background")]
//    public SpriteRenderer skyRenderer;
//    [Header("Sky Variation")]
//    public float skyVariationStrength = 0.05f; // subtle noise
//    [Header("Sun Horizon Tint")]
//    [Range(0f, 1f)]
//    public float horizonWarmth = 0.2f;
//    public Color horizonTint = new Color(1f, 0.6f, 0.3f);

//    // =====================================================
//    // Brightness Settings
//    // =====================================================
//    [Header("Brightness Settings")]
//    [Range(0f, 1f)] public float minBrightness = 0.2f;  // darkest (midnight)
//    [Range(0f, 1f)] public float maxBrightness = 1f;    // brightest (noon)
//    [Header("Objects to Update Brightness")]
//    [Tooltip("Add all SpriteRenderers that should be affected by global brightness")]
//    public List<SpriteRenderer> brightnessObjects = new List<SpriteRenderer>();

//    // =====================================================
//    // Sun Settings
//    // =====================================================
//    [Header("Sun Settings")]
//    public Transform sunTransform;
//    public Light2D sunLight; // URP 2D Light component

//    [Header("Sun Mask Settings")]
//    [Tooltip("Sun is hidden below this Y position")]
//    public float sunMaskY = 3f;
//    [Tooltip("Smooth fade distance below mask")]
//    public float sunMaskFade = 0f;

//    [Header("Sun Path Settings")]
//    public float sunStartX = -10f;
//    public float sunEndX = 10f;
//    public float sunMinY = -2f;
//    public float sunMaxY = 5f;

//    [Header("Sun Brightness Curve")]
//    public AnimationCurve sunBrightnessCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
//    public float sunMaxIntensity = 1f;

//    [Header("Sun Arc Shape")]
//    [Range(1f, 5f)]
//    public float arcSharpness = 2f;

//    [Header("Sun Corona Settings")]
//    public SpriteRenderer sunCorona; // Assign your corona sprite here
//    public Material coronaMaterial;   // Assign your corona material here
//    [Range(0f, 0.55f)] public float coronaMaxRadius = 0.55f;
//    [Range(0.7f, 1f)] public float coronaMinSoftness = 0.7f;

//    [Header("Celestial Parallax Offsets")]
//    public float celestialOffsetX = 0f;

//    // =====================================================
//    // Moon Settings
//    // =====================================================

//    [Header("Moon")]
//    [Tooltip("Transform representing the moon")]
//    public Transform moonTransform;
//    [Tooltip("URP 2D Light for moon glow")]
//    public Light2D moonLight;

//    [Range(0f, 1f)]
//    public float moonMaxIntensity = 0.5f; // max brightness of moon
//    [Range(0f, 1f)]
//    public float moonMinIntensity = 0f;   // lowest brightness (daytime)

//    [Tooltip("Moon rise/set times in hours")]
//    public float moonRise = 18f;
//    public float moonPeak = 0f; // highest point at midnight
//    public float moonSet = 6f;

//    [Header("Moon Arc Settings")]
//    public float moonStartX = -10f;
//    public float moonEndX = 10f;
//    public float moonMinY = -2f;
//    public float moonMaxY = 5f;
//    public float moonArcSharpness = 2f;


//    // =====================================================
//    // Cloud Spawning
//    // =====================================================
//    [Header("Cloud Spawning")]
//    public GameObject cloudPrefab;
//    public int targetCloudCount = 10;
//    public float spawnInterval = 1f;
//    [Range(0f, 1f)] public float spawnChance = 0.5f;
//    public Vector2 spawnXRange = new Vector2(65f, 70f);
//    public Vector2 spawnYRange = new Vector2(4f, 30f);
//    public float cloudDestroyX = 70.0f;

//    // =====================================================
//    // Sea / Side Water / Foam
//    // =====================================================
//    [Header("Sea Background")]
//    public SpriteRenderer seaRenderer;
//    public Material seaMaterial;

//    [Header("Side Water")]
//    public MeshRenderer sideWaterRenderer;
//    private Material sideWaterMaterial;

//    [Header("Foam Settings")]
//    [Range(0f, 1f)] public float amountOfFoam = 1f;

//    // =====================================================
//    // Sunrise/Sunset
//    // =====================================================
//    [Header("Sunset Sunrise")]
//    public SpriteRenderer SunrisesetRenderer;
//    public Material SunrisesetMaterial;

//    [Header("Sunrise/Sunset Times")]
//    public float sunriseStart = 4.5f;
//    public float sunrisePeak = 6f;
//    public float sunriseFadeEnd = 7.5f;
//    public float sunsetStart = 16.2f;
//    public float sunsetPeak = 17.8f;
//    public float sunsetFadeEnd = 19f;

//    [Header("Sunrise/Sunset Values")]
//    public float sunriseGradientMin = 0f;
//    public float sunriseGradientMax = 5f;
//    public float sunriseBrightnessMin = 0.5f;
//    public float sunriseBrightnessMax = 2f;
//    public float sunsetGradientMin = 5f;
//    public float sunsetGradientMax = 0f;
//    public float sunsetBrightnessMin = 2f;
//    public float sunsetBrightnessMax = 0.5f;
//    [Range(0f, 1f)] public float sunriseAlphaMin = 0f;
//    [Range(0f, 1f)] public float sunriseAlphaMax = 1f;
//    [Range(0f, 1f)] public float sunsetAlphaMin = 0f;
//    [Range(0f, 1f)] public float sunsetAlphaMax = 1f;

//    // =====================================================
//    // Stars
//    // =====================================================

//    [Header("Stars")]
//    [Tooltip("Renderer for the stars overlay (uses your star shader)")]
//    public SpriteRenderer starsRenderer;
//    public Material starsMaterial;

//    [Range(0f, 1f)]
//    public float starsMaxAlpha = 1f;    // Alpha at night
//    [Range(0f, 1f)]
//    public float starsMinAlpha = 0f;    // Alpha during day

//    [Tooltip("Hour at which stars start fading in")]
//    public float starsFadeInStart = 18.0f;
//    [Tooltip("Hour at which stars reach full alpha")]
//    public float starsFadeInEnd = 19.0f;
//    [Tooltip("Hour at which stars start fading out in the morning")]
//    public float starsFadeOutStart = 4.0f;
//    [Tooltip("Hour at which stars reach zero alpha")]
//    public float starsFadeOutEnd = 6.0f;

//    // =====================================================
//    // Private Materials
//    // =====================================================
//    private Material skyMaterial;

//    // =====================================================
//    // Cloud Spawn Tracking
//    // =====================================================
//    private float cloudSpawnTimer = 0f;
//    private int currentCloudCount = 0;

//    // =====================================================
//    // Unity Lifecycle
//    // =====================================================
//    private void Awake()
//    {
//        if (skyRenderer != null)
//            skyMaterial = skyRenderer.sharedMaterial;
//        if (seaRenderer != null)
//            seaMaterial = seaRenderer.sharedMaterial;
//        if (sideWaterRenderer != null)
//            sideWaterMaterial = sideWaterRenderer.sharedMaterial;
//    }

//    private void Update()
//    {
//        UpdateTime();
//        UpdateSky();
//        UpdateSun();
//        UpdateMoon();
//        UpdateSea();
//        UpdateSideWater();
//        UpdateFoamIntensity();
//        UpdateSunriseSunset();
//        UpdateObjectBrightness();
//        UpdateStars();
//        //UpdateClouds();
//    }

//    // =====================================================
//    // Time Methods
//    // =====================================================
//    private void UpdateTime()
//    {
//        if (dayLength <= 0f) return;

//        currentTime += (24f / dayLength) * Time.deltaTime;
//        if (currentTime >= 24f) currentTime -= 24f;
//    }

//    public float GetDayBrightness01()
//    {
//        float t = currentTime / 24f;
//        return sunBrightnessCurve.Evaluate(t);
//    }

//    // =====================================================
//    // Sky Methods
//    // =====================================================
//private void UpdateSky()
//{
//    if (skyMaterial == null) return;

//    float brightness01 = GetDayBrightness01();
//    float brightness = Mathf.Lerp(minBrightness, maxBrightness, brightness01);

//    skyMaterial.SetFloat("_Brightness", brightness);
//    skyMaterial.SetColor("_HorizonTint", horizonTint);
//    skyMaterial.SetFloat("_VariationStrength", skyVariationStrength);
//}

//    // =====================================================
//    // Sun Methods
//    // =====================================================
//    private void UpdateSun()
//    {
//        if (sunTransform == null || sunLight == null) return;

//        float t = currentTime / 24f;
//        float parabola = 4f * t * (1f - t);
//        float arc = Mathf.Pow(parabola, arcSharpness);

//        float y = Mathf.Lerp(sunMinY, sunMaxY, arc);
//        float smoothT = Mathf.SmoothStep(0f, 1f, t);
//        float x = Mathf.Lerp(sunStartX, sunEndX, smoothT);
//        x += celestialOffsetX;

//        sunTransform.localPosition = new Vector3(x, y, sunTransform.localPosition.z);

//        float brightness01 = GetDayBrightness01();
//        sunLight.intensity = brightness01 * sunMaxIntensity;

//        if (sunCorona != null && coronaMaterial != null)
//        {
//            float radius = Mathf.Lerp(0f, coronaMaxRadius, brightness01);
//            float softness = Mathf.Lerp(1f, coronaMinSoftness, brightness01);
//            coronaMaterial.SetFloat("_Radius", radius);
//            coronaMaterial.SetFloat("_Softness", softness);
//        }
//    }

//    // =====================================================
//    // Moon Methods
//    // =====================================================
//    private void UpdateMoon()
//    {
//        if (moonTransform == null || moonLight == null) return;

//        float t = currentTime;

//        // Calculate moon arc duration
//        float moonDuration = (moonSet >= moonRise) ? (moonSet - moonRise) : (moonSet + 24f - moonRise);

//        bool moonVisible = false;
//        float moonT = 0f;

//        // Check if current time is within moon arc
//        if (moonRise <= moonSet) // arc does NOT cross midnight
//        {
//            if (t >= moonRise && t <= moonSet)
//            {
//                moonT = Mathf.InverseLerp(moonRise, moonSet, t);
//                moonVisible = true;
//            }
//        }
//        else // arc wraps past midnight
//        {
//            if (t >= moonRise || t <= moonSet)
//            {
//                // Normalize t to moon arc
//                float normalizedT = (t >= moonRise) ? t : t + 24f;
//                moonT = Mathf.InverseLerp(moonRise, moonRise + moonDuration, normalizedT);
//                moonVisible = true;
//            }
//        }

//        if (!moonVisible)
//        {
//            // Moon is below horizon
//            moonLight.intensity = 0f;
//            return;
//        }

//        // Sharpened parabolic arc for moon
//        float parabola = 4f * moonT * (1f - moonT);
//        float arc = Mathf.Pow(parabola, moonArcSharpness);

//        // Interpolate position along arc
//        float y = Mathf.Lerp(moonMinY, moonMaxY, arc);
//        float x = Mathf.Lerp(moonStartX, moonEndX, moonT);
//        x += celestialOffsetX;

//        moonTransform.localPosition = new Vector3(x, y, moonTransform.localPosition.z);

//        // Moon brightness along arc
//        float intensity = Mathf.Lerp(moonMinIntensity, moonMaxIntensity, parabola);
//        moonLight.intensity = intensity;
//    }


//    // =====================================================
//    // Sea & Side Water Methods
//    // =====================================================
//    private void UpdateSea()
//    {
//        if (seaMaterial == null) return;

//        float brightness01 = GetDayBrightness01();
//        float seaBrightness = Mathf.Lerp(0.2f, 1.0f, brightness01);
//        seaMaterial.SetFloat("_Brightness", seaBrightness);

//        float sparkleScale = Mathf.Lerp(0f, 1.2f, brightness01);
//        seaMaterial.SetFloat("_SparkleIntensity", sparkleScale);
//    }

//    private void UpdateSideWater()
//    {
//        if (sideWaterMaterial == null) return;

//        float brightness01 = GetDayBrightness01();
//        float waterBrightness = Mathf.Lerp(0.2f, 1f, brightness01);
//        sideWaterMaterial.SetFloat("_Brightness", waterBrightness);

//        float sparkleScale = Mathf.Lerp(0f, 1.2f, brightness01);
//        sideWaterMaterial.SetFloat("_SparkleIntensity", sparkleScale);
//    }

//    private void UpdateFoamIntensity()
//    {
//        if (sideWaterMaterial != null)
//            sideWaterMaterial.SetFloat("_FoamIntensity", amountOfFoam);
//    }

//    // =====================================================
//    // Sunrise/Sunset Methods
//    // =====================================================
//    private void UpdateSunriseSunset()
//    {
//        if (SunrisesetMaterial == null) return;

//        float t = currentTime;
//        float gradientPower = 0f;
//        float brightness = 0f;
//        float alpha = 0f;

//        // Morning sunrise
//        if (t >= sunriseStart && t <= sunrisePeak)
//        {
//            float factor = Mathf.InverseLerp(sunriseStart, sunrisePeak, t);
//            gradientPower = Mathf.Lerp(sunriseGradientMin, sunriseGradientMax, factor);
//            brightness = Mathf.Lerp(sunriseBrightnessMin, sunriseBrightnessMax, factor);
//            alpha = Mathf.Lerp(sunriseAlphaMin, sunriseAlphaMax, factor);
//        }
//        else if (t > sunrisePeak && t <= sunriseFadeEnd)
//        {
//            float factor = Mathf.InverseLerp(sunrisePeak, sunriseFadeEnd, t);
//            gradientPower = sunriseGradientMax;
//            brightness = sunriseBrightnessMax;
//            alpha = Mathf.Lerp(sunriseAlphaMax, sunriseAlphaMin, factor);
//        }
//        // Evening sunset
//        else if (t >= sunsetStart && t <= sunsetPeak)
//        {
//            float factor = Mathf.InverseLerp(sunsetStart, sunsetPeak, t);
//            gradientPower = Mathf.Lerp(sunsetGradientMin, sunsetGradientMax, factor);
//            brightness = Mathf.Lerp(sunsetBrightnessMin, sunsetBrightnessMax, factor);
//            alpha = Mathf.Lerp(sunsetAlphaMin, sunsetAlphaMax, factor);
//        }
//        else if (t > sunsetPeak && t <= sunsetFadeEnd)
//        {
//            float factor = Mathf.InverseLerp(sunsetPeak, sunsetFadeEnd, t);
//            gradientPower = sunsetGradientMax;
//            brightness = sunsetBrightnessMax;
//            alpha = Mathf.Lerp(sunsetAlphaMax, sunsetAlphaMin, factor);
//        }
//        else
//        {
//            gradientPower = 0f;
//            brightness = sunriseBrightnessMin;
//            alpha = 0f;
//        }

//        SunrisesetMaterial.SetFloat("_GradientPower", gradientPower);
//        SunrisesetMaterial.SetFloat("_Brightness", brightness);
//        SunrisesetMaterial.SetFloat("_Alpha", alpha);
//    }

//    // =====================================================
//    // Brightness Object Management
//    // =====================================================
//    public void RegisterBrightnessObject(SpriteRenderer sr)
//    {
//        if (sr != null && !brightnessObjects.Contains(sr))
//            brightnessObjects.Add(sr);
//    }

//    public void UnregisterBrightnessObject(SpriteRenderer sr)
//    {
//        if (sr != null && brightnessObjects.Contains(sr))
//            brightnessObjects.Remove(sr);
//    }

//    private void UpdateObjectBrightness()
//    {
//        float globalBrightness = GetDayBrightness01();

//        foreach (var sr in brightnessObjects)
//        {
//            Debug.Log(sr);
//            if (sr == null || sr.sharedMaterial == null) continue;
//            sr.material.SetFloat("_Brightness", globalBrightness);
//        }
//    }

//    // =====================================================
//    // Stars
//    // =====================================================

//    private void UpdateStars()
//    {
//        if (starsRenderer == null || starsMaterial == null) return;

//        float alpha = 0f;
//        float t = currentTime;

//        // Fade in in evening
//        if (t >= starsFadeInStart && t <= starsFadeInEnd)
//        {
//            float factor = Mathf.InverseLerp(starsFadeInStart, starsFadeInEnd, t);
//            alpha = Mathf.Lerp(starsMinAlpha, starsMaxAlpha, factor);
//        }
//        // Fade out in morning
//        else if (t >= starsFadeOutStart && t <= starsFadeOutEnd)
//        {
//            float factor = Mathf.InverseLerp(starsFadeOutStart, starsFadeOutEnd, t);
//            alpha = Mathf.Lerp(starsMaxAlpha, starsMinAlpha, factor);
//        }
//        // Fully night
//        else if (t > starsFadeInEnd || t < starsFadeOutStart)
//        {
//            alpha = starsMaxAlpha;
//        }
//        // Fully day
//        else
//        {
//            alpha = starsMinAlpha;
//        }

//        // Apply alpha to shader
//        starsMaterial.SetFloat("_StarAlpha", alpha);
//    }



//    // =====================================================
//    // Cloud Methods (optional)
//    // =====================================================
//    private void UpdateClouds()
//    {
//        float sunBrightness = GetDayBrightness01();

//        cloudSpawnTimer += Time.deltaTime;
//        if (cloudSpawnTimer >= spawnInterval)
//        {
//            cloudSpawnTimer = 0f;

//            if (currentCloudCount < targetCloudCount && Random.value < spawnChance)
//            {
//                SpawnCloud();
//            }
//        }

//        ProceduralCloud[] clouds = Object.FindObjectsByType<ProceduralCloud>(FindObjectsSortMode.None);
//        currentCloudCount = clouds.Length;

//        foreach (var cloud in clouds)
//            cloud.UpdateCloud(sunBrightness);
//    }

//    private void SpawnCloud()
//    {
//        if (cloudPrefab == null) return;

//        float spawnX = Random.Range(spawnXRange.x, spawnXRange.y);
//        float spawnY = Random.Range(spawnYRange.x, spawnYRange.y);

//        GameObject cloudGO = Instantiate(cloudPrefab, new Vector3(spawnX, spawnY, 0f), Quaternion.identity);

//        float scale = Random.Range(0.8f, 1.5f);
//        cloudGO.transform.localScale = new Vector3(scale, scale, 1f);
//    }
//}
