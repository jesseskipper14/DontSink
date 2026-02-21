using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CelestialBodyManager : MonoBehaviour, ICelestialBodyService
{
    [Header("Shared")]
    [SerializeField] private float celestialOffsetX = 0f;

    // =====================================================
    // Sun
    // =====================================================
    [Header("Sun")]
    [SerializeField] private Transform sunTransform;
    [SerializeField] private Light2D sunLight;

    [SerializeField] private float sunStartX = -10f;
    [SerializeField] private float sunEndX = 10f;
    [SerializeField] private float sunMinY = -2f;
    [SerializeField] private float sunMaxY = 5f;

    [SerializeField] private float sunMaxIntensity = 1f;
    [SerializeField]
    private AnimationCurve sunBrightnessCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Range(1f, 5f)]
    [SerializeField] private float sunArcSharpness = 2f;

    [Header("Sun Corona")]
    [SerializeField] private SpriteRenderer sunCorona;
    [SerializeField] private Material coronaMaterial;
    [SerializeField, Range(0f, 0.55f)] private float coronaMaxRadius = 0.55f;
    [SerializeField, Range(0.7f, 1f)] private float coronaMinSoftness = 0.7f;

    // =====================================================
    // Moon
    // =====================================================
    [Header("Moon")]
    [SerializeField] private Transform moonTransform;
    [SerializeField] private Light2D moonLight;

    [SerializeField] private float moonRise = 18f;
    [SerializeField] private float moonSet = 6f;

    [SerializeField] private float moonStartX = -10f;
    [SerializeField] private float moonEndX = 10f;
    [SerializeField] private float moonMinY = -2f;
    [SerializeField] private float moonMaxY = 5f;

    [SerializeField] private float moonMinIntensity = 0f;
    [SerializeField] private float moonMaxIntensity = 0.5f;

    [Range(1f, 5f)]
    [SerializeField] private float moonArcSharpness = 2f;

    // =====================================================

    private ITimeOfDayService time;

    /// <summary>
    /// Called on scene load to reconnect scene-owned transforms/lights.
    /// Safe to call repeatedly.
    /// </summary>
    public void RebindSceneAnchors(
        Transform sun,
        Light2D sunLight2D,
        Transform moon,
        Light2D moonLight2D,
        SpriteRenderer corona = null,
        Material coronaMat = null)
    {
        sunTransform = sun;
        sunLight = sunLight2D;
        moonTransform = moon;
        moonLight = moonLight2D;

        if (corona != null) sunCorona = corona;
        if (coronaMat != null) coronaMaterial = coronaMat;

        // If time already initialized, refresh visuals immediately.
        if (time != null)
            OnTimeChanged(time.CurrentTime);
    }

    public void Initialize(ITimeOfDayService timeService)
    {
        if (time != null)
            time.OnTimeChanged -= OnTimeChanged;

        time = timeService;

        if (time != null)
            time.OnTimeChanged += OnTimeChanged;

        // Force initial update
        if (time != null)
            OnTimeChanged(time.CurrentTime);
    }

    private void OnDestroy()
    {
        if (time != null)
            time.OnTimeChanged -= OnTimeChanged;
    }

    private void OnTimeChanged(float hour)
    {
        UpdateSun(hour);
        UpdateMoon(hour);
    }

    private void UpdateSun(float hour)
    {
        if (!sunTransform || !sunLight) return;

        float t = hour / 24f;

        float parabola = 4f * t * (1f - t);
        float arc = Mathf.Pow(parabola, sunArcSharpness);

        float y = Mathf.Lerp(sunMinY, sunMaxY, arc);
        float x = Mathf.Lerp(sunStartX, sunEndX, t) + celestialOffsetX;

        sunTransform.localPosition = new Vector3(x, y, sunTransform.localPosition.z);

        float intensity01 = sunBrightnessCurve.Evaluate(t);
        sunLight.intensity = intensity01 * sunMaxIntensity;

        if (sunCorona && coronaMaterial)
        {
            float radius = Mathf.Lerp(0f, coronaMaxRadius, intensity01);
            float softness = Mathf.Lerp(1f, coronaMinSoftness, intensity01);

            coronaMaterial.SetFloat("_Radius", radius);
            coronaMaterial.SetFloat("_Softness", softness);
        }
    }

    private void UpdateMoon(float hour)
    {
        if (!moonTransform || !moonLight) return;

        float moonT;
        bool visible = TryGetMoonT(hour, out moonT);

        if (!visible)
        {
            moonLight.intensity = 0f;
            return;
        }

        float parabola = 4f * moonT * (1f - moonT);
        float arc = Mathf.Pow(parabola, moonArcSharpness);

        float y = Mathf.Lerp(moonMinY, moonMaxY, arc);
        float x = Mathf.Lerp(moonStartX, moonEndX, moonT) + celestialOffsetX;

        moonTransform.localPosition = new Vector3(x, y, moonTransform.localPosition.z);
        moonLight.intensity = Mathf.Lerp(moonMinIntensity, moonMaxIntensity, parabola);
    }

    private bool TryGetMoonT(float hour, out float moonT)
    {
        float duration = (moonSet >= moonRise)
            ? moonSet - moonRise
            : moonSet + 24f - moonRise;

        if (moonRise <= moonSet)
        {
            if (hour < moonRise || hour > moonSet)
            {
                moonT = 0f;
                return false;
            }

            moonT = Mathf.InverseLerp(moonRise, moonSet, hour);
            return true;
        }
        else
        {
            if (hour < moonRise && hour > moonSet)
            {
                moonT = 0f;
                return false;
            }

            float normalizedHour = (hour >= moonRise) ? hour : hour + 24f;
            moonT = Mathf.InverseLerp(moonRise, moonRise + duration, normalizedHour);
            return true;
        }
    }

    private float offsetX = 0f;

    public void SetHorizontalOffset(float x)
    {
        celestialOffsetX = x; // <-- FIX: you were writing offsetX and never using it
        if (time != null)
            OnTimeChanged(time.CurrentTime);
    }
}


