using UnityEngine;
using UnityEngine.UI;

public class WorldMapHeatmapController : MonoBehaviour
{
    public enum HeatmapMode
    {
        None,
        Prosperity,
        Stability,
        Security,
        DockRating,
        TradeRating,
        Population,
        FoodBalance
    }

    [Header("Auto Refresh")]
    public bool autoRefresh = true;
    [Min(0.1f)] public float refreshIntervalSeconds = 1f;

    private float _refreshTimer;

    [Header("Refs")]
    [SerializeField] private WorldMapNodeSpawner spawner;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;

    [Header("UI (optional)")]
    [SerializeField] private Text modeLabel;

    [Header("Colors")]
    public Color lowColor = new Color(1f, 0.25f, 0.25f, 1f);   // red-ish
    public Color highColor = new Color(0.25f, 1f, 0.35f, 1f);  // green-ish
    public Color unknownColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Normalization")]
    [Range(0f, 4f)] public float minValue = 0f;
    [Range(0f, 4f)] public float maxValue = 4f;

    [Header("Population Heatmap")]
    public float populationMin = 0f;
    public float populationMax = 5000f;
    public Color populationLow = new Color(0.15f, 0.15f, 0.35f, 1f);   // dark blue
    public Color populationHigh = new Color(0.55f, 0.75f, 1f, 1f);     // bright blue

    [Header("FoodBalance Heatmap")]
    public float foodMin = -4f;
    public float foodMax = 4f;
    public Color foodNegative = new Color(1f, 0.25f, 0.25f, 1f);       // red-ish
    public Color foodNeutral = new Color(0.75f, 0.75f, 0.75f, 1f);     // gray
    public Color foodPositive = new Color(0.25f, 1f, 0.35f, 1f);       // green-ish

    [SerializeField] private HeatmapMode mode = HeatmapMode.None;

    [SerializeField] private CanvasGroup overlayRoot; // MapOverlayRoot

    private bool OverlayVisible()
    {
        if (overlayRoot == null) return true; // fail-open if not assigned
        return overlayRoot.alpha > 0.001f && overlayRoot.blocksRaycasts;
    }

    private void Reset()
    {
        spawner = FindAnyObjectByType<WorldMapNodeSpawner>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
    }

    private void Start()
    {
        if (mode != HeatmapMode.None)
            Apply();
    }

    // Hook these to buttons, or call from code.
    public void SetModeNone() => SetMode(HeatmapMode.None);
    public void SetModeProsperity() => SetMode(HeatmapMode.Prosperity);
    public void SetModeStability() => SetMode(HeatmapMode.Stability);
    public void SetModeSecurity() => SetMode(HeatmapMode.Security);
    public void SetModeDock() => SetMode(HeatmapMode.DockRating);
    public void SetModeTrade() => SetMode(HeatmapMode.TradeRating);
    public void SetModePopulation() => SetMode(HeatmapMode.Population);
    public void SetModeFoodBalance() => SetMode(HeatmapMode.FoodBalance);


    private void Update()
    {
        if (!OverlayVisible()) return;
        if (!autoRefresh) return;
        if (mode == HeatmapMode.None) return;

        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= refreshIntervalSeconds)
        {
            _refreshTimer = 0f;
            Apply();
        }
    }

    public void SetMode(HeatmapMode newMode)
    {
        mode = newMode;
        Apply();
    }

    [ContextMenu("Apply Heatmap")]
    public void Apply()
    {
        if (!OverlayVisible()) return;
        if (modeLabel != null)
            modeLabel.text = $"Heatmap: {mode}";

        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;

        // Best: use spawner’s node view container to find views.
        // Works even if you later respawn.
        MapNodeView[] views;
        if (spawner != null && spawner.gameObject.activeInHierarchy)
            views = spawner.GetComponentsInChildren<MapNodeView>(includeInactive: false);
        else
            views = FindObjectsByType<MapNodeView>(FindObjectsSortMode.None);

        for (int i = 0; i < views.Length; i++)
        {
            var v = views[i];
            int id = v.NodeId;
            if (id < 0) continue;

            if (runtimeBinder == null || !runtimeBinder.IsBuilt || !runtimeBinder.Registry.TryGetByIndex(id, out var rt))
            {
                v.SetTint(unknownColor);
                continue;
            }

            float value;
            bool hasValue = TryGetValue(rt, out value);

            if (!hasValue)
            {
                v.SetTint(unknownColor);
                continue;
            }

            v.SetTint(EvaluateColor(mode, value));
        }
    }

    private bool TryGetValue(MapNodeRuntime rt, out float value)
    {
        value = 0f;
        var s = rt.State;
        if (s == null) return false;

        switch (mode)
        {
            case HeatmapMode.None:
                return false;

            case HeatmapMode.DockRating:
                value = s.GetStat(NodeStatId.DockRating).value;
                return true;

            case HeatmapMode.TradeRating:
                value = s.GetStat(NodeStatId.TradeRating).value;
                return true;

            case HeatmapMode.Prosperity:
                value = s.GetStat(NodeStatId.Prosperity).value;
                return true;

            case HeatmapMode.Stability:
                value = s.GetStat(NodeStatId.Stability).value;
                return true;

            case HeatmapMode.Security:
                value = s.GetStat(NodeStatId.Security).value;
                return true;

            case HeatmapMode.Population:
                value = s.population;
                return true;

            case HeatmapMode.FoodBalance:
                value = s.GetStat(NodeStatId.FoodBalance).value;
                return true;

            default:
                return false;
        }
    }


    private Color EvaluateColor(HeatmapMode m, float value)
    {
        switch (m)
        {
            case HeatmapMode.Population:
                return EvaluatePopulation(value);

            case HeatmapMode.FoodBalance:
                return EvaluateFoodBalance(value);

            // Everything else stays on the old 0..4 scale.
            case HeatmapMode.DockRating:
            case HeatmapMode.TradeRating:
            case HeatmapMode.Prosperity:
            case HeatmapMode.Stability:
            case HeatmapMode.Security:
                {
                    float t = Mathf.InverseLerp(minValue, maxValue, value);
                    return Color.Lerp(lowColor, highColor, t);
                }

            default:
                return unknownColor;
        }
    }

    private Color EvaluatePopulation(float pop)
    {
        float t = Mathf.InverseLerp(populationMin, populationMax, pop);
        // Quantity ramp: dark→bright (single-hue)
        return Color.Lerp(populationLow, populationHigh, t);
    }

    private Color EvaluateFoodBalance(float food)
    {
        // Centered at 0 (neutral). Negative ramps red→neutral, positive ramps neutral→green.
        if (food < 0f)
        {
            float t = Mathf.InverseLerp(foodMin, 0f, food); // foodMin..0
            return Color.Lerp(foodNegative, foodNeutral, t);
        }
        else
        {
            float t = Mathf.InverseLerp(0f, foodMax, food); // 0..foodMax
            return Color.Lerp(foodNeutral, foodPositive, t);
        }
    }
}
