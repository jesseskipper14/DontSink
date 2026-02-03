using UnityEngine;

public class RainShaderRenderer : MonoBehaviour
{
    [Header("Service Sources (must implement interfaces)")]
    [SerializeField] private MonoBehaviour rainSource;
    [SerializeField] private MonoBehaviour windSource;

    [Header("Material")]
    [SerializeField] private Material rainMaterial;

    [Header("Rain Mapping")]
    [SerializeField] private int maxRaindrops = 2000;
    [SerializeField] private float minRainSpeed = 0.05f;
    [SerializeField] private float maxRainSpeed = 2.0f;

    [Header("Wind Mapping")]
    [SerializeField] private float maxWindStrength = 0.5f;

    private IRainService rain;
    private IWindService wind;

    private static readonly int RaindropCountID = Shader.PropertyToID("_RaindropCount");
    private static readonly int RainSpeedID = Shader.PropertyToID("_RainSpeed");
    private static readonly int WindID = Shader.PropertyToID("_Wind");

    private void Awake()
    {
        // Resolve interfaces
        rain = rainSource as IRainService;
        wind = windSource as IWindService;

        if (rain == null)
            Debug.LogError($"{name}: rainSource does not implement IRainService");

        if (wind == null)
            Debug.LogError($"{name}: windSource does not implement IWindService");

        if (rainMaterial == null)
            Debug.LogError($"{name}: Rain material not assigned");

        // Subscribe (RAIN — REFACTORED)
        if (rain != null)
        {
            rain.OnRainDropDensityChanged += UpdateRainDropDensity;
            rain.OnRainFallSpeedChanged += UpdateRainFallSpeed;
        }

        // Subscribe (WIND — DO NOT TOUCH)
        if (wind != null)
            wind.OnWindChanged += UpdateWind;

        if (wind is WindManager wm)
            wm.OnWindTransitionStarted += ResetRainTime;

        // Initial sync
        if (rain != null)
        {
            UpdateRainDropDensity(rain.RainDropDensity);
            UpdateRainFallSpeed(rain.RainFallSpeed);
        }

        if (wind != null)
            UpdateWind(wind.WindStrength01);

        rainMaterial.SetFloat("_TimeOffset", Time.time * 2f);
    }

    private void OnDestroy()
    {
        if (rain != null)
        {
            rain.OnRainDropDensityChanged -= UpdateRainDropDensity;
            rain.OnRainFallSpeedChanged -= UpdateRainFallSpeed;
        }

        if (wind != null)
            wind.OnWindChanged -= UpdateWind;
    }

    // =========================
    // Event Handlers (RAIN)
    // =========================

    private void UpdateRainDropDensity(float density01)
    {
        if (!rainMaterial) return;

        int dropCount = Mathf.RoundToInt(
            Mathf.Lerp(0, maxRaindrops, density01)
        );

        rainMaterial.SetFloat(RaindropCountID, dropCount);
    }

    private void UpdateRainFallSpeed(float speed01)
    {
        if (!rainMaterial) return;

        float speed = Mathf.Lerp(minRainSpeed, maxRainSpeed, speed01);

        // Negative because rain goes down
        rainMaterial.SetFloat(RainSpeedID, -speed);
    }

    // =========================
    // Event Handlers (WIND)
    // =========================

    private void UpdateWind(float windStrength01)
    {
        if (!rainMaterial) return; 

        float windX = windStrength01 * maxWindStrength; 
        Vector2 windVector = new Vector2(windX, 0f); 

        rainMaterial.SetVector(WindID, windVector);

        // Reset local time origin 
        //rainMaterial.SetFloat("_TimeOffset", Time.time);
    }

    private void ResetRainTime()
    {
        if (!rainMaterial) return;
        rainMaterial.SetFloat("_TimeOffset", Time.time);
    }

#if UNITY_EDITOR
    [ContextMenu("Test Rain Density 50%")]
    private void TestRainDensity50() => UpdateRainDropDensity(0.5f);

    [ContextMenu("Test Rain Speed 50%")]
    private void TestRainSpeed50() => UpdateRainFallSpeed(0.5f);
#endif
}
