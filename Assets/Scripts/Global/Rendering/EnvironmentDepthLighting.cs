using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Local environmental ambient loss and lamp lighting for unlit world renderers.</summary>
[DefaultExecutionOrder(15000)]
public sealed class EnvironmentDepthLighting : MonoBehaviour
{
    private const int MaxLights = 16;
    public static EnvironmentDepthLighting Instance { get; private set; }
    [SerializeField, Min(0)] private float fadeStartDepth = 10f;
    [SerializeField, Min(1)] private float blackDepth = 300f;
    private readonly Vector4[] positions = new Vector4[MaxLights];
    private readonly Vector4[] colors = new Vector4[MaxLights];
    private readonly Vector4[] cones = new Vector4[MaxLights];
    private Light2D[] lights = System.Array.Empty<Light2D>();
    private float nextScan;
    private int sceneHandle = -1;
    private int lightCount;
    private float waterY;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; Shader.SetGlobalFloat("_DontSinkDepthLightingEnabled", 0); }
    private void OnEnable()
    {
        // A newly loaded duplicate ServiceRoot may enable its children before
        // its singleton guard destroys it. It must not steal the live binding.
        if (Instance == null || !Instance.isActiveAndEnabled) Instance = this;
    }
    private void OnDisable()
    {
        if (Instance != this) return;
        Instance = null;
        Shader.SetGlobalFloat("_DontSinkDepthLightingEnabled", 0);
        GetComponent<GlobalBrightnessManager>()?.RefreshAmbientLight();
    }
    public static float AmbientAt(float worldY) => Instance != null
        ? DepthAmbient(worldY, Instance.waterY, Instance.fadeStartDepth, Instance.blackDepth) : 1f;
    public static float DepthAmbient(float worldY, float surfaceY, float start, float end)
    {
        float t = Mathf.InverseLerp(start, Mathf.Max(start + .001f, end), surfaceY - worldY);
        return 1f - t * t * (3f - 2f * t);
    }
    public static Color LightAt(Vector2 point)
    {
        if (Instance == null) return Color.white;
        float daylight = ServiceRoot.Instance?.Brightness?.Brightness01 ?? 1f;
        Vector3 illumination = Vector3.one * (AmbientAt(point.y) * daylight);
        for (int i = 0; i < Instance.lightCount; i++)
        {
            Vector4 p = Instance.positions[i], c = Instance.colors[i], cone = Instance.cones[i];
            Vector2 offset = point - new Vector2(p.x, p.y);
            float weight = LampWeight(offset, p.z, p.w, cone);
            illumination += new Vector3(c.x, c.y, c.z) * weight;
        }
        return new Color(Mathf.Clamp01(illumination.x), Mathf.Clamp01(illumination.y), Mathf.Clamp01(illumination.z), 1);
    }
    public static float LampWeight(Vector2 offset, float outer, float inner, Vector4 cone)
    {
        float distance = offset.magnitude;
        if (outer <= 0 || distance >= outer) return 0;
        float radial = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(inner, outer, distance));
        if (distance <= .0001f) return radial;
        float cosine = Vector2.Dot(offset / distance, new Vector2(cone.x, cone.y));
        float angular = cone.z <= -0.999f ? 1f
            : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(cone.w, Mathf.Max(cone.w + .0001f, cone.z), cosine));
        return radial * angular;
    }
    private void LateUpdate()
    {
        var owner = GetComponent<GlobalBrightnessManager>();
        var boundBrightness = ServiceRoot.Instance?.Brightness;
        if (boundBrightness != null && !ReferenceEquals(boundBrightness, owner)) return;
        // Recover after scene/bootstrap ordering temporarily cleared ownership.
        Instance = this;
        var camera = CameraManager.Instance?.ActiveCamera;
        if (camera == null)
        {
            lightCount = 0;
            Shader.SetGlobalFloat("_DontSinkDepthLightingEnabled", 0);
            GetComponent<GlobalBrightnessManager>()?.RefreshAmbientLight();
            return;
        }
        var waves = ServiceRoot.Instance?.WaveManager;
        waterY = waves != null ? waves.SampleSurfaceY(camera.transform.position.x) : 0;
        if (Time.unscaledTime >= nextScan || sceneHandle != camera.gameObject.scene.handle)
        {
            lights = FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            sceneHandle = camera.gameObject.scene.handle;
            nextScan = Time.unscaledTime + .5f;
        }
        // Closest lights win when more than sixteen overlap the local view.
        System.Array.Sort(lights, (a, b) => Distance(a, camera).CompareTo(Distance(b, camera)));
        lightCount = 0;
        foreach (var lamp in lights)
        {
            if (lightCount == MaxLights) break;
            if (lamp == null || !lamp.isActiveAndEnabled || lamp.intensity <= 0 || lamp.lightType != Light2D.LightType.Point ||
                lamp.gameObject.scene.handle != sceneHandle ||
                (SceneContext.Current != null && (lamp == SceneContext.Current.sunLight || lamp == SceneContext.Current.moonLight))) continue;
            Vector3 p = lamp.transform.position;
            positions[lightCount] = new Vector4(p.x, p.y, lamp.pointLightOuterRadius, lamp.pointLightInnerRadius);
            Color c = lamp.color * lamp.intensity;
            colors[lightCount] = new Vector4(c.r, c.g, c.b, 1);
            Vector2 direction = lamp.transform.up;
            cones[lightCount] = new Vector4(direction.x, direction.y,
                Mathf.Cos(lamp.pointLightInnerAngle * .5f * Mathf.Deg2Rad),
                Mathf.Cos(lamp.pointLightOuterAngle * .5f * Mathf.Deg2Rad));
            lightCount++;
        }
        Shader.SetGlobalFloat("_DontSinkDepthLightingEnabled", 1);
        Shader.SetGlobalVector("_DontSinkDepthLighting", new Vector4(waterY, fadeStartDepth, Mathf.Max(fadeStartDepth + 1, blackDepth),
            ServiceRoot.Instance?.Brightness?.Brightness01 ?? 1));
        Shader.SetGlobalInt("_DontSinkDepthLampCount", lightCount);
        Shader.SetGlobalVectorArray("_DontSinkDepthLampPositions", positions);
        Shader.SetGlobalVectorArray("_DontSinkDepthLampColors", colors);
        Shader.SetGlobalVectorArray("_DontSinkDepthLampCones", cones);
        GetComponent<GlobalBrightnessManager>()?.RefreshAmbientLight();
    }
    public void RefreshNow()
    {
        nextScan = 0;
        LateUpdate();
    }
    private static float Distance(Light2D light, Camera camera) => light != null
        ? ((Vector2)(light.transform.position - camera.transform.position)).sqrMagnitude : float.PositiveInfinity;
}
