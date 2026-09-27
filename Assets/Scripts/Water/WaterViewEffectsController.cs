using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

/// <summary>
/// Supplies LOCAL view-dependent water shader data to all WaterMeshRenderers
/// under this object.
///
/// This component is presentation-only:
/// - no gameplay authority
/// - no replicated state
/// - no simulation mutation
///
/// Each multiplayer client therefore sees sun shafts relative to its own local
/// viewer while the shared ocean simulation remains identical.
/// </summary>
[DisallowMultipleComponent]
public sealed class WaterViewEffectsController : MonoBehaviour
{
    private static readonly int ViewerPositionId =
        Shader.PropertyToID(
            "_DontSinkViewerPositionWS");

    private static readonly int SunPositionId =
        Shader.PropertyToID(
            "_DontSinkSunPositionWS");

    private static readonly int SunVisibilityId =
        Shader.PropertyToID(
            "_DontSinkSunVisibility");

    private static readonly int RayColorId =
        Shader.PropertyToID(
            "_RayColor");

    private static readonly int SparkleVisibilityId =
        Shader.PropertyToID(
            "_DontSinkSparkleVisibility");

    private static readonly int SparkleIntensityId =
        Shader.PropertyToID(
            "_SparkleIntensity");

    [Header("Optional Overrides")]
    [Tooltip(
        "Usually leave blank. Falls back to a WaterMeshRenderer CenterTarget, " +
        "then Camera.main.")]
    [SerializeField] private Transform viewerOverride;

    [Header("Optional Celestial Overrides")]
    [Tooltip(
        "Usually leave blank. Falls back to SceneContext.Current.sunTransform.")]
    [SerializeField] private Transform sunOverride;

    [Tooltip(
        "Usually leave blank. Falls back to SceneContext.Current.moonTransform.")]
    [SerializeField] private Transform moonOverride;

    [Header("Celestial Ray Presentation")]
    [FormerlySerializedAs("sunVisibility")]
    [Range(0f, 1f)]
    [Tooltip(
        "Final presentation multiplier after celestial light intensity and horizon gating.")]
    [SerializeField] private float rayVisibilityMultiplier = 1f;

    [Min(0.01f)]
    [Tooltip(
        "Small near-horizon smoothing range. At or below the waterline, rays are exactly zero.")]
    [SerializeField] private float horizonFadeHeight = 1.25f;

    [Min(0.1f)]
    [Tooltip(
        "Height above the waterline at which a celestial body reaches full ray strength. " +
        "Use roughly the Sun/Moon arc's maximum height above the water.")]
    [SerializeField] private float celestialFullStrengthHeight = 25f;

    [Range(0.1f, 4f)]
    [Tooltip(
        "Shapes the rise from weak horizon rays to strong overhead rays. " +
        "1 = linear-ish SmoothStep, higher values keep low-angle light weaker for longer.")]
    [SerializeField] private float celestialElevationPower = 1.35f;

    [Range(0f, 2f)]
    [SerializeField] private float sunRayMultiplier = 1f;

    [Range(0f, 2f)]
    [SerializeField] private float moonRayMultiplier = 1f;

    [Range(0f, 1f)]
    [Tooltip(
        "How strongly the existing sunrise/sunset overlay tint influences solar ray color.")]
    [SerializeField] private float atmosphereTintStrength = 1f;

    [Header("Celestial Sparkles")]
    [Range(0f, 2f)]
    [Tooltip(
        "Base sparkle intensity owned by this controller. This intentionally overrides " +
        "the old WaterVisualManager brightness-driven sparkle value on the side-water renderer.")]
    [SerializeField] private float sparkleBaseIntensity = 0.35f;

    [Range(0f, 3f)]
    [SerializeField] private float sunSparkleMultiplier = 1f;

    [Range(0f, 3f)]
    [Tooltip(
        "Moon sparkles often need a little perceptual lift because moon Light2D intensity " +
        "is intentionally much lower than the Sun.")]
    [SerializeField] private float moonSparkleMultiplier = 1.35f;

