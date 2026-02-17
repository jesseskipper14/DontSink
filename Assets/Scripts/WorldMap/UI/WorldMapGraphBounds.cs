using UnityEngine;

public readonly struct WorldMapGraphBounds
{
    public readonly Vector2 min;
    public readonly Vector2 max;

    public WorldMapGraphBounds(Vector2 min, Vector2 max)
    {
        this.min = min;
        this.max = max;
    }

    public static WorldMapGraphBounds ComputePadded(MapGraph graph, float padFrac = 0.08f)
    {
        Vector2 min = graph.nodes[0].position;
        Vector2 max = graph.nodes[0].position;

        for (int i = 1; i < graph.nodes.Count; i++)
        {
            var p = graph.nodes[i].position;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        Vector2 size = max - min;
        if (size.x < 0.001f) size.x = 1f;
        if (size.y < 0.001f) size.y = 1f;

        Vector2 pad = size * padFrac;
        return new WorldMapGraphBounds(min - pad, max + pad);
    }

    public Vector2 GraphToPanel(Vector2 graphPos, RectTransform mapPanel)
    {
        Vector2 size = max - min;
        float nx = (graphPos.x - min.x) / (size.x <= 0.0001f ? 1f : size.x);
        float ny = (graphPos.y - min.y) / (size.y <= 0.0001f ? 1f : size.y);

        float x = (nx - 0.5f) * mapPanel.rect.width;
        float y = (ny - 0.5f) * mapPanel.rect.height;
        return new Vector2(x, y);
    }
}
