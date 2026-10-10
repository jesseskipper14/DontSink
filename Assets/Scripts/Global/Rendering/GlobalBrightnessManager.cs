using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class GlobalBrightnessManager : MonoBehaviour, IBrightnessService
{
    [Header("Daylight Hours (24-hour clock)")]
    [Tooltip("Hour when the scene begins brightening from night. Example: 4 = 4 AM.")]
    [SerializeField, Range(0f, 24f)] private float dawnStartHour = 4f;
    [Tooltip("Hour when scene and sky reach Max Brightness. Set 6 for full brightness at 6 AM.")]
    [SerializeField, Range(0f, 24f)] private float fullDaylightHour = 6f;
    [Tooltip("Hour when the scene begins dimming. Example: 18 = 6 PM.")]
    [SerializeField, Range(0f, 24f)] private float duskStartHour = 18f;
    [Tooltip("Hour when scene and sky reach Min Brightness. Example: 20 = 8 PM.")]
    [SerializeField, Range(0f, 24f)] private float fullNightHour = 20f;
    [Header("Brightness Levels")]
    [SerializeField] private bool useLegacyBrightnessCurve;
    [SerializeField]
    private AnimationCurve brightnessCurve =
        AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f);

    [SerializeField] private float minBrightness = 0.1f;
    [SerializeField] private float maxBrightness = 1f;

    [Header("Global Light 2D")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private bool driveGlobalLight = true;

    [Tooltip("Optional multiplier if you want light intensity to be stronger/weaker than Brightness01.")]
    [SerializeField] private float lightIntensityMultiplier = 1f;

    private readonly HashSet<SpriteRenderer> registered = new();
    private MaterialPropertyBlock mpb;

    public float Brightness01 { get; private set; }

    public event System.Action<float> OnBrightnessChanged;

    private ITimeOfDayService timeService;

    private void Awake()
    {
        if (GetComponent<EnvironmentDepthLighting>() == null)
            gameObject.AddComponent<EnvironmentDepthLighting>();
        mpb = new MaterialPropertyBlock();

        if (globalLight == null)
            globalLight = FindSceneGlobalLight();
    }

    public void Initialize(ITimeOfDayService time)
    {
        if (timeService != null)
            timeService.OnTimeChanged -= HandleTimeChanged;

        timeService = time;

        if (timeService != null)
        {
            timeService.OnTimeChanged += HandleTimeChanged;
            HandleTimeChanged(timeService.CurrentTime, forceApply: true);
        }
    }

    public void RebindSceneAnchors(Light2D sceneGlobalLight)
    {
        globalLight = sceneGlobalLight != null
            ? sceneGlobalLight
            : FindSceneGlobalLight();

        // A scene transition usually does not change Brightness01, so relying on the
        // next time-change event leaves the newly loaded light untouched. Apply now.
        ApplyGlobalLight();
    }

    private void OnDestroy()
    {
        if (timeService != null)
            timeService.OnTimeChanged -= HandleTimeChanged;
    }

    private void HandleTimeChanged(float hour)
    {
        HandleTimeChanged(hour, forceApply: false);
    }

    private void HandleTimeChanged(float hour, bool forceApply)
    {
        float curveValue = EvaluateDaylight01(hour);
        float newBrightness = Mathf.Lerp(minBrightness, maxBrightness, curveValue);

        bool changed = !Mathf.Approximately(newBrightness, Brightness01);
        Brightness01 = newBrightness;

        if (changed || forceApply)
        {
            ApplyBrightness();
            ApplyGlobalLight();
        }

        if (changed)
            OnBrightnessChanged?.Invoke(Brightness01);
    }

    /// <summary>Clock-driven illumination; sun geometry never controls scene brightness.</summary>
    public float EvaluateDaylight01(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        if (useLegacyBrightnessCurve) return Mathf.Clamp01(brightnessCurve.Evaluate(hour / 24f));
        // Keep transitions ordered even while Inspector values are being edited.
        float dawn = Mathf.Clamp(dawnStartHour, 0f, 24f);
        float day = Mathf.Clamp(fullDaylightHour, dawn, 24f);
        float dusk = Mathf.Clamp(duskStartHour, day, 24f);
        float night = Mathf.Clamp(fullNightHour, dusk, 24f);
        if (hour < dawn || hour >= night) return 0f;
        if (hour < day) return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dawn, day, hour));
        if (hour < dusk) return 1f;
        return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(dusk, night, hour));
    }

    public void Register(SpriteRenderer sr)
    {
        if (!sr) return;
        registered.Add(sr);
        ApplyTo(sr);
    }

    public void Unregister(SpriteRenderer sr)
    {
        registered.Remove(sr);
    }

    private void ApplyBrightness()
    {
        foreach (var sr in registered)
        {
            if (!sr) continue;
            ApplyTo(sr);
        }
    }

    private void ApplyTo(SpriteRenderer sr)
    {
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat("_Brightness", Brightness01);
        sr.SetPropertyBlock(mpb);
    }

    private void ApplyGlobalLight()
    {
        if (!driveGlobalLight) return;
        var camera = CameraManager.Instance?.ActiveCamera;
        // Persistent services can outlive the scene light they originally bound.
        // Resolve against the local viewing scene instead of silently leaving its
        // replacement global light at the authored full-bright intensity.
        if (globalLight == null || (camera != null && globalLight.gameObject.scene != camera.gameObject.scene))
            globalLight = FindSceneGlobalLight(camera);
        if (globalLight == null) return;
        float ambient = camera != null ? EnvironmentDepthLighting.AmbientAt(camera.transform.position.y) : 1f;
        // URP's default lit sprite shader returns unlit albedo when a sorting
        // layer has no active lights. Keep the ambient light in the lighting
        // pass even at full darkness; this floor is below visible brightness.
        globalLight.intensity = Mathf.Max(0.000001f, Brightness01 * lightIntensityMultiplier * ambient);
    }

    public void RefreshAmbientLight() => ApplyGlobalLight();

    private static Light2D FindSceneGlobalLight(Camera camera = null)
    {
        Light2D[] lights = FindObjectsByType<Light2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            Light2D candidate = lights[i];
            if (candidate != null && candidate.lightType == Light2D.LightType.Global &&
                (camera == null || candidate.gameObject.scene == camera.gameObject.scene))
                return candidate;
        }

        return null;
    }
}