    [Range(0.1f, 2f)]
    [Tooltip(
        "Shapes celestial strength specifically for sparkles. Values below 1 make weaker " +
        "moonlight remain visible without making the actual moon rays equally bright.")]
    [SerializeField] private float sparkleResponsePower = 0.55f;

    [Header("Waterline")]
    [Tooltip(
        "Optional. Auto-resolves ServiceRoot/WaveManager, then scene WaveManager.")]
    [SerializeField] private WaveManager waveManager;

    [SerializeField] private float fallbackWaterLevelY = 0f;

    [Header("Targets")]
    [Tooltip(
        "Usually leave empty. All child WaterMeshRenderers, including inactive " +
        "Ocean_Back / Ocean_Front objects, are auto-resolved.")]
    [SerializeField] private WaterMeshRenderer[] waterRenderers;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        ResolveTargets();
        ResolveRefs();
        EnsurePropertyBlock();
    }

    private void OnEnable()
    {
        ResolveTargets();
        ResolveRefs();
        EnsurePropertyBlock();
        ApplyNow();
    }

    private void LateUpdate()
    {
        ApplyNow();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        rayVisibilityMultiplier =
            Mathf.Clamp01(
                rayVisibilityMultiplier);

        horizonFadeHeight =
            Mathf.Max(
                0.01f,
                horizonFadeHeight);

        celestialFullStrengthHeight =
            Mathf.Max(
                0.1f,
                celestialFullStrengthHeight);

        celestialElevationPower =
            Mathf.Clamp(
                celestialElevationPower,
                0.1f,
                4f);

        sparkleBaseIntensity =
            Mathf.Clamp(
                sparkleBaseIntensity,
                0f,
                2f);

        sparkleResponsePower =
            Mathf.Clamp(
                sparkleResponsePower,
                0.1f,
                2f);

        if (!Application.isPlaying)
            return;

        ResolveTargets();
        ApplyNow();
    }
