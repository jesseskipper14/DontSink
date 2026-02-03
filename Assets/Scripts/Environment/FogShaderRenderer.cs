using UnityEngine;

public class FogShaderRenderer : MonoBehaviour
{
    [Header("Service Source (must implement interfaces)")]
    [SerializeField] private MonoBehaviour fogSource;

    [Header("Material")]
    [SerializeField] private Material fogMaterial;

    [Header("Fog Mapping")]
    [SerializeField] private float maxFogIntensity = 0.75f;
    [SerializeField] private float maxFogBrightness = 1.0f;

    private IFogService fog;

    private static readonly int FogAlpha = Shader.PropertyToID("_FogAlpha");
    private static readonly int Brightness = Shader.PropertyToID("_Brightness");

    private void Awake()
    {
        // Resolve interfaces
        fog = fogSource as IFogService;

        if (fog == null)
            Debug.LogError($"{name}: fogSource does not implement IFogService");

        if (fogMaterial == null)
            Debug.LogError($"{name}: Fog material not assigned");

        // Subscribe
        if (fog != null)
        {
            fog.OnFogIntensityChanged += UpdateFogIntensity;
            fog.OnFogBrightnessChanged += UpdateFogBrightness;
        }

        // Initial sync
        if (fog != null)
        {
            UpdateFogIntensity(fog.FogIntensity);
            UpdateFogBrightness(fog.FogBrightness);
        }
    }

    private void OnDestroy()
    {
        if (fog != null)
        {
            fog.OnFogIntensityChanged -= UpdateFogIntensity;
            fog.OnFogBrightnessChanged -= UpdateFogBrightness;
        }
    }

    // =========================
    // Event Handlers
    // =========================

    private void UpdateFogIntensity(float intensity01)
    {
        if (!fogMaterial) return;

        float currentFogIntensity = Mathf.Lerp(0, maxFogIntensity, intensity01);

        fogMaterial.SetFloat(FogAlpha, currentFogIntensity);
    }

    private void UpdateFogBrightness(float intensity01)
    {
        if (!fogMaterial) return;

        float currentFogBrightness = Mathf.Lerp(0, maxFogBrightness, intensity01);

        fogMaterial.SetFloat(Brightness, maxFogBrightness);
    }

#if UNITY_EDITOR
    [ContextMenu("Test Fog Intensity 50%")]
    private void TestFogInt50() => UpdateFogIntensity(0.5f);

    [ContextMenu("Test Fog Intensity 100%")]
    private void TestFogInt100() => UpdateFogIntensity(1f);

    [ContextMenu("Test Fog Brightness 50%")]
    private void TestFogBrt50() => UpdateFogBrightness(0.5f);

    [ContextMenu("Test Fog Brightness 100%")]
    private void TestFogBrt100() => UpdateFogBrightness(1f);
#endif
}
