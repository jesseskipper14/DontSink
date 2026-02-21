using UnityEngine;

public sealed class WorldMapPlayerMarkerUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform marker;              // the icon (can be this transform)
    [SerializeField] private RectTransform mapPanel;            // same mapPanel used for nodes
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapPlayerRef playerRef;

    [Header("Placement")]
    [SerializeField] private Vector2 pixelOffset = Vector2.zero;

    private void Reset()
    {
        marker = transform as RectTransform;
        mapPanel = GetComponentInParent<RectTransform>(); // override in inspector if wrong
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        playerRef = FindAnyObjectByType<WorldMapPlayerRef>();
    }

    private void OnEnable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt += Refresh;
    }

    private void OnDisable()
    {
        if (runtimeBinder != null)
            runtimeBinder.OnRuntimeBuilt -= Refresh;
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (marker == null || mapPanel == null) return;
        if (generator?.graph == null) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;
        if (playerRef?.State == null) return;

        if (!runtimeBinder.Registry.TryGetByStableId(playerRef.State.currentNodeId, out var rt) || rt == null)
            return;

        int nodeIndex = rt.NodeIndex;
        if (nodeIndex < 0 || nodeIndex >= generator.graph.nodes.Count)
            return;

        // Compute padded bounds (same math as overlay)
        var g = generator.graph;

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

        Vector2 anchored = GraphToPanel(g.nodes[nodeIndex].position, min, max, mapPanel);
        marker.anchoredPosition = anchored + pixelOffset;
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
