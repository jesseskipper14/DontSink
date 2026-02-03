using UnityEngine;

public class WaterVisualManager : MonoBehaviour, IWaterVisualService
{
    // =====================================================
    // Sea
    // =====================================================
    [Header("Sea")]
    [SerializeField] private SpriteRenderer seaRenderer;
    [SerializeField] private Material seaMaterial;

    [Header("Sea Brightness")]
    [SerializeField] private float seaBrightnessMin = 0.1f;
    [SerializeField] private float seaBrightnessMax = 1f;

    [Header("Sea Sparkles")]
    [SerializeField] private float sparkleMin = 0f;
    [SerializeField] private float sparkleMax = 1.2f;

    // =====================================================
    // Side Water
    // =====================================================
    [Header("Side Water")]
    [SerializeField] private MeshRenderer sideWaterRenderer;
    [SerializeField] private Material sideWaterMaterial;

    [Header("Foam")]
    [SerializeField, Range(0f, 1f)] private float foamIntensity = 1f;

    // =====================================================

    private ITimeOfDayService time;
    private IBrightnessService brightness;

    public void Initialize(IBrightnessService brightnessService)
    {
        brightness = brightnessService;
        brightness.OnBrightnessChanged += OnBrightnessChanged;

        CacheMaterials();
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (brightness != null)
            brightness.OnBrightnessChanged -= OnBrightnessChanged;
    }

    private void CacheMaterials()
    {
        if (seaRenderer && seaMaterial == null)
            seaMaterial = seaRenderer.sharedMaterial;

        if (sideWaterRenderer && sideWaterMaterial == null)
            sideWaterMaterial = sideWaterRenderer.sharedMaterial;
    }

    public void OnBrightnessChanged(float brightness01)
    {
        UpdateBrightness(brightness01);
        UpdateSparkles(brightness01);
    }

    private void RefreshAll()
    {
        UpdateSparkles(brightness.Brightness01);
        UpdateBrightness(brightness.Brightness01);
        UpdateFoam();
    }

    private void UpdateBrightness(float brightness01)
    {
        if (seaMaterial)
        {
            float seaBrightness = Mathf.Lerp(seaBrightnessMin, seaBrightnessMax, brightness01);
            seaMaterial.SetFloat("_Brightness", seaBrightness);
        }

        if (sideWaterMaterial)
        {
            float sideBrightness = Mathf.Lerp(seaBrightnessMin, seaBrightnessMax, brightness01);
            sideWaterMaterial.SetFloat("_Brightness", sideBrightness);
        }
    }

    private void UpdateSparkles(float brightness01)
    {
        float sparkleBrightness = Mathf.Lerp(sparkleMin, sparkleMax, brightness01);
        if (seaMaterial)
            seaMaterial.SetFloat("_SparkleIntensity", sparkleBrightness);

        if (sideWaterMaterial)
            sideWaterMaterial.SetFloat("_SparkleIntensity", sparkleBrightness);
    }

    private void UpdateFoam()
    {
        if (sideWaterMaterial)
            sideWaterMaterial.SetFloat("_FoamIntensity", foamIntensity);
    }
}

