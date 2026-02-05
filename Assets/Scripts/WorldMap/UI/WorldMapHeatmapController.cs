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
        TradeRating
    }

    [Header("Auto Refresh")]
    public bool autoRefresh = true;
    [Min(0.1f)] public float refreshIntervalSeconds = 1f;

    private float _refreshTimer;

    [Header("Refs")]
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapNodeSpawner spawner;

    [Header("UI (optional)")]
    [SerializeField] private Text modeLabel;

    [Header("Colors")]
    public Color lowColor = new Color(1f, 0.25f, 0.25f, 1f);   // red-ish
    public Color highColor = new Color(0.25f, 1f, 0.35f, 1f);  // green-ish
    public Color unknownColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Normalization")]
    [Range(0f, 4f)] public float minValue = 0f;
    [Range(0f, 4f)] public float maxValue = 4f;

    [SerializeField] private HeatmapMode mode = HeatmapMode.None;

    private void Reset()
    {
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        spawner = FindAnyObjectByType<WorldMapNodeSpawner>();
    }

    private void Start()
    {
        Apply();
    }

    // Hook these to buttons, or call from code.
    public void SetModeNone() => SetMode(HeatmapMode.None);
    public void SetModeProsperity() => SetMode(HeatmapMode.Prosperity);
    public void SetModeStability() => SetMode(HeatmapMode.Stability);
    public void SetModeSecurity() => SetMode(HeatmapMode.Security);
    public void SetModeDock() => SetMode(HeatmapMode.DockRating);
    public void SetModeTrade() => SetMode(HeatmapMode.TradeRating);

    private void Update()
    {
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
        if (modeLabel != null)
            modeLabel.text = $"Heatmap: {mode}";

        if (generator == null || generator.graph == null) return;

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
            if (id < 0 || id >= generator.graph.nodes.Count) continue;

            var node = generator.graph.nodes[id];
            float value;
            bool hasValue = TryGetValue(node, out value);

            if (!hasValue)
            {
                v.SetTint(unknownColor);
                continue;
            }

            float t = Mathf.InverseLerp(minValue, maxValue, value);
            v.SetTint(Color.Lerp(lowColor, highColor, t));
        }
    }

    private bool TryGetValue(MapNode node, out float value)
    {
        value = 0f;

        switch (mode)
        {
            case HeatmapMode.None:
                return false;

            case HeatmapMode.DockRating:
                value = node.dock.rating;
                return true;

            case HeatmapMode.TradeRating:
                value = node.tradeHub.rating;
                return true;

            case HeatmapMode.Prosperity:
                return TryGetStat(node, NodeStatId.Prosperity, out value);

            case HeatmapMode.Stability:
                return TryGetStat(node, NodeStatId.Stability, out value);

            case HeatmapMode.Security:
                return TryGetStat(node, NodeStatId.Security, out value);

            default:
                return false;
        }
    }

    private static bool TryGetStat(MapNode node, NodeStatId id, out float value)
    {
        for (int i = 0; i < node.stats.Count; i++)
        {
            if (node.stats[i].id == id)
            {
                value = node.stats[i].stat.value;
                return true;
            }
        }

        value = 0f;
        return false;
    }
}
