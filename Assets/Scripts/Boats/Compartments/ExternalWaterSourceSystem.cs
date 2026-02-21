using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives external water sources into compartments (rain, sea breaches, hoses, etc.)
/// </summary>
public class ExternalWaterSourceSystem : MonoBehaviour
{
    [Header("Service Sources (must implement interfaces)")]
    [SerializeField] private MonoBehaviour rainSourceMono;
    [SerializeField] private WaveField waveField;

    private IRainService rainService;

    [Header("Boats to affect")]
    [SerializeField] private List<Boat> boats = new List<Boat>();
    [SerializeField] private bool useBoatRegistry = true;

    private IBoatRegistry _boatRegistry;

    private void Awake()
    {
        // Resolve the rain interface
        rainService = rainSourceMono as IRainService;
        if (rainService == null)
        {
            Debug.LogError($"{name}: rainSourceMono does not implement IRainService");
            return;
        }

        // Subscribe to rain events
        rainService.OnRainDropDensityChanged += OnRainDensityChanged;
    }

    private void OnEnable()
    {
        var ctx = SceneContext.Current;
        if (rainSourceMono == null && ctx != null)
            rainSourceMono = ctx.rainSourceMono;

        rainService = rainSourceMono as IRainService;
        if (rainService == null)
        {
            Debug.LogWarning("[ExternalWaterSourceSystem] No IRainService available in this scene. Rain disabled.");
            return;
        }

        rainService.OnRainDropDensityChanged += OnRainDensityChanged;

        if (useBoatRegistry)
            HookBoatRegistry();
    }

    private void OnDisable()
    {
        if (rainService != null)
            rainService.OnRainDropDensityChanged -= OnRainDensityChanged;

        if (_boatRegistry != null)
        {
            _boatRegistry.BoatAdded -= OnBoatAdded;
            _boatRegistry.BoatRemoved -= OnBoatRemoved;
            _boatRegistry = null;
        }
    }

    private void HookBoatRegistry()
    {
        var gs = GameState.I;
        _boatRegistry = gs != null ? gs.boatRegistry : null;

        if (_boatRegistry == null)
        {
            Debug.LogWarning("[ExternalWaterSourceSystem] No BoatRegistry available yet.");
            return;
        }

        _boatRegistry.BoatAdded += OnBoatAdded;
        _boatRegistry.BoatRemoved += OnBoatRemoved;

        // Seed current boats
        boats.Clear();
        boats.AddRange(_boatRegistry.Boats);
    }

    private void OnBoatAdded(Boat boat)
    {
        if (boat == null) return;
        if (!boats.Contains(boat))
            boats.Add(boat);
    }

    private void OnBoatRemoved(Boat boat)
    {
        if (boat == null) return;
        boats.Remove(boat);
    }

    private void OnDestroy()
    {
        if (rainService != null)
        {
            rainService.OnRainDropDensityChanged -= OnRainDensityChanged;
        }
    }

    private float rainMultiplier = 1f; // from 0 to 1

    private void OnRainDensityChanged(float density01)
    {
        // Map rain density to a multiplier for water sources
        rainMultiplier = Mathf.Clamp01(density01);
        // Optional debug
        //Debug.Log($"[ExternalWaterSourceSystem] Rain multiplier updated: {rainMultiplier:F2}");
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        foreach (var boat in boats)
        {
            foreach (var comp in boat.Compartments)
            {
                foreach (var src in comp.externalWaterSources)
                {
                    if (!src.IsActive)
                        continue;

                    switch (src.type)
                    {
                        case ExternalWaterSourceType.Rain:
                            float rainDelta = src.GetWaterContribution(dt) * rainMultiplier;
                            if (rainDelta > 0f)
                            {
                                float accepted = comp.AcceptWater(rainDelta);
                                if (accepted > 0f)
                                    Debug.Log($"[Rain] {comp.name} received {accepted:F3} m³ of rain water");
                            }
                            break;

                        case ExternalWaterSourceType.Sea:
                            if (waveField == null) break;

                            // Sample the sea height at the compartment top corners
                            Vector3 worldP0 = comp.transform.TransformPoint(comp.p0);
                            Vector3 worldP1 = comp.transform.TransformPoint(comp.p1);

                            float leftTopY = worldP0.y;
                            float rightTopY = worldP1.y;

                            float seaLeft = waveField.SampleHeight(worldP0.x);
                            float seaRight = waveField.SampleHeight(worldP1.x);

                            float deltaLeft = seaLeft - leftTopY;
                            float deltaRight = seaRight - rightTopY;

                            float deltaWater = Mathf.Max(deltaLeft, deltaRight);

                            //Debug.LogFormat(
                            //    "[Sea Debug] Compartment={0}\n" +
                            //    "TopLeftY={1:F3}, TopRightY={2:F3}\n" +
                            //    "SeaLeft={3:F3}, SeaRight={4:F3}\n" +
                            //    "DeltaLeft={5:F3}, DeltaRight={6:F3}, DeltaWater={7:F3}",
                            //    comp.name,
                            //    leftTopY, rightTopY,
                            //    seaLeft, seaRight,
                            //    deltaLeft, deltaRight,
                            //    deltaWater
                            //);

                            if (deltaWater > 0f)
                            {
                                float accepted = comp.AcceptWater(deltaWater);
                                if (accepted > 0f)
                                    Debug.Log($"[Sea] {comp.name} received {accepted:F3} m³ of sea water");
                            }
                            break;
                    }
                }
            }
        }
    }

}
