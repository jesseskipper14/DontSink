using System.Globalization;
using UnityEngine;

/// <summary>Read-only, world-space presentation of this settlement's current simulation state.</summary>
[DisallowMultipleComponent]
public sealed class TownCenterStatusBoard : MonoBehaviour
{
    [SerializeField] private Vector2 readableSize = new Vector2(2.6f, 1.8f);
    [SerializeField] private string sortingLayer = "GroundFront";
    [SerializeField] private int sortingOrder = 6;
    [SerializeField] private Color textColor = new Color(.93f, .92f, .79f, 1);
    [SerializeField, Min(1)] private float trendWindowSeconds = 10;
    private static readonly string[] Names = { "Population", "Prosperity", "Stability", "Security", "Food Balance", "Trade Rating", "Dock Rating" };
    private static readonly NodeStatId[] Ratings = { NodeStatId.Prosperity, NodeStatId.Stability, NodeStatId.Security, NodeStatId.FoodBalance, NodeStatId.TradeRating, NodeStatId.DockRating };
    private TownCenterBuilding building;
    private TextMesh title, label, valueText, arrow;
    private MapNodeState sampledState;
    private readonly float[] baseline = new float[7], previous = new float[7];
    private readonly bool[] baselineValid = new bool[7], previousValid = new bool[7];
    private readonly int[] trends = new int[7];
    private float nextRefresh, nextTrendWindow;
    private int selected;
    private string lastTitle, lastValue, lastLabel;
    private int lastTrend = int.MinValue;
    public string NodeStableId => building != null ? building.NodeStableId : null;
    public string DisplayedValues => valueText != null ? valueText.text : null;
    public int SelectedStat => selected;
    public int DisplayedTrend => trends[selected];

    public void Bind(TownCenterBuilding owner)
    {
        if (building != owner) { sampledState = null; selected = 0; }
        building = owner;
        EnsureText();
        RefreshNow();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        RefreshNow();
    }

    public void CycleStat()
    {
        selected = (selected + 1) % Names.Length;
        RefreshNow();
    }

    public void RefreshNow()
    {
        if (building == null) return;
        EnsureText();
        MapNodeState state = null;
        var store = GameState.I?.worldMap?.byNodeStableId;
        if (store != null && NodeStableId != null) store.TryGetValue(NodeStableId, out state);
        SampleTrends(state);
        string heading = HarborTravelService.TryGetNode(NodeStableId, out var node) &&
            !string.IsNullOrWhiteSpace(node.displayName) ? node.displayName : "SETTLEMENT";
        string content = TryValue(state, selected, out float value) ? FormatValue(value, selected == 0) : "—";
        if (heading == lastTitle && content == lastValue && Names[selected] == lastLabel && trends[selected] == lastTrend) return;
        lastTitle = heading; lastValue = content; lastLabel = Names[selected]; lastTrend = trends[selected];
        title.text = heading;
        label.text = Names[selected];
        valueText.text = content;
        arrow.text = lastTrend > 0 ? "↑" : lastTrend < 0 ? "↓" : "—";
        arrow.color = lastTrend > 0 ? new Color(.35f, .85f, .42f) : lastTrend < 0 ? new Color(1, .35f, .32f) : new Color(.6f, .6f, .55f);
        Fit(title, readableSize.x, .3f);
        Fit(label, readableSize.x, .35f);
        Fit(valueText, readableSize.x * .7f, .65f);
        Fit(arrow, readableSize.x * .18f, .5f);
    }

    public static string FormatValue(float value, bool population) =>
        value.ToString(population ? "0" : "0.0", CultureInfo.InvariantCulture);

    private static bool TryValue(MapNodeState state, int index, out float value)
    {
        value = 0;
        if (state == null) return false;
        if (index == 0) { value = state.population; return true; }
        if (!state.TryGetStat(Ratings[index - 1], out var stat)) return false;
        value = stat.value;
        return true;
    }

    private void SampleTrends(MapNodeState state)
    {
        bool reset = !ReferenceEquals(sampledState, state);
        bool roll = Time.unscaledTime >= nextTrendWindow;
        for (int i = 0; i < Names.Length; i++)
        {
            bool valid = TryValue(state, i, out float current);
            if (reset || !valid || !baselineValid[i]) trends[i] = 0;
            else
            {
                float delta = current - baseline[i];
                float epsilon = i == 0 ? .01f : .0001f;
                trends[i] = delta > epsilon ? 1 : delta < -epsilon ? -1 : 0;
            }
            // Keep the previous ten-second sample as the baseline, so the arrow
            // survives a rollover rather than briefly clearing on each sample.
            if (reset || roll)
            {
                baseline[i] = reset ? current : previous[i];
                baselineValid[i] = reset ? valid : previousValid[i];
                previous[i] = current;
                previousValid[i] = valid;
            }
        }
        sampledState = state;
        if (reset || roll) nextTrendWindow = Time.unscaledTime + trendWindowSeconds;
    }

    private void EnsureText()
    {
        if (title != null) return;
        float top = readableSize.y / 2;
        title = MakeText("Settlement name", new Vector2(0, top - .15f), .48f);
        label = MakeText("Selected stat", new Vector2(0, top - .58f), .6f);
        valueText = MakeText("Current value", new Vector2(-readableSize.x * .08f, top - 1.1f), 1.25f);
        arrow = MakeText("Recent trend", new Vector2(readableSize.x * .37f, top - 1.1f), .95f);
    }

    private TextMesh MakeText(string name, Vector2 position, float size)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = new Vector3(position.x, position.y, -.01f);
        var text = child.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64; text.characterSize = size;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.richText = false; text.color = textColor;
        var renderer = child.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = text.font.material;
        renderer.sortingLayerName = sortingLayer; renderer.sortingOrder = sortingOrder;
        return text;
    }

    private static void Fit(TextMesh text, float width, float height)
    {
        text.transform.localScale = Vector3.one;
        var bounds = text.GetComponent<MeshRenderer>().localBounds.size;
        text.transform.localScale = Vector3.one * Mathf.Min(1, width / Mathf.Max(.001f, bounds.x), height / Mathf.Max(.001f, bounds.y));
    }

    private void OnGUI()
    {
        var manager = CameraManager.Instance;
        if (building == null || manager == null || !manager.CanProvideGameplayInput || GameplayInputBlocker.IsBlocked ||
            manager.OwnerTarget == null || manager.OwnerTarget.gameObject.scene != gameObject.scene ||
            Vector2.Distance(manager.OwnerTarget.position, transform.position) > 6) return;
        var camera = manager.ActiveCamera;
        if (camera == null) return;
        Vector3 left = camera.WorldToScreenPoint(transform.TransformPoint(new Vector3(-readableSize.x / 2, -readableSize.y / 2 + .12f, 0)));
        Vector3 right = camera.WorldToScreenPoint(transform.TransformPoint(new Vector3(readableSize.x / 2, -readableSize.y / 2 + .12f, 0)));
        if (left.z <= 0 || !camera.pixelRect.Contains((left + right) * .5f)) return;
        float width = Mathf.Clamp(Mathf.Abs(right.x - left.x) * .85f, 84, 220);
        var rect = new Rect((left.x + right.x - width) / 2, Screen.height - (left.y + right.y) / 2 - 12, width, 24);
        if (GUI.Button(rect, "Next stat  ›")) CycleStat();
    }
}
