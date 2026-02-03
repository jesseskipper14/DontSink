using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class RainParticleRender : MonoBehaviour
{
    [Header("Services (set via Initialize)")]
    [SerializeField] private RainManager rainManager;
    [SerializeField] private WindManager windManager;

    private ParticleSystem ps;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        SetupParticleSystem();

        // Subscribe to rain
        if (rainManager != null)
            rainManager.OnRainDropDensityChanged += UpdateRain;

        // Subscribe to wind
        if (windManager != null)
            windManager.OnWindChanged += UpdateWind;

        // Initialize with current values
        if (rainManager != null)
            UpdateRain(rainManager.RainDropDensity);
        if (windManager != null)
            UpdateWind(windManager.WindStrength01);
    }

    private void OnDestroy()
    {
        if (rainManager != null)
            rainManager.OnRainDropDensityChanged -= UpdateRain;

        if (windManager != null)
            windManager.OnWindChanged -= UpdateWind;
    }

    private void SetupParticleSystem()
    {
        if (!ps) return;

        // Main module
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 3f;
        main.startSpeed = 20f;
        main.startSize = 0.05f;
        main.startColor = new Color(0.6f, 0.6f, 0.8f, 0.7f);
        main.maxParticles = 1200;

        // Shape
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(300f, 1f, 1f);
        shape.position = new Vector3(0f, 55f, 0f);

        // Velocity
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;

        // Set curves in CONSTANT mode to avoid "all curves must be in same mode" error
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 1f);
        velocity.y = new ParticleSystem.MinMaxCurve(-10f, -5f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ps.Clear();
        ps.Play();
    }

    private void UpdateRain(float intensity)
    {
        if (!ps) return;

        var emission = ps.emission;
        emission.rateOverTime = Mathf.Lerp(0f, 2000f, intensity);
    }

    private void UpdateWind(float windStrength01)
    {
        if (!ps || windManager == null) return;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;

        float windSpeed = windStrength01 * windManager.MaxWindSpeed;
        vel.x = new ParticleSystem.MinMaxCurve(-windSpeed, windSpeed); // X-axis affected by wind
        vel.y = new ParticleSystem.MinMaxCurve(-10f, -10f);                  // constant fall speed
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

#if UNITY_EDITOR
    [ContextMenu("Test Rain 50%")]
    private void TestRain50() => UpdateRain(0.5f);

    [ContextMenu("Test Rain 100%")]
    private void TestRain100() => UpdateRain(1f);

    [ContextMenu("Test Wind 50%")]
    private void TestWind50() => UpdateWind(0.5f);

    [ContextMenu("Test Wind 100%")]
    private void TestWind100() => UpdateWind(1f);
#endif
}
