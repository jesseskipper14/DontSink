using UnityEngine;

public sealed class WorldMapPlayerMarkerUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform marker;
    [SerializeField] private RectTransform mapPanel;
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapPlayerRef playerRef;

    [Header("Placement")]
    [SerializeField] private Vector2 pixelOffset = Vector2.zero;

    [Header("Travel Marker")]
    [Range(0f, 1f)]
    [SerializeField] private float debugTravelT = 0.5f; // midpoint until boat sim drives it

    private Vector2 _min;
    private Vector2 _max;
    private bool _boundsValid;

    private void Reset()
    {
        marker = transform as RectTransform;
        mapPanel = GetComponentInParent<RectTransform>();
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        playerRef = FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        EnsureRefs();

        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt += RecomputeBounds;

        if (generator != null)
            generator.OnGraphGenerated += RecomputeBounds;

        RecomputeBounds();
        Refresh();
    }

    private void OnDisable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt -= RecomputeBounds;

        if (generator != null)
            generator.OnGraphGenerated -= RecomputeBounds;
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void EnsureRefs()
    {
        if (generator == null) generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        if (runtimeBinder == null) runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        if (playerRef == null) playerRef = FindAnyObjectByType<WorldMapPlayerRef>(FindObjectsInactive.Include);
        if (marker == null) marker = transform as RectTransform;
    }

    private void RecomputeBounds()
    {
        _boundsValid = false;

        if (generator?.graph == null) return;
        var g = generator.graph;
        if (g.nodes == null || g.nodes.Count == 0) return;

        Vector2 min = g.nodes[0].position;
        Vector2 max = g.nodes[0].position;

        for (int i = 1; i < g.nodes.Count; i++)
        {
            var p = g.nodes[i].position;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        Vector2 size = max - min;
        if (size.x < 0.001f) size.x = 1f;
        if (size.y < 0.001f) size.y = 1f;

        Vector2 pad = size * 0.08f;
        min -= pad;
        max += pad;

        _min = min;
        _max = max;
        _boundsValid = true;
    }

    private void Refresh()
    {
        if (marker == null || mapPanel == null) return;
        if (!_boundsValid) return;
        if (generator?.graph == null) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;

        // Prefer showing travel marker if we are currently in transit
        var gs = GameState.I;
        if (gs != null && gs.activeTravel != null)
        {
            if (TryGetTravelEndpoints(gs.activeTravel, out var fromIdx, out var toIdx))
            {
                Vector2 a = GraphToPanel(generator.graph.nodes[fromIdx].position, _min, _max, mapPanel);
                Vector2 b = GraphToPanel(generator.graph.nodes[toIdx].position, _min, _max, mapPanel);

                float t = Mathf.Clamp01(debugTravelT);
                marker.anchoredPosition = Vector2.Lerp(a, b, t) + pixelOffset;
                return;
            }
        }

        // Otherwise, show player snapped to current node
        if (playerRef?.State == null) return;

        if (!runtimeBinder.Registry.TryGetByStableId(playerRef.State.currentNodeId, out var rt) || rt == null)
            return;

        int nodeIndex = rt.NodeIndex;
        if (nodeIndex < 0 || nodeIndex >= generator.graph.nodes.Count)
            return;

        Vector2 anchored = GraphToPanel(generator.graph.nodes[nodeIndex].position, _min, _max, mapPanel);
        marker.anchoredPosition = anchored + pixelOffset;
    }

    private bool TryGetTravelEndpoints(TravelPayload payload, out int fromIndex, out int toIndex)
    {
        fromIndex = -1;
        toIndex = -1;

        if (payload == null) return false;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return false;

        if (string.IsNullOrEmpty(payload.fromNodeStableId) || string.IsNullOrEmpty(payload.toNodeStableId))
            return false;

        if (!runtimeBinder.Registry.TryGetByStableId(payload.fromNodeStableId, out var fromRt) || fromRt == null)
            return false;

        if (!runtimeBinder.Registry.TryGetByStableId(payload.toNodeStableId, out var toRt) || toRt == null)
            return false;

        fromIndex = fromRt.NodeIndex;
        toIndex = toRt.NodeIndex;
        return fromIndex >= 0 && toIndex >= 0;
    }

    private static Vector2 GraphToPanel(Vector2 graphPos, Vector2 min, Vector2 max, RectTransform mapPanel)
    {
        Vector2 size = max - min;
        float nx = (graphPos.x - min.x) / (size.x <= 0.0001f ? 1f : size.x);
        float ny = (graphPos.y - min.y) / (size.y <= 0.0001f ? 1f : size.y);

        float x = (nx - 0.5f) * mapPanel.rect.width;
        float y = (ny - 0.5f) * mapPanel.rect.height;
        return new Vector2(x, y);
    }
}