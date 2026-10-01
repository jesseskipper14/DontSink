using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class GlobalBrightnessManager : MonoBehaviour, IBrightnessService
{
    [Header("Brightness Mapping")]
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
        float t01 = hour / 24f;
        float curveValue = brightnessCurve.Evaluate(t01);
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
        if (globalLight == null) return;

        globalLight.intensity = Mathf.Max(0f, Brightness01 * lightIntensityMultiplier);
    }

    private static Light2D FindSceneGlobalLight()
    {
        Light2D[] lights = FindObjectsByType<Light2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            Light2D candidate = lights[i];
            if (candidate != null && candidate.lightType == Light2D.LightType.Global)
                return candidate;
        }

        return null;
    }
}
