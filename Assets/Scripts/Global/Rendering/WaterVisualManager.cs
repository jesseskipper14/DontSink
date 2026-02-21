using UnityEngine;

public class WaterVisualManager : MonoBehaviour, IWaterVisualService
{
    [Header("Sea")]
    [SerializeField] private SpriteRenderer seaRenderer;
    [SerializeField] private Material seaMaterial;

    [Header("Sea Brightness")]
    [SerializeField] private float seaBrightnessMin = 0.1f;
    [SerializeField] private float seaBrightnessMax = 1f;

    [Header("Sea Sparkles")]
    [SerializeField] private float sparkleMin = 0f;
    [SerializeField] private float sparkleMax = 1.2f;

    [Header("Side Water")]
    [SerializeField] private MeshRenderer sideWaterRenderer;
    [SerializeField] private Material sideWaterMaterial;

    [Header("Foam")]
    [SerializeField, Range(0f, 1f)] private float foamIntensity = 1f;

    private IBrightnessService brightness;

    public void Initialize(IBrightnessService brightnessService)
    {
        // Unsubscribe if re-initialized
        if (brightness != null)
            brightness.OnBrightnessChanged -= OnBrightnessChanged;

        brightness = brightnessService;

        if (brightness != null)
            brightness.OnBrightnessChanged += OnBrightnessChanged;

        CacheMaterials();
        RefreshAllSafe();
    }

    /// <summary>
    /// Called on scene load to reconnect renderers/materials that live in the scene.
    /// Safe to call repeatedly.
    /// </summary>
    public void RebindSceneAnchors(SpriteRenderer sea, MeshRenderer sideWater)
    {
        seaRenderer = sea;
        sideWaterRenderer = sideWater;

        // Materials belong to renderers; recache them.
        seaMaterial = null;
        sideWaterMaterial = null;

        CacheMaterials();
        RefreshAllSafe();
    }

    private void OnDestroy()
    {
        if (brightness != null)
            brightness.OnBrightnessChanged -= OnBrightnessChanged;
    }

    public void OnBrightnessChanged(float brightness01)
    {
        UpdateBrightness(brightness01);
        UpdateSparkles(brightness01);
    }

    private void CacheMaterials()
    {
        if (seaRenderer && seaMaterial == null)
            seaMaterial = seaRenderer.sharedMaterial;

        if (sideWaterRenderer && sideWaterMaterial == null)
            sideWaterMaterial = sideWaterRenderer.sharedMaterial;
    }

    private void RefreshAllSafe()
    {
        if (brightness == null) return;

        float b = brightness.Brightness01;
        UpdateSparkles(b);
        UpdateBrightness(b);
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