using UnityEngine;

public sealed class BoatTerrainChunk2D : MonoBehaviour
{
    public long Index { get; private set; }
    public EdgeCollider2D Edge { get; private set; }
    private Mesh _mesh;
    private Vector2[] _points;
    private Vector3[] _vertices;
    private Vector2[] _uv;

    public void Build(BoatTerrainPlan plan, long index, float originX, Material material,
        int layer, int sortingLayer, int sortingOrder)
    {
        Index = index;
        gameObject.layer = layer;
        transform.position = new Vector3((float)(originX + index * (double)plan.Width), 0, 0);
        _points = plan.BuildSurface(index);
        Edge = gameObject.AddComponent<EdgeCollider2D>();
        Edge.points = _points;
        Edge.useAdjacentStartPoint = Edge.useAdjacentEndPoint = true;
        // Extend the actual local tangent. Adjacent forecast must never affect
        // collision normals of committed geometry or its subsequent reload.
        Edge.adjacentStartPoint = _points[0] * 2f - _points[1];
        Edge.adjacentEndPoint = _points[^1] * 2f - _points[^2];

        int count = _points.Length;
        var vertices = new Vector3[count * 2];
        var uv = new Vector2[vertices.Length];
        _vertices = vertices; _uv = uv;
        var colors = new Color[vertices.Length];
        var triangles = new int[(count - 1) * 6];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = _points[i];
            vertices[count + i] = new Vector3(_points[i].x, plan.BottomY, 0);
            uv[i] = new Vector2(transform.position.x + _points[i].x, _points[i].y);
            uv[count + i] = new Vector2(uv[i].x, plan.BottomY);
            colors[i] = colors[count + i] = Color.white;
            if (i == count - 1) continue;
            int t = i * 6;
            triangles[t] = i; triangles[t + 1] = i + 1; triangles[t + 2] = count + i + 1;
            triangles[t + 3] = i; triangles[t + 4] = count + i + 1; triangles[t + 5] = count + i;
        }
        _mesh = new Mesh { name = $"Seabed {index}" };
        _mesh.vertices = vertices; _mesh.uv = uv; _mesh.colors = colors; _mesh.triangles = triangles;
        _mesh.RecalculateBounds(); _mesh.RecalculateNormals();
        gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = sortingLayer; renderer.sortingOrder = sortingOrder;
    }

    /// <summary>Mesh, sampler and collider always consume the same surface.</summary>
    public void UpdateSurface(System.Func<int, float> height)
    {
        bool changed = false;
        int count = _points.Length;
        for (int i = 0; i < count; i++)
        {
            float y = height(i);
            if (Mathf.Abs(y - _points[i].y) < .0001f) continue;
            changed = true;
            _points[i].y = y; _vertices[i].y = y; _uv[i].y = y;
        }
        if (!changed) return;
        Edge.points = _points;
        Edge.adjacentStartPoint = _points[0] * 2 - _points[1];
        Edge.adjacentEndPoint = _points[^1] * 2 - _points[^2];
        _mesh.vertices = _vertices; _mesh.uv = _uv;
        _mesh.RecalculateBounds(); _mesh.RecalculateNormals();
    }

    public bool TrySample(float worldX, out float y, out float slope)
    {
        y = slope = 0;
        float x = worldX - transform.position.x;
        if (_points == null || x < 0 || x > _points[^1].x) return false;
        float step = _points[^1].x / (_points.Length - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(x / step), 0, _points.Length - 2);
        Vector2 a = _points[i], b = _points[i + 1];
        y = Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, x));
        slope = Mathf.Abs(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        return true;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }
    }
}
