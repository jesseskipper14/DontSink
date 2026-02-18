using UnityEngine;
using UnityEngine.UI;

public sealed class MapOverlayHeatmapController : MonoBehaviour
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

    [Header("Active When Overlay Visible")]
    [SerializeField] private CanvasGroup overlayRoot;

    [Header("Refs")]
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapGraphGenerator generator;

    [Tooltip("NodeContainer that holds the spawned UI buttons.")]
    [SerializeField] private RectTransform nodeContainer;

    [Header("UI (optional)")]
    [SerializeField] private Text modeLabel;

    [Header("Auto Refresh")]
    public bool autoRefresh = true;
    [Min(0.1f)] public float refreshIntervalSeconds = 1f;
    private float _t;

    [Header("Colors")]
    public Color lowColor = new Color(1f, 0.25f, 0.25f, 1f);
    public Color highColor = new Color(0.25f, 1f, 0.35f, 1f);
    public Color unknownColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Normalization (0..4 stats)")]
    [Range(0f, 4f)] public float minValue = 0f;
    [Range(0f, 4f)] public float maxValue = 4f;

    [Header("Population")]
    public float populationMin = 0f;
    public float populationMax = 5000f;
    public Color populationLow = new Color(0.15f, 0.15f, 0.35f, 1f);
    public Color populationHigh = new Color(0.55f, 0.75f, 1f, 1f);

    [Header("FoodBalance")]
    public float foodMin = -4f;
    public float foodMax = 4f;
    public Color foodNegative = new Color(1f, 0.25f, 0.25f, 1f);
    public Color foodNeutral = new Color(0.75f, 0.75f, 0.75f, 1f);
    public Color foodPositive = new Color(0.25f, 1f, 0.35f, 1f);

    [Header("Mode")]
    [SerializeField] private HeatmapMode mode = HeatmapMode.None;

    [Header("Primary Node Color")]
    public bool preservePrimaryYellow = true;
    public Color primaryColor = Color.yellow;

    // Hook these to buttons
    public void SetModeNone() => SetMode(HeatmapMode.None);
    public void SetModeProsperity() => SetMode(HeatmapMode.Prosperity);
    public void SetModeStability() => SetMode(HeatmapMode.Stability);
    public void SetModeSecurity() => SetMode(HeatmapMode.Security);
    public void SetModeDock() => SetMode(HeatmapMode.DockRating);
    public void SetModeTrade() => SetMode(HeatmapMode.TradeRating);
    public void SetModePopulation() => SetMode(HeatmapMode.Population);
    public void SetModeFoodBalance() => SetMode(HeatmapMode.FoodBalance);

    private bool OverlayVisible()
    {
        if (overlayRoot == null) return true;
        return overlayRoot.alpha > 0.001f && overlayRoot.blocksRaycasts;
    }

    private void Reset()
    {
        overlayRoot = GetComponentInParent<CanvasGroup>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
    }

    private void Update()
    {
        if (!OverlayVisible()) return;
        if (!autoRefresh) return;
        if (mode == HeatmapMode.None) return;

        _t += Time.unscaledDeltaTime;
        if (_t >= refreshIntervalSeconds)
        {
            _t = 0f;
            Apply();
        }
    }

    public void SetMode(HeatmapMode newMode)
    {
        mode = newMode;
        Apply();
    }

    [ContextMenu("Apply Overlay Heatmap")]
    public void Apply()
    {
        if (!OverlayVisible()) return;

        if (modeLabel != null)
            modeLabel.text = $"Heatmap: {mode}";

        if (mode == HeatmapMode.None)
        {
            Clear();
            return;
        }

        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;
        if (generator?.graph == null) return;
        if (nodeContainer == null) return;

        for (int i = 0; i < nodeContainer.childCount; i++)
        {
            var child = nodeContainer.GetChild(i);
            var ht = child.GetComponent<MapNodeHoverTarget>();
            if (ht == null) continue;

            int nodeIndex = ht.NodeId;
            if (nodeIndex < 0 || nodeIndex >= generator.graph.nodes.Count) continue;

            var img = child.GetComponent<Image>();
            if (img == null) continue;

            // Optional: keep primary nodes yellow, regardless of heatmap
            if (preservePrimaryYellow && generator.graph.nodes[nodeIndex].isPrimary)
            {
                img.color = primaryColor;
                continue;
            }

            if (!runtimeBinder.Registry.TryGetByIndex(nodeIndex, out var rt) || rt == null || rt.State == null)
            {
                img.color = unknownColor;
                continue;
            }

            if (!TryGetValue(rt, out float value))
            {
                img.color = unknownColor;
                continue;
            }

            img.color = EvaluateColor(mode, value);
        }
    }

    [ContextMenu("Clear Overlay Heatmap")]
    public void Clear()
    {
        if (generator?.graph == null) return;
        if (nodeContainer == null) return;

        for (int i = 0; i < nodeContainer.childCount; i++)
        {
            var child = nodeContainer.GetChild(i);
            var ht = child.GetComponent<MapNodeHoverTarget>();
            if (ht == null) continue;

            int nodeIndex = ht.NodeId;
            if (nodeIndex < 0 || nodeIndex >= generator.graph.nodes.Count) continue;

            var img = child.GetComponent<Image>();
            if (img == null) continue;

            // Restore your default node coloring (primary yellow, else white)
            if (generator.graph.nodes[nodeIndex].isPrimary)
                img.color = primaryColor;
            else
                img.color = Color.white;
        }
    }

    private bool TryGetValue(MapNodeRuntime rt, out float value)
    {
        value = 0f;
        var s = rt.State;
        if (s == null) return false;

        switch (mode)
        {
            case HeatmapMode.DockRating: value = s.GetStat(NodeStatId.DockRating).value; return true;
            case HeatmapMode.TradeRating: value = s.GetStat(NodeStatId.TradeRating).value; return true;
            case HeatmapMode.Prosperity: value = s.GetStat(NodeStatId.Prosperity).value; return true;
            case HeatmapMode.Stability: value = s.GetStat(NodeStatId.Stability).value; return true;
            case HeatmapMode.Security: value = s.GetStat(NodeStatId.Security).value; return true;
            case HeatmapMode.Population: value = s.population; return true;
            case HeatmapMode.FoodBalance: value = s.GetStat(NodeStatId.FoodBalance).value; return true;
            default: return false;
        }
    }

    private Color EvaluateColor(HeatmapMode m, float value)
    {
        switch (m)
        {
            case HeatmapMode.Population:
                return Color.Lerp(populationLow, populationHigh,
                    Mathf.InverseLerp(populationMin, populationMax, value));

            case HeatmapMode.FoodBalance:
                if (value < 0f)
                    return Color.Lerp(foodNegative, foodNeutral, Mathf.InverseLerp(foodMin, 0f, value));
                else
                    return Color.Lerp(foodNeutral, foodPositive, Mathf.InverseLerp(0f, foodMax, value));

            default:
                return Color.Lerp(lowColor, highColor, Mathf.InverseLerp(minValue, maxValue, value));
        }
    }
}
