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
    [SerializeField] private Light2D globalLight;              // drag your scene Global Light here
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

        // Safety: auto-find a global Light2D if not assigned
        if (globalLight == null)
        {
            var lights = FindObjectsByType<Light2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].lightType == Light2D.LightType.Global)
                {
                    globalLight = lights[i];
                    break;
                }
            }
        }
    }

    public void Initialize(ITimeOfDayService time)
    {
        timeService = time;
        timeService.OnTimeChanged += HandleTimeChanged;

        HandleTimeChanged(timeService.CurrentTime);
    }

    private void OnDestroy()
    {
        if (timeService != null)
            timeService.OnTimeChanged -= HandleTimeChanged;
    }

    private void HandleTimeChanged(float hour)
    {
        float t01 = hour / 24f;
        float curveValue = brightnessCurve.Evaluate(t01);
        float newBrightness = Mathf.Lerp(minBrightness, maxBrightness, curveValue);

        if (Mathf.Approximately(newBrightness, Brightness01))
            return;

        Brightness01 = newBrightness;

        ApplyBrightness();
        ApplyGlobalLight();

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
}