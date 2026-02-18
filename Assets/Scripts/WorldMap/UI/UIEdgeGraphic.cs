using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIEdgeGraphic : Graphic
{
    [Min(1f)] public float lineThickness = 2f;

    private readonly List<Segment> _segments = new List<Segment>(256);

    public void SetSegments(List<Vector2> a, List<Vector2> b)
    {
        SetSegments(a, b, null);
    }

    public void SetSegments(List<Vector2> a, List<Vector2> b, List<Color32> colors)
    {
        _segments.Clear();

        int n = Mathf.Min(a.Count, b.Count);
        if (colors != null) n = Mathf.Min(n, colors.Count);

        for (int i = 0; i < n; i++)
            _segments.Add(new Segment(a[i], b[i], colors != null ? colors[i] : (Color32)color));

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_segments.Count == 0) return;

        float t = Mathf.Max(1f, lineThickness);

        for (int i = 0; i < _segments.Count; i++)
        {
            var s = _segments[i];
            AddLine(vh, s.a, s.b, t, s.color);
        }
    }

    private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 col)
    {
        Vector2 dir = (b - a);
        float len = dir.magnitude;
        if (len <= 0.0001f) return;

        dir /= len;
        Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        UIVertex v0 = UIVertex.simpleVert; v0.color = col; v0.position = a - n;
        UIVertex v1 = UIVertex.simpleVert; v1.color = col; v1.position = a + n;
        UIVertex v2 = UIVertex.simpleVert; v2.color = col; v2.position = b + n;
        UIVertex v3 = UIVertex.simpleVert; v3.color = col; v3.position = b - n;

        int idx = vh.currentVertCount;
        vh.AddVert(v0);
        vh.AddVert(v1);
        vh.AddVert(v2);
        vh.AddVert(v3);

        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx + 0);
    }

    private readonly struct Segment
    {
        public readonly Vector2 a;
        public readonly Vector2 b;
        public readonly Color32 color;

        public Segment(Vector2 a, Vector2 b, Color32 color)
        {
            this.a = a;
            this.b = b;
            this.color = color;
        }
    }
}