#endif

    [ContextMenu("Reapply Water View Effects")]
    public void ApplyNow()
    {
        ResolveTargets();
        EnsurePropertyBlock();

        ResolveRefs();

        Transform viewer =
            ResolveViewer();

        Vector4 viewerPosition =
            viewer != null
                ? new Vector4(
                    viewer.position.x,
                    viewer.position.y,
                    viewer.position.z,
                    1f)
                : Vector4.zero;

        ResolveCelestialSource(
            viewer,
            out Transform celestial,
            out Light2D celestialLight,
            out bool usingMoon,
            out float resolvedVisibility,
            out Color resolvedRayColor);

        Vector4 celestialPosition =
            celestial != null
                ? new Vector4(
                    celestial.position.x,
                    celestial.position.y,
                    celestial.position.z,
                    1f)
                : Vector4.zero;

        float sparkleSourceMultiplier =
            usingMoon
                ? moonSparkleMultiplier
                : sunSparkleMultiplier;

        float sparkleVisibility =
            resolvedVisibility > 0f
                ? Mathf.Clamp01(
                    Mathf.Pow(
                        resolvedVisibility,
                        sparkleResponsePower) *
                    sparkleSourceMultiplier)
                : 0f;

        if (waterRenderers == null)
            return;

        for (int i = 0;
             i < waterRenderers.Length;
             i++)
        {
            WaterMeshRenderer water =
                waterRenderers[i];

            if (water == null)
                continue;

            MeshRenderer renderer =
                water.GetComponent<MeshRenderer>();

            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(
                _propertyBlock);

            _propertyBlock.SetVector(
                ViewerPositionId,
                viewerPosition);

            _propertyBlock.SetVector(
                SunPositionId,
                celestialPosition);

            _propertyBlock.SetFloat(
                SunVisibilityId,
                resolvedVisibility);

            _propertyBlock.SetColor(
                RayColorId,
                resolvedRayColor);

            _propertyBlock.SetFloat(
                SparkleVisibilityId,
                sparkleVisibility);

            // Take presentation ownership of the side-water sparkle base value.
            // The legacy WaterVisualManager still writes _SparkleIntensity on
            // the shared material from global brightness. A property block wins
            // on these renderers, which lets moonlight create sparkles at night.
            _propertyBlock.SetFloat(
                SparkleIntensityId,
                sparkleBaseIntensity);

            renderer.SetPropertyBlock(
                _propertyBlock);
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[WaterViewEffectsController:{name}] " +
                $"viewer={(viewer != null ? viewer.name : "NULL")} " +
                $"source={(celestial != null ? celestial.name : "NONE")} " +
                $"kind={(celestial != null ? (usingMoon ? "Moon" : "Sun") : "None")} " +
                $"light={(celestialLight != null ? celestialLight.intensity.ToString("0.00") : "none")} " +
                $"visibility={resolvedVisibility:0.00} " +
                $"sparkles={sparkleVisibility:0.00} " +
                $"rayColor=#{ColorUtility.ToHtmlStringRGB(resolvedRayColor)} " +
                $"targets={(waterRenderers != null ? waterRenderers.Length : 0)}",
                this);
        }
    }

    private void ResolveTargets()
    {
        bool hasAny =
            waterRenderers != null &&
            waterRenderers.Length > 0;

        if (hasAny)
            return;

        waterRenderers =
            GetComponentsInChildren<WaterMeshRenderer>(
                includeInactive: true);
    }

    private Transform ResolveViewer()
    {
        if (viewerOverride != null)
            return viewerOverride;

        if (waterRenderers != null)
        {
            for (int i = 0;
                 i < waterRenderers.Length;
                 i++)
            {
                WaterMeshRenderer water =
                    waterRenderers[i];

                if (water == null ||
                    water.CenterTarget == null)
                {
                    continue;
                }

                return water.CenterTarget;
            }
        }

        Camera camera =
            Camera.main;

        return
            camera != null
                ? camera.transform
                : null;
    }

    private void ResolveRefs()
    {
        if (ServiceRoot.Instance != null &&
            ServiceRoot.Instance.WaveManager != null)
        {
            waveManager =
                ServiceRoot.Instance.WaveManager;
        }
        else if (waveManager == null)
        {
            waveManager =
                FindFirstObjectByType<WaveManager>();
        }
    }

    private void ResolveCelestialSource(
        Transform viewer,
        out Transform celestial,
        out Light2D celestialLight,
        out bool usingMoon,
        out float visibility,
        out Color rayColor)
    {
        celestial = null;
        celestialLight = null;
        usingMoon = false;
        visibility = 0f;
        rayColor = Color.white;

        Transform sun =
            ResolveSunTransform();

        Transform moon =
            ResolveMoonTransform();

        Light2D sunLight =
            ResolveSunLight(
                sun);

        Light2D moonLight =
            ResolveMoonLight(
                moon);

        float waterLevelY =
            ResolveWaterLevelY(
                viewer);

        float sunScore =
            EvaluateCelestialScore(
                sun,
                sunLight,
                waterLevelY,
                sunRayMultiplier);

        float moonScore =
            EvaluateCelestialScore(
                moon,
                moonLight,
                waterLevelY,
                moonRayMultiplier);

        if (sunScore <= 0f &&
            moonScore <= 0f)
        {
            // Deliberately no celestial position. The shader sees w=0 and
            // completely skips the ray pass. No below-horizon geometry crimes.
            return;
        }

        usingMoon =
            moonScore > sunScore;

        celestial =
            usingMoon
                ? moon
                : sun;

        celestialLight =
            usingMoon
                ? moonLight
                : sunLight;

        float winningScore =
            usingMoon
                ? moonScore
                : sunScore;

        visibility =
            Mathf.Clamp01(
                winningScore *
                rayVisibilityMultiplier);

        rayColor =
            ResolveSourceColor(
                celestialLight,
                usingMoon);
    }

    private float EvaluateCelestialScore(
        Transform celestial,
        Light2D celestialLight,
        float waterLevelY,
        float sourceMultiplier)
    {
        if (celestial == null ||
            !celestial.gameObject.activeInHierarchy)
        {
            return 0f;
        }

        float heightAboveWater =
            celestial.position.y -
            waterLevelY;

        if (heightAboveWater <= 0f)
        {
            // Requirement: a body at/below the waterline contributes exactly
            // zero rays, regardless of its Light2D intensity.
            return 0f;
        }

        float horizonVisibility =
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                    heightAboveWater /
                    Mathf.Max(
                        0.01f,
                        horizonFadeHeight)));

        // Full-arc elevation strength:
        // horizon ~= 0, overhead / arc apex ~= 1.
        //
        // This is intentionally independent of Light2D intensity, then multiplied
        // by it below. So the water presentation respects BOTH celestial elevation
        // and the game's existing dynamic-light strength.
        float elevation01 =
            Mathf.Clamp01(
                heightAboveWater /
                Mathf.Max(
                    0.1f,
                    celestialFullStrengthHeight));

        float elevationStrength =
            Mathf.Pow(
                Mathf.SmoothStep(
                    0f,
                    1f,
                    elevation01),
                celestialElevationPower);

        float lightIntensity =
            celestialLight != null &&
            celestialLight.enabled &&
            celestialLight.gameObject.activeInHierarchy
                ? Mathf.Max(
                    0f,
                    celestialLight.intensity)
                : 1f;

        return
            horizonVisibility *
            elevationStrength *
            lightIntensity *
            Mathf.Max(
                0f,
                sourceMultiplier);
    }

    private Color ResolveSourceColor(
        Light2D celestialLight,
        bool usingMoon)
    {
        Color baseColor =
            celestialLight != null
                ? celestialLight.color
                : Color.white;

        if (usingMoon)
            return baseColor;

        SunriseSunsetOverlayManager atmosphere =
            ResolveAtmosphere();

        if (atmosphere == null ||
            atmosphere.Tint01 <= 0f)
        {
            return baseColor;
        }

        float tintAmount =
            Mathf.Clamp01(
                atmosphere.Tint01 *
                atmosphereTintStrength);

        return
            Color.Lerp(
                baseColor,
                atmosphere.CurrentTintColor,
                tintAmount);
    }

    private float ResolveWaterLevelY(
        Transform viewer)
    {
        if (waveManager != null)
        {
            float sampleX =
                viewer != null
                    ? viewer.position.x
                    : 0f;

            return
                waveManager.SampleSurfaceY(
                    sampleX);
        }

        return fallbackWaterLevelY;
    }

    private Transform ResolveSunTransform()
    {
        if (sunOverride != null)
            return sunOverride;

        return
            SceneContext.Current != null
                ? SceneContext.Current.sunTransform
                : null;
    }

    private Transform ResolveMoonTransform()
    {
        if (moonOverride != null)
            return moonOverride;

        return
            SceneContext.Current != null
                ? SceneContext.Current.moonTransform
                : null;
    }

    private static Light2D ResolveSunLight(
        Transform sun)
    {
        if (SceneContext.Current != null &&
            SceneContext.Current.sunLight != null)
        {
            return
                SceneContext.Current.sunLight;
        }

        return
            sun != null
                ? sun.GetComponent<Light2D>()
                : null;
    }

    private static Light2D ResolveMoonLight(
        Transform moon)
    {
        if (SceneContext.Current != null &&
            SceneContext.Current.moonLight != null)
        {
            return
                SceneContext.Current.moonLight;
        }

        return
            moon != null
                ? moon.GetComponent<Light2D>()
                : null;
    }

    private static SunriseSunsetOverlayManager ResolveAtmosphere()
    {
        if (ServiceRoot.Instance != null &&
            ServiceRoot.Instance.SunriseSunset is SunriseSunsetOverlayManager manager)
        {
            return manager;
        }

        return
            FindFirstObjectByType<SunriseSunsetOverlayManager>();
    }

    private void EnsurePropertyBlock()
    {
        if (_propertyBlock == null)
            _propertyBlock =
                new MaterialPropertyBlock();
    }
}
