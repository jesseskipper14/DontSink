using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class SceneContext : MonoBehaviour
{
    public static SceneContext Current { get; private set; }

    [Header("Boat Scene Anchors")]
    public WaveField waveField;
    public MonoBehaviour rainSourceMono;   // must implement IRainService
    public Transform playerSpawn;          // tag Respawn optional

    [Header("Node Scene / Map UI")]
    public WorldMapGraphGenerator mapGenerator;
    //public WorldMapHeatmapController heatmap;
    public WorldMapRuntimeBinder runtimeBinder;
    public WorldMapTravelDebugController travelDebug;
    public NodeTravelController travelLauncher;

    [Header("Global Lighting")]
    public Light2D globalLight;

    [Header("Celestial Anchors")]
    public Transform sunTransform;
    public Light2D sunLight;
    public Transform moonTransform;
    public Light2D moonLight;
    public SpriteRenderer sunCorona;
    public Material coronaMaterial;

    [Header("Water Visuals")]
    public SpriteRenderer seaRenderer;
    public MeshRenderer sideWaterRenderer;

    [Header("Sky / Clouds")]
    public Renderer cloudRenderer;
    public SpriteRenderer starsRenderer;
    public SpriteRenderer sunriseOverlayRenderer;

    [Header("Optional Material Overrides")]
    public Material skyMaterialOverride;
    public Material starsMaterialOverride;
    public Material cloudMaterialOverride;
    public Material sunriseOverlayMaterialOverride;

    private void Awake()
    {
        Current = this;

        // Optional: auto-wire if missing (safe)
        if (waveField == null) waveField = FindAnyObjectByType<WaveField>();
        if (mapGenerator == null) mapGenerator = FindAnyObjectByType<WorldMapGraphGenerator>();
        //if (heatmap == null) heatmap = FindAnyObjectByType<WorldMapHeatmapController>();
        if (runtimeBinder == null) runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        if (travelDebug == null) travelDebug = FindAnyObjectByType<WorldMapTravelDebugController>();
        if (travelLauncher == null) travelLauncher = FindAnyObjectByType<NodeTravelController>();
        if (globalLight == null) globalLight = FindGlobalLight2D();

        // Note: sky/cloud renderers are intentionally NOT auto-wired here.
        // There are often multiple Renderers/SpriteRenderers in a scene; prefer explicit scene wiring.
    }

    private static Light2D FindGlobalLight2D()
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

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}